using System.Text;
using Piersec.Core.Modelo;
using Piersec.Core.Operacao;
using Piersec.Core.Armazenamento;
using Piersec.Core.Padronizacao;
using Piersec.Core.Pipeline;

namespace Piersec.Tests;

/// <summary>Operação automática (SOC simulado): travas, ensaio, limite, idempotência, ciclo de vida e ações humanas.</summary>
public class OperacaoTests : IDisposable
{
    private const string CabecalhoVuln =
        "agent.name,host.os.name,package.name,package.version,vulnerability.id,vulnerability.description,vulnerability.severity,vulnerability.score.base,vulnerability.reference";
    private const string CabecalhoAlertas = "\"timestamp,agent.name,rule.description,data.devname,rule.level,rule.id\"";

    private readonly string _pasta = Directory.CreateTempSubdirectory("piersec-op-").FullName;
    private readonly RelogioAjustavel _relogio = new(Fabrica.Agora);

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    // ---------- montagem (dados fictícios) ----------

    /// <summary>n falhas críticas do programa na máquina (CVEs fictícias, ano 2099).</summary>
    private static IEnumerable<string> Falhas(string maquina, string sistema, string programa, int n, int primeiraCve = 1) =>
        Enumerable.Range(primeiraCve, n).Select(i => $"{maquina},{sistema},{programa},1.0,CVE-2099-{i:0000},d,Critical,9.8,ref");

    private static Lote Lote(IEnumerable<string> vulnerabilidades, bool comAlertas = false)
    {
        List<ArquivoEnviado> arquivos = [Fabrica.Csv("vuln.csv", [CabecalhoVuln, .. vulnerabilidades])];
        if (comAlertas)
            arquivos.Add(Fabrica.Csv("alertas.csv", CabecalhoAlertas, "\"Oct 7,2026 @ 08:00:00.000,coletor,desc,FW-01,3,100\""));
        var lote = Fabrica.ProcessadorReal().Processar(arquivos);
        lote = lote with { Validacao = Fabrica.Validador().Validar(lote) };
        Assert.True(lote.Validacao.TudoCerto, "o lote fictício deveria passar na validação");
        return lote;
    }

