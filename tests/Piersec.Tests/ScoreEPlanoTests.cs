using Piersec.Core.Analise;
using Piersec.Core.Modelo;
using Piersec.Core.Pipeline;
using Piersec.Core.Relatorio;

namespace Piersec.Tests;

public class ScoreEPlanoTests
{
    private const string CabecalhoVuln =
        "agent.name,host.os.name,package.name,package.version,vulnerability.id,vulnerability.description,vulnerability.severity,vulnerability.score.base,vulnerability.reference";
    private const string CabecalhoAlertas = "\"timestamp,agent.name,rule.description,data.devname,rule.level,rule.id\"";

    private static readonly CalculadoraScore Calculadora = new(Fabrica.Regras.Score, Fabrica.Regras.Ativos);
    private static readonly HashSet<string> SoVulnerabilidades = [TiposEvento.Vulnerabilidade];

    private static string Alerta(int i, int nivel) => $"\"Oct 7,2026 @ 08:{i % 60:00}:00.000,coletor,desc,FW-01,{nivel},{100 + nivel}\"";

    // ---------- sanidade (pedida pelo PROMPT) ----------

    [Fact]
    public void Ambiente_limpo_tem_score_baixo()
    {
        var lote = Fabrica.ProcessadorReal().Processar([
            Fabrica.Csv("vuln.csv", CabecalhoVuln),                                            // scanner rodou e não achou nada
            Fabrica.Csv("alertas.csv", [CabecalhoAlertas, .. Enumerable.Range(0, 10).Select(i => Alerta(i, 3))])]);

        var score = lote.Avaliacao!.Score;
        Assert.Equal(0, score.Valor);
        Assert.Equal("Baixo", score.Faixa);
    }

    [Fact]
    public void Ambiente_caotico_tem_score_critico()
    {
        var vulns = from m in Enumerable.Range(1, 20)
                    from c in Enumerable.Range(1, 15)
                    select $"SRV-{m},Windows Server 2019,pacote-{c % 4},1.0,CVE-2099-{c:0000},d,Critical,9.8,ref";
        var lote = Fabrica.ProcessadorReal().Processar([
            Fabrica.Csv("vuln.csv", [CabecalhoVuln, .. vulns]),
            Fabrica.Csv("alertas.csv", [CabecalhoAlertas, .. Enumerable.Range(0, 30).Select(i => Alerta(i, 15))])]);

        var score = lote.Avaliacao!.Score;
        Assert.True(score.Valor >= 80, $"score {score.Valor}");
        Assert.Equal("Crítico", score.Faixa);
    }

    [Fact]
    public void Fator_sem_dados_fica_nao_medido_e_nao_vira_zero()
    {
        EventoSeguranca[] eventos = [Fabrica.Vulnerabilidade("EV-1", "A", "CVE-1") with { TipoAtivo = "estacao" }]; // 1 crítica = 70

        var score = Calculadora.Calcular(eventos, SoVulnerabilidades);

        Assert.Equal(70, score.Valor);                       // não foi "diluído" pelos fatores sem dados
        Assert.Null(score.Fatores.Single(f => f.Id == "ameacas").Valor);
        Assert.Contains("Não medido", score.Fatores.Single(f => f.Id == "erroHumano").Explicacao);
        Assert.Equal(35, score.PercentualMedido);
        Assert.Equal("baixa", score.Confianca);
    }

    [Fact]
    public void Soma_das_contribuicoes_e_o_score()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")]);
        var score = lote.Avaliacao!.Score;