    private static Lote SoAlertas()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("alertas.csv", CabecalhoAlertas, "\"Oct 7,2026 @ 08:00:00.000,coletor,desc,FW-01,3,100\"")]);
        return lote with { Validacao = Fabrica.Validador().Validar(lote) };
    }

    private ServicoOperacao Servico(int nivel = 2, bool ensaio = false, int limite = 20, ICifrador? cifrador = null) =>
        new(Fabrica.Regras, new RepositorioChamados(_pasta, cifrador), _relogio, NormalizadorDatas.FusoBrasilia,
            Fabrica.Regras.Automacao with { NivelAutonomia = nivel, ModoEnsaio = ensaio, LimiteAcoesPorDia = limite });

    private static ItemOperacao Do(ResultadoOperacao r, string playbook) => r.Itens.Single(i => i.PlaybookId == playbook);

    // ---------- níveis e travas ----------

    [Fact]
    public void Nivel_2_abre_chamado_sozinho_mas_isolar_maquina_sempre_espera_aprovacao()
    {
        var r = Servico(nivel: 2).Processar(Lote(Falhas("PC-01", "Windows 10", "openssl", 6)));

        var programa = Do(r, "PB-ATUALIZAR-PROGRAMA");
        Assert.Equal(ResultadoItem.AgiuSozinho, programa.Resultado);
        Assert.Equal(EstadoChamado.Aberto, programa.Estado);

        var isolar = Do(r, "PB-ISOLAR-MAQUINA");                    // 6 críticas ≥ 5: dispara
        Assert.Equal(EstadoChamado.AguardandoAprovacao, isolar.Estado);
        Assert.Contains("alto impacto", isolar.Motivo);

        var corrigir = Do(r, "PB-CORRIGIR-MAQUINA");                // impacto médio: só sozinho no nível 3
        Assert.Equal(EstadoChamado.AguardandoAprovacao, corrigir.Estado);
        Assert.Contains("a partir do nível 3", corrigir.Motivo);

        Assert.All(r.Itens.Where(i => i.PlaybookId == "PB-MEDIR-FONTES"), i => Assert.Equal(EstadoChamado.Sugerido, i.Estado));
        Assert.Equal(1, r.Sozinho);
    }

    [Fact]
    public void Nivel_0_so_sugere()
    {
        var r = Servico(nivel: 0).Processar(Lote(Falhas("PC-01", "Windows 10", "openssl", 6)));

        Assert.NotEmpty(r.Itens);
        Assert.All(r.Itens, i => Assert.Equal(EstadoChamado.Sugerido, i.Estado));
        Assert.Equal(0, r.Sozinho);
    }

    [Fact]
    public void Nivel_1_pede_aprovacao_para_tudo()
    {
        var r = Servico(nivel: 1).Processar(Lote(Falhas("PC-01", "Windows 10", "openssl", 6)));

        Assert.Equal(0, r.Sozinho);
        Assert.Equal(EstadoChamado.AguardandoAprovacao, Do(r, "PB-ATUALIZAR-PROGRAMA").Estado);
    }

    [Fact]
    public void Nivel_3_corrige_estacao_sozinho_mas_servidor_sempre_tem_pessoa_no_circuito()
    {
        var r = Servico(nivel: 3).Processar(Lote([
            .. Falhas("PC-01", "Windows 10", "openssl", 2),
            .. Falhas("SRV-01", "Windows Server 2019", "java", 2, primeiraCve: 10)]));

        var corrigir = r.Itens.Where(i => i.PlaybookId == "PB-CORRIGIR-MAQUINA").ToList();
        var estacao = corrigir.Single(i => i.Titulo.Contains("PC-01"));
        var servidor = corrigir.Single(i => i.Titulo.Contains("SRV-01"));
        Assert.Equal(ResultadoItem.AgiuSozinho, estacao.Resultado);
        Assert.Equal(EstadoChamado.AguardandoAprovacao, servidor.Estado);
        Assert.Contains("servidor (ativo crítico)", servidor.Motivo);
    }

    [Fact]
    public void Lote_que_nao_passou_na_validacao_nao_dispara_nada_sozinho()
    {
        var lote = Lote(Falhas("PC-01", "Windows 10", "openssl", 2));
        lote = lote with { Validacao = new ResultadoValidacao([new Verificacao("V99", "teste", false, "reprovada de propósito")]) };

        var r = Servico(nivel: 3).Processar(lote);

        Assert.False(r.LoteValidado);
        Assert.Equal(0, r.Sozinho);
        Assert.Contains("verificações de consistência", Do(r, "PB-ATUALIZAR-PROGRAMA").Motivo);
    }

    [Fact]
    public void Modo_ensaio_mostra_o_que_faria_e_nao_grava_nenhum_chamado()
    {
        var servico = Servico(nivel: 2, ensaio: true);

        var r = servico.Processar(Lote(Falhas("PC-01", "Windows 10", "openssl", 2)));

        Assert.True(r.Ensaio);
        Assert.Equal(1, r.Sozinho);                                   // "faria sozinho"
        Assert.All(r.Itens, i => Assert.Null(i.Numero));
        Assert.Empty(servico.Estado().Chamados);
        Assert.Empty(Directory.GetFiles(_pasta, "*", SearchOption.AllDirectories));
        Assert.Equal("faria sozinho", ResultadoItem.AgiuSozinho.Nome(ensaio: true));
    }

    [Fact]
    public void Limite_por_dia_segura_o_excesso_e_volta_a_valer_no_dia_seguinte()
    {
        var servico = Servico(nivel: 2, limite: 1);

        var r = servico.Processar(Lote([.. Falhas("PC-01", "Windows 10", "openssl", 2), .. Falhas("PC-02", "Windows 10", "java", 1, 20)]));

        var programas = r.Itens.Where(i => i.PlaybookId == "PB-ATUALIZAR-PROGRAMA").ToList();
        Assert.Single(programas, i => i.AgiuSozinho);
        Assert.Contains("Limite de 1 ações automáticas por dia", programas.Single(i => !i.AgiuSozinho).Motivo);
        Assert.Equal(1, r.AutomaticasHoje);

        _relogio.Avancar(TimeSpan.FromDays(1));
        var amanha = servico.Processar(Lote([.. Falhas("PC-01", "Windows 10", "openssl", 2), .. Falhas("PC-03", "Windows 10", "chrome", 1, 30)]));
        Assert.True(amanha.Itens.Single(i => i.Titulo.Contains("chrome")).AgiuSozinho);
    }

    [Fact]
    public void Simulacao_mostra_o_que_cada_nivel_faria_com_o_mesmo_lote()
    {
        var r = Servico(nivel: 2).Processar(Lote([
            .. Falhas("PC-01", "Windows 10", "openssl", 6),
            .. Falhas("SRV-01", "Windows Server 2019", "java", 2, 10)]));

        Assert.Equal([0, 1, 2, 3], r.SimulacaoPorNivel.Select(s => s.Nivel));
        Assert.Equal(0, r.SimulacaoPorNivel[0].Sozinho);
        Assert.Equal(0, r.SimulacaoPorNivel[1].Sozinho);
        Assert.Equal(2, r.SimulacaoPorNivel[2].Sozinho);                        // os 2 programas
        Assert.Equal(3, r.SimulacaoPorNivel[3].Sozinho);                        // + corrigir a estação (o servidor e isolar continuam com pessoa)
        Assert.All(r.SimulacaoPorNivel, s => Assert.Equal(r.SimulacaoPorNivel[0].Sugestao + r.SimulacaoPorNivel[0].Aprovacao,
            s.Sozinho + s.Aprovacao + s.Sugestao));                             // o total é o mesmo; muda só o caminho
    }

    // ---------- idempotência e ciclo de vida ----------

    [Fact]
    public void Mesmo_lote_duas_vezes_nao_duplica_chamado()
    {
        var servico = Servico();
        var lote = Lote(Falhas("PC-01", "Windows 10", "openssl", 6));

        servico.Processar(lote);
        var antes = servico.Estado();
        var segunda = servico.Processar(lote);
        var depois = servico.Estado();

        Assert.Equal(antes.Chamados.Count, depois.Chamados.Count);
        Assert.Equal(antes.ProximoNumero, depois.ProximoNumero);
        Assert.All(segunda.Itens, i => Assert.Equal(ResultadoItem.JaExistia, i.Resultado));
        Assert.Equal(0, segunda.Sozinho);
        Assert.Equal(depois.Chamados.Count, depois.Chamados.Select(c => c.Chave).Distinct().Count());
    }

    [Fact]
    public void Chamado_fecha_sozinho_quando_o_problema_some_e_reabre_quando_volta()
    {
        var servico = Servico();
        servico.Processar(Lote(Falhas("PC-01", "Windows 10", "openssl", 2)));
        var numero = servico.Estado().Chamados.Single(c => c.Chave == "PB-ATUALIZAR-PROGRAMA|programa:openssl").Numero;

        var semOpenssl = servico.Processar(Lote(Falhas("PC-01", "Windows 10", "java", 1, 50)));
        Assert.Equal(ResultadoItem.FechouSozinho, semOpenssl.Itens.Single(i => i.Numero == numero).Resultado);
        Assert.Equal(EstadoChamado.Resolvido, servico.Estado().Chamados.Single(c => c.Numero == numero).Estado);

        var voltou = servico.Processar(Lote(Falhas("PC-01", "Windows 10", "openssl", 2)));
        var item = voltou.Itens.Single(i => i.Numero == numero);                 // o MESMO chamado, não um novo
        Assert.Equal(ResultadoItem.Reaberto, item.Resultado);
        Assert.Equal(EstadoChamado.Aberto, item.Estado);
        var historico = servico.Estado().Chamados.Single(c => c.Numero == numero).Historico.Select(h => h.Evento).ToList();
        Assert.Equal([ServicoOperacao.EventoCriado, ServicoOperacao.EventoFeitoSozinho, ServicoOperacao.EventoFechadoSozinho,
                      ServicoOperacao.EventoReaberto, ServicoOperacao.EventoFeitoSozinho], historico);
    }

    [Fact]
    public void Lote_sem_dados_de_vulnerabilidade_nao_fecha_chamado_de_vulnerabilidade()
    {
        var servico = Servico();
        servico.Processar(Lote(Falhas("PC-01", "Windows 10", "openssl", 2)));

        var r = servico.Processar(SoAlertas());

        var item = r.Itens.Single(i => i.PlaybookId == "PB-ATUALIZAR-PROGRAMA");
        Assert.Equal(ResultadoItem.MantidoSemConferir, item.Resultado);
        Assert.Contains("fonte ausente não é problema resolvido", item.Motivo);
        Assert.Equal(EstadoChamado.Aberto, servico.Estado().Chamados.Single(c => c.PlaybookId == "PB-ATUALIZAR-PROGRAMA").Estado);
    }

    [Fact]
    public void Problema_fora_do_top_continua_com_o_chamado_aberto()
    {
        var servico = Servico();
        servico.Processar(Lote(Falhas("PC-01", "Windows 10", "openssl", 1)));
        // openssl continua, mas agora 3 outros programas têm mais falhas (top 3 por lote)
        var r = servico.Processar(Lote([
            .. Falhas("PC-01", "Windows 10", "openssl", 1),
            .. Falhas("PC-02", "Windows 10", "java", 3, 10),
            .. Falhas("PC-03", "Windows 10", "chrome", 3, 20),
            .. Falhas("PC-04", "Windows 10", "zoom", 3, 30)]));

        Assert.Equal(ResultadoItem.JaExistia, r.Itens.Single(i => i.Titulo.Contains("openssl")).Resultado);
    }

    // ---------- ações humanas ----------

    [Fact]
    public void Pessoa_aprova_rejeita_com_motivo_e_desfaz()
    {
        var servico = Servico(nivel: 2);
        var r = servico.Processar(Lote(Falhas("PC-01", "Windows 10", "openssl", 6)));
        var isolar = Do(r, "PB-ISOLAR-MAQUINA").Numero!;
        var corrigir = Do(r, "PB-CORRIGIR-MAQUINA").Numero!;
        var programa = Do(r, "PB-ATUALIZAR-PROGRAMA").Numero!;

        var aprovado = servico.Aprovar(isolar, "ana");
        Assert.True(aprovado.Ok, aprovado.Mensagem);
        Assert.Equal(EstadoChamado.Aberto, aprovado.Chamado!.Estado);
        Assert.False(aprovado.Chamado.AgiuSozinho);
        Assert.Equal("ana", aprovado.Chamado.Historico[^1].Quem);

        Assert.False(servico.Rejeitar(corrigir, "ana", "  ").Ok);           // motivo é obrigatório
        var rejeitado = servico.Rejeitar(corrigir, "ana", "janela de manutenção só no sábado; senha=Segredo123");
        Assert.True(rejeitado.Ok);
        Assert.DoesNotContain("Segredo123", rejeitado.Chamado!.Historico[^1].Detalhe);

        Assert.False(servico.Desfazer(corrigir, "ana").Ok);                 // rejeitado não se desfaz
        var desfeito = servico.Desfazer(programa, "ana");
        Assert.True(desfeito.Ok);
        Assert.Contains("SIMULADO", desfeito.Chamado!.Historico[^1].Detalhe);

        Assert.False(servico.Aprovar(programa, "ana").Ok);                  // desfeito não volta por aprovação
        Assert.False(servico.Aprovar("../CH-0001", "ana").Ok);
        Assert.False(servico.Aprovar("CH-9999", "ana").Ok);

        // A decisão da pessoa vale nos próximos lotes: a plataforma não insiste sozinha.
        var proximo = servico.Processar(Lote(Falhas("PC-01", "Windows 10", "openssl", 6)));
        Assert.Equal(ResultadoItem.DecisaoHumanaMantida, proximo.Itens.Single(i => i.Numero == corrigir).Resultado);
        Assert.Equal(ResultadoItem.DecisaoHumanaMantida, proximo.Itens.Single(i => i.Numero == programa).Resultado);
        Assert.Equal(0, proximo.Sozinho);
    }

    [Fact]
    public void Artefato_do_chamado_e_texto_simulado_com_motivo_passos_desfazer_e_historico()
    {
        var servico = Servico();
        var r = servico.Processar(Lote(Falhas("PC-01", "Windows 10", "openssl", 2)));

        var texto = servico.Artefato(Do(r, "PB-ATUALIZAR-PROGRAMA").Numero!)!;

        Assert.Contains("SIMULADO", texto);
        Assert.Contains("Nenhum sistema externo foi acionado", texto);
        Assert.Contains("openssl", texto);
        Assert.Contains("COMO DESFAZER", texto);
        Assert.Contains("1. Testar a versão mais nova de openssl", texto);
        Assert.Contains("feito sozinho", texto);
        Assert.Contains("Confiança: alta", texto);
        Assert.Contains("Ação:        abrir chamado", texto);
        Assert.Contains("Eventos: 2 no lote", texto);
        Assert.Null(servico.Artefato("..\\x"));
    }

    [Fact]
    public void Artefato_de_sugestao_nao_repete_a_acao_nem_mostra_zero_eventos()
    {
        var servico = Servico();
        var r = servico.Processar(Lote(Falhas("PC-01", "Windows 10", "openssl", 2)));

        var texto = servico.Artefato(r.Itens.First(i => i.PlaybookId == "PB-MEDIR-FONTES").Numero!)!;

        Assert.Contains("Situação:    sugestão", texto);
        Assert.DoesNotContain("Ação:", texto);
        Assert.DoesNotContain("Eventos: 0", texto);
        Assert.Contains("Eventos: não se aplica: o problema é justamente a falta de dados deste tipo", texto);
    }

    // ---------- guarda, verificação e regras ----------

    [Fact]
    public void Chamados_ficam_cifrados_no_disco_e_sao_lidos_de_volta()
    {
        var cifrador = new CifradorDeTeste();
        Servico(cifrador: cifrador).Processar(Lote(Falhas("PC-01", "Windows 10", "openssl", 2)));

        var arquivo = Directory.GetFiles(_pasta, "*", SearchOption.AllDirectories).Single();
        Assert.EndsWith(".cifrado", arquivo);
        Assert.DoesNotContain("openssl", Encoding.UTF8.GetString(File.ReadAllBytes(arquivo)));
        Assert.NotEmpty(Servico(cifrador: cifrador).Estado().Chamados);
        Assert.Throws<InvalidOperationException>(() => Servico(cifrador: null).Estado()); // sem a chave: erro, não "começar do zero"
    }

    [Fact]
    public void V13_aprova_a_operacao_real_e_reprova_acao_sozinha_que_furou_a_trava()
    {
        var config = Fabrica.Regras.Automacao;
        var r = Servico(nivel: 3).Processar(Lote([.. Falhas("PC-01", "Windows 10", "openssl", 6), .. Falhas("SRV-01", "Windows Server 2019", "java", 6, 10)]));
        Assert.True(MotorDecisao.Conferir(r, config).Aprovada, MotorDecisao.Conferir(r, config).Detalhe);

        var adulterado = r with
        {
            Itens = [.. r.Itens.Select(i => i.PlaybookId == "PB-ISOLAR-MAQUINA" || i.Titulo.Contains("SRV-01") ? i with { AgiuSozinho = true } : i)],
        };
        var v13 = MotorDecisao.Conferir(adulterado, config);
        Assert.False(v13.Aprovada);
        Assert.Contains("ativo crítico", v13.Detalhe);
    }

    [Fact]
    public void Relatorio_tem_a_secao_do_que_a_plataforma_fez_sozinha()
    {
        var lote = Lote(Falhas("PC-01", "Windows 10", "openssl", 6));
        lote = lote with { Operacao = Servico(nivel: 2).Processar(lote) };

        var html = Fabrica.Gerador().Gerar(lote);

        Assert.Contains("5. O que a plataforma fez sozinha", html);
        Assert.Contains("6. O que esta análise não consegue dizer", html);
        Assert.Contains("Tudo é simulado", html);
        Assert.Contains("sozinha <strong>1 ação</strong>", html);                   // plural certo, sem "(s)"
        Assert.Contains("CH-0001", html);
        Assert.Contains("Ação de alto impacto", html);                              // o porquê de esperar aprovação
    }

    [Fact]
    public async Task Ciclo_completo_roda_a_operacao_e_a_verificacao_V13()
    {
        var conversor = new Core.Relatorio.ConversorPdf();
        Assert.SkipWhen(conversor.Navegador is null, "Nenhum Edge/Chrome nesta máquina.");
        var ciclo = new CicloAnalise(Fabrica.ProcessadorReal(), new RepositorioLotes(_pasta), Fabrica.Gerador(), conversor, Fabrica.Validador(), Servico());

        var resultado = await ciclo.ExecutarAsync([Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")],
            TestContext.Current.CancellationToken);

        var lote = resultado.Lote;
        Assert.NotNull(lote.Operacao);
        Assert.True(lote.Operacao.Sozinho > 0);
        var v13 = lote.Validacao!.Verificacoes.Single(v => v.Id == "V13");
        Assert.True(v13.Aprovada, v13.Detalhe);
        Assert.True(lote.Validacao.TudoCerto);
        Assert.Equal("V12", lote.Validacao.Verificacoes[^1].Id);                    // o relatório final continua sendo o último a ser conferido
    }

    [Fact]
    public void Regras_de_automacao_invalidas_sao_apontadas()
    {
        var ok = Fabrica.Regras.Automacao;
        Assert.Empty(ok.Problemas(Fabrica.Regras.Ativos));

        Assert.Contains((ok with { NivelAutonomia = 4 }).Problemas(Fabrica.Regras.Ativos), p => p.Contains("nivelAutonomia"));
        var marcadorErrado = ok with { Playbooks = [ok.Playbooks[0] with { Titulo = "Atualizar {programa}" }] };
        Assert.Contains(marcadorErrado.Problemas(Fabrica.Regras.Ativos), p => p.Contains("{programa}"));
        var gatilhoErrado = ok with { Playbooks = [ok.Playbooks[0] with { Gatilho = "nao_existe" }] };
        Assert.Contains(gatilhoErrado.Problemas(Fabrica.Regras.Ativos), p => p.Contains("nao_existe"));
        var tipoErrado = ok with { TiposCriticos = ["nave-espacial"] };
        Assert.Contains(tipoErrado.Problemas(Fabrica.Regras.Ativos), p => p.Contains("nave-espacial"));
    }
}