        Assert.Equal(score.Valor!.Value, score.Fatores.Sum(f => f.Contribuicao), precision: 0);
    }

    [Fact]
    public void Servidor_pesa_mais_que_computador_de_funcionario()
    {
        var servidor = Enumerable.Range(1, 5).Select(i => Fabrica.Vulnerabilidade($"S{i}", "SRV", $"CVE-{i}") with { TipoAtivo = "servidor" }); // 70 + 4×3 = 82
        var estacao = Fabrica.Vulnerabilidade("E1", "PC", "CVE-9") with { TipoAtivo = "estacao" };                                          // 70

        var score = Calculadora.Calcular([.. servidor, estacao], SoVulnerabilidades);

        Assert.Equal(78, score.Valor); // (2×82 + 1×70) / 3
    }

    [Theory]
    [InlineData(Severidade.Critica, 1, 70)]
    [InlineData(Severidade.Critica, 11, 100)] // 70 + 10×3 = 100 (teto)
    [InlineData(Severidade.Alta, 1, 50)]
    [InlineData(Severidade.Baixa, 2, 13)]
    public void Uma_falha_critica_ja_deixa_a_maquina_em_risco_alto(Severidade severidade, int falhas, double esperado)
    {
        var eventos = Enumerable.Range(1, falhas).Select(i => Fabrica.Vulnerabilidade($"EV-{i}", "A", $"CVE-{i}", severidade: severidade));

        Assert.Equal(esperado, Calculadora.PontosDaMaquina(eventos));
    }

    [Fact]
    public void Score_e_coerente_com_os_riscos_quando_todas_as_maquinas_tem_falha_critica()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")]);
        var avaliacao = lote.Avaliacao!;

        Assert.StartsWith("Programas com falhas graves em 100%", avaliacao.Riscos[0].Titulo);
        Assert.True(avaliacao.Score.Fatores.Single(f => f.Id == "vulnerabilidades").Valor >= 70);
        Assert.DoesNotContain(avaliacao.Score.Faixa, new[] { "Baixo", "Moderado" }); // nunca "risco baixo" com 100% das máquinas críticas
    }

    [Fact]
    public void Na_projecao_maquina_corrigida_conta_como_zero_e_nao_some_da_media()
    {
        var b = Enumerable.Range(1, 11).Select(i => Fabrica.Vulnerabilidade($"B{i}", "B", $"CVE-{i}") with { TipoAtivo = "estacao" }).ToList(); // 100
        var populacao = new Dictionary<string, string?> { ["A"] = "estacao", ["B"] = "estacao" };

        var semPopulacao = Calculadora.Calcular(b, SoVulnerabilidades);
        var comPopulacao = Calculadora.Calcular(b, SoVulnerabilidades, populacao);

        Assert.Equal(100, semPopulacao.Valor);
        Assert.Equal(50, comPopulacao.Valor); // A foi corrigida: conta 0, não desaparece
    }

    [Fact]
    public void Sem_nenhum_dado_o_score_nao_e_calculado()
    {
        var score = Calculadora.Calcular([], new HashSet<string>());

        Assert.Null(score.Valor);
        Assert.Contains("NÃO significa", score.FraseFaixa);
    }

    // ---------- tipo de máquina ----------

    [Theory]
    [InlineData("Microsoft Windows Server 2019 Datacenter", "servidor")]
    [InlineData("Microsoft Windows 10 Pro", "estacao")]
    [InlineData("Ubuntu 22.04 LTS", "servidor")]
    [InlineData("", "desconhecido")]
    [InlineData("SistemaInventado 1.0", "desconhecido")]
    public void Tipo_da_maquina_vem_do_sistema_operacional(string sistema, string esperado)
    {
        var evento = Fabrica.Vulnerabilidade("EV-1", "A", "CVE-1") with { SistemaOperacional = sistema };

        Assert.Equal(esperado, new ClassificadorAtivos(Fabrica.Regras.Ativos).Classificar(evento));
    }

    [Fact]
    public void Alerta_e_de_equipamento_de_rede()
    {
        var alerta = Fabrica.Alerta("EV-1", "FW", "1", Fabrica.Agora);

        Assert.Equal("rede", new ClassificadorAtivos(Fabrica.Regras.Ativos).Classificar(alerta));
    }

    // ---------- plano de ação ----------

    [Fact]
    public void Plano_calcula_quanto_cada_acao_elimina()
    {
        // 10 falhas graves: pacote A 5, B 3, C 1 (máquinas M1..M3), D 1 (M9)
        var eventos = new List<EventoSeguranca>();
        var n = 0;
        foreach (var (pacote, quantas, maquina) in new[] { ("A", 5, "M1"), ("B", 3, "M2"), ("C", 1, "M3"), ("D", 1, "M9") })
            for (var i = 0; i < quantas; i++)
                eventos.Add(Fabrica.Vulnerabilidade($"EV-{++n:00}", maquina, $"CVE-{pacote}{i}", pacote) with { TipoAtivo = "estacao" });

        var avaliacao = new PlanejadorAvaliacao(Fabrica.Regras, Calculadora).Avaliar(eventos, [], SoVulnerabilidades);

        var programas = avaliacao.Acoes.Single(a => a.Tipo == PlanejadorAvaliacao.AcaoAtualizarProgramas);
        Assert.Equal(90, programas.PercentualFalhasGraves); // A + B + C = 9 de 10
        Assert.Contains("A, B e C", programas.Titulo);
        var maquinas = avaliacao.Acoes.Single(a => a.Tipo == PlanejadorAvaliacao.AcaoCorrigirMaquinas);
        Assert.Equal(10, maquinas.PercentualFalhasGraves);  // sobra a D, na M9
        Assert.Contains("90%", avaliacao.FraseUmaCoisa);
        Assert.Contains("100%", avaliacao.FraseUmaCoisa);
        Assert.True(avaliacao.ScorePrevisto < avaliacao.Score.Valor);
    }

    [Theory]
    [InlineData(Nivel.Alto, Nivel.Baixo, "Fazer agora")]
    [InlineData(Nivel.Alto, Nivel.Medio, "Fazer agora")]
    [InlineData(Nivel.Alto, Nivel.Alto, "Planejar")]
    [InlineData(Nivel.Medio, Nivel.Baixo, "Planejar")]
    [InlineData(Nivel.Baixo, Nivel.Baixo, "Se sobrar tempo")]
    [InlineData(Nivel.Baixo, Nivel.Alto, "Não priorizar agora")]
    public void Matriz_impacto_por_esforco(Nivel impacto, Nivel esforco, string esperado)
    {
        Assert.Equal(esperado, PlanejadorAvaliacao.Quadrante(impacto, esforco));
    }

    [Fact]
    public void Riscos_principais_dizem_o_que_e_por_que_importa_e_como_resolver()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")]);
        var riscos = lote.Avaliacao!.Riscos;

        Assert.Equal(3, riscos.Count);
        Assert.StartsWith("Programas com falhas graves", riscos[0].Titulo);
        Assert.All(riscos, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.OQueE));
            Assert.False(string.IsNullOrWhiteSpace(r.PorQueImporta));
            Assert.Matches("Aç(ão|ões) [0-9]", r.ComoResolver);
        });
    }

    [Fact]
    public void Acoes_tem_ordem_sem_buracos_e_fazer_agora_vem_primeiro()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")]);
        var acoes = lote.Avaliacao!.Acoes;

        Assert.Equal(Enumerable.Range(1, acoes.Count), acoes.Select(a => a.Ordem));
        Assert.Equal("Fazer agora", acoes[0].Quadrante);
    }

    // ---------- tendência ----------

    [Fact]
    public void Comparacao_com_o_lote_anterior_diz_quanto_mudou_e_por_que()
    {
        var atual = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")]);
        var fatores = atual.Avaliacao!.Score.Fatores.ToDictionary(f => f.Id, f => f.Valor);
        // Antes: só o fator de falhas era 40 pontos pior; os outros iguais → ele é o motivo
        var antes = new Dictionary<string, double?>(fatores) { ["vulnerabilidades"] = fatores["vulnerabilidades"] + 40 };
        var anterior = new ResumoLote("anterior", Fabrica.Agora.AddDays(-7), 1, 1, 0, 0, Score: 90, Fatores: antes);

        var c = CicloAnalise.Comparar(atual, anterior)!;

        Assert.Equal(Math.Round(atual.Avaliacao.Score.Valor!.Value - 90, 1), c.Diferenca);
        Assert.Contains("Falhas nos programas", c.Motivo);
        Assert.Contains("foi de", c.Motivo);
    }

    [Fact]
    public void Primeiro_lote_nao_tem_comparacao()
    {
        var atual = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv")]);

        Assert.Null(CicloAnalise.Comparar(atual, null));
    }

    // ---------- relatório para leigo ----------

    [Fact]
    public void Primeira_pagina_responde_as_tres_perguntas_sem_codigos_tecnicos()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")]);
        var html = Fabrica.Gerador().Gerar(lote);

        var inicio = html.IndexOf("Resumo para a diretoria", StringComparison.Ordinal);
        var fim = html.IndexOf("2. Serviços da empresa", StringComparison.Ordinal);
        Assert.True(inicio > 0 && fim > inicio);
        var pagina1 = html[inicio..fim];

        Assert.Contains("COMO ESTAMOS", pagina1);
        Assert.Contains("O que é mais grave", pagina1);
        Assert.Contains("O que fazer agora", pagina1);
        Assert.Contains("SE FIZER SÓ UMA COISA", pagina1);
        Assert.Contains("100 = risco máximo", pagina1);   // direção escrita no rótulo
        Assert.DoesNotContain("CVE-", pagina1);           // nada de código técnico na página da diretoria
        Assert.DoesNotContain("CVSS", pagina1);
        Assert.Contains("G. Glossário", html);
    }

    // ---------- "3 achados explicam X% do risco" ----------

    private static Lote LoteVuln(params string[] linhas) =>
        Fabrica.ProcessadorReal().Processar([Fabrica.Csv("vuln.csv", [CabecalhoVuln, .. linhas])]);

    [Fact]
    public void Um_unico_achado_explica_todo_o_risco_e_nao_aparece_duas_vezes()
    {
        // A máquina e o programa são as MESMAS falhas: um achado só, não dois.
        var a = LoteVuln("PC-01,Windows 10,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref").Avaliacao!;

        var achado = Assert.Single(a.AchadosPrincipais);
        Assert.Equal(100, a.PercentualExplicado);
        Assert.Equal(0, a.ScoreSemAchados);
        Assert.Equal(a.Score.Valor, achado.Pontos);
        Assert.Equal("Atualizar o programa openssl, que está em 1 máquina", achado.Texto);   // empate: a ação mais simples vence
        Assert.Equal("Um problema responde por todo o risco. Resolvendo só ele, o risco cairia de alto para baixo (de 70 para 0).",
            Fabrica.Gerador().FraseAchados(a));
    }

    [Fact]
    public void Achados_principais_sao_os_que_mais_derrubam_o_score_e_o_percentual_e_recalculado()
    {
        var a = LoteVuln([
            .. Enumerable.Range(1, 4).Select(i => $"SRV-{i},Windows Server 2019,openssl,1.0,CVE-2099-000{i},d,Critical,9.8,ref"),
            .. Enumerable.Range(1, 3).Select(i => $"PC-{i},Windows 10,java,8.0,CVE-2099-010{i},d,High,7.5,ref"),
            .. Enumerable.Range(1, 6).Select(i => $"PC-{i},Windows 10,zoom,5.0,CVE-2099-020{i},d,Low,2.0,ref")]).Avaliacao!;

        Assert.InRange(a.AchadosPrincipais.Count, 1, PlanejadorAvaliacao.QuantidadeAchadosPrincipais);
        Assert.Contains("openssl", a.AchadosPrincipais[0].Descricao);                 // servidores com falha crítica pesam mais
        Assert.Equal(a.AchadosPrincipais.Select(x => x.Pontos).OrderDescending(), a.AchadosPrincipais.Select(x => x.Pontos));
        Assert.InRange(a.PercentualExplicado!.Value, 0.1, 100);
        // o percentual e o "score sem eles" contam a mesma história
        Assert.Equal(a.Score.Valor!.Value * (1 - a.PercentualExplicado.Value / 100), a.ScoreSemAchados!.Value, 0.2);
        Assert.Matches("^(Dois|Três) problemas respondem por", Fabrica.Gerador().FraseAchados(a));
        Assert.All(a.AchadosPrincipais, x => Assert.DoesNotContain("regra", x.Texto));   // nada de número de regra para o executivo
    }

    [Fact]
    public void Quando_o_risco_esta_espalhado_a_frase_diz_isso_e_nao_promete_atalho()
    {
        // 20 máquinas iguais, cada uma com 6 falhas críticas em programas só dela: nenhum achado resolve muito.
        var a = LoteVuln([.. from m in Enumerable.Range(1, 20)
                             from p in Enumerable.Range(1, 6)
                             select $"PC-{m:00},Windows 10,prog-{m}-{p},1.0,CVE-2099-{m:00}{p},d,Critical,9.8,ref"]).Avaliacao!;

        Assert.False(GeradorRelatorioHtml.RiscoConcentrado(a));
        Assert.InRange(a.PercentualExplicado!.Value, 1, GeradorRelatorioHtml.PercentualRiscoConcentrado - 1);
        var frase = Fabrica.Gerador().FraseAchados(a)!;
        Assert.Equal("Não existe um problema único para atacar. Mesmo resolvendo os 3 maiores, o risco cairia pouco: de crítico para alto (de 85 para 72). " +
                     "O caminho é atualizar todas as máquinas de forma automática, e não corrigir uma por uma.", frase);
    }

    [Fact]
    public void Ambiente_limpo_nao_tem_achados_principais()
    {
        var a = LoteVuln().Avaliacao!;

        Assert.Empty(a.AchadosPrincipais);
        Assert.Null(a.PercentualExplicado);
        Assert.Null(Fabrica.Gerador().FraseAchados(a));
    }

    [Fact]
    public void Relatorio_mostra_os_achados_principais_na_secao_do_score()
    {
        var lote = LoteVuln("PC-01,Windows 10,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref");

        var html = Fabrica.Gerador().Gerar(lote with { Validacao = Fabrica.Validador().Validar(lote) });

        Assert.Contains("Poucos problemas concentram boa parte do risco", html);
        Assert.Contains("Um problema responde por todo o risco", html);
        Assert.Contains("Atualizar o programa openssl, que está em 1 máquina", html);
        Assert.Contains("Detalhe técnico: falhas no programa openssl", html);   // o técnico continua, em letra pequena
        Assert.Contains("tira 70 pontos do score", html);
    }
}
