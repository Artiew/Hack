using Piersec.Core.Analise;
using Piersec.Core.Coleta;
using Piersec.Core.Modelo;
using Piersec.Core.Relatorio;

namespace Piersec.Tests;

public class CorrelacaoTresFontesTests
{
    private static Lote Exemplo() => Fabrica.ProcessadorTresFontes().Processar([
        Fabrica.Exemplo("tres-fontes-vulnerabilidades-ficticio.csv"),
        Fabrica.Exemplo("tres-fontes-alertas-ficticio.csv"),
        Fabrica.Exemplo("exposicao-internet-ficticio.csv")]);
    private static IReadOnlyList<Correlacao> Correlacionar(IEnumerable<EventoSeguranca> eventos) =>
        new MotorCorrelacao().Correlacionar(eventos.ToList(), Fabrica.Regras.Correlacoes).Correlacoes;
    private static bool TemCadeia(IEnumerable<EventoSeguranca> eventos) => Correlacionar(eventos).Any(c => c.RegraId == "COR-TRES-FONTES");

    [Fact]
    public void Tres_fontes_geram_cadeia_com_evidencia_individual_e_acao()
    {
        var lote = Exemplo();
        var c = Assert.Single(lote.Correlacoes, c => c.RegraId == "COR-TRES-FONTES");
        var provas = lote.Eventos.Where(e => c.EventoIds.Contains(e.Id)).ToList();
        Assert.Equal(3, provas.Select(e => e.Tipo).Distinct().Count());
        Assert.Equal(3, provas.Select(e => e.Evidencia.Arquivo).Distinct().Count());
        Assert.All(provas, e => { Assert.Equal(2, e.Evidencia.Linha); Assert.Equal(64, e.Evidencia.Sha256Arquivo.Length); });
        Assert.Contains("SRV-DEMO-01", c.Ativos);
        Assert.Contains("não prova", c.Justificativa); // coexistência, não prova de invasão
        Assert.Contains("T1190", c.Frameworks.Mitre);
        Assert.Contains("Máquinas expostas", lote.Avaliacao!.Riscos.First().Titulo);
        Assert.Contains(lote.Avaliacao.Acoes, a => a.Tipo == PlanejadorAvaliacao.AcaoInvestigarPicos);
        Assert.Equal(75, lote.Avaliacao.Score.PercentualMedido);
        Assert.True(Fabrica.Validador().Validar(lote).TudoCerto);
    }

    [Theory]
    [InlineData(TiposEvento.Exposicao)]
    [InlineData(TiposEvento.Alerta)]
    [InlineData(TiposEvento.Vulnerabilidade)]
    public void Fonte_ausente_nao_produz_cadeia(string ausente)
    {
        Assert.False(TemCadeia(Exemplo().Eventos.Where(e => e.Tipo != ausente)));
    }

    [Fact]
    public void Coletor_com_nome_igual_ao_ativo_nao_substitui_alvo_explicito()
    {
        var eventos = Exemplo().Eventos.Select(e => e.Tipo == TiposEvento.Alerta ? e with { AtivoAlvo = null, Ativo = "SRV-DEMO-01" } : e);
        Assert.False(TemCadeia(eventos));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("OUTRA-EMPRESA")]
    public void Ambiente_ausente_ou_diferente_nao_produz_cadeia(string? ambiente)
    {
        Assert.False(TemCadeia(Exemplo().Eventos.Select(e => e.Tipo == TiposEvento.Alerta ? e with { AmbienteDeclarado = ambiente } : e)));
    }

    [Fact]
    public void Nomes_de_ativos_diferentes_nao_sao_associados_por_semelhanca()
    {
        Assert.False(TemCadeia(Exemplo().Eventos.Select(e => e.Tipo == TiposEvento.Alerta ? e with { AtivoAlvo = "SRV-DEMO-02" } : e)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void Exposicao_fechada_ou_desconhecida_nao_produz_cadeia(bool? exposto)
    {
        Assert.False(TemCadeia(Exemplo().Eventos.Select(e => e.Tipo == TiposEvento.Exposicao ? e with { ExpostoInternet = exposto } : e)));
    }

    [Fact]
    public void Inventario_contraditorio_nao_e_usado_para_correlar()
    {
        var eventos = Exemplo().Eventos.ToList();
        var exposto = eventos.Single(e => e.Tipo == TiposEvento.Exposicao && e.ExpostoInternet == true);
        eventos.Add(exposto with { Id = "EV-CONTRADITORIO", ExpostoInternet = false });
        Assert.False(TemCadeia(eventos));
        Assert.DoesNotContain(Correlacionar(eventos), c => c.RegraId == "COR-EXPOSTO-E-FALHA");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(25)]
    public void Data_ausente_ou_fora_da_janela_nao_produz_cadeia(int? horas)
    {
        Assert.False(TemCadeia(Exemplo().Eventos.Select(e => e.Tipo == TiposEvento.Alerta
            ? e with { DataUtc = horas is { } h ? Fabrica.Agora.AddHours(h) : null } : e)));
    }

    [Fact]
    public void Limite_inclusivo_da_janela_e_mudanca_de_regra_sao_respeitados()
    {
        var eventos = Exemplo().Eventos.Select(e => e.Tipo == TiposEvento.Alerta ? e with { DataUtc = Fabrica.Agora.AddHours(24) } : e).ToList();
        Assert.True(TemCadeia(eventos));
        var regra = Fabrica.Regra("ativo_exposto_com_vulnerabilidade_e_alerta", new { severidadeMinima = "Alta", severidadeMinimaAlerta = "Media", janelaHoras = 1 });
        Assert.Empty(new MotorCorrelacao().Correlacionar(eventos, [regra]).Correlacoes);
        Assert.Throws<InvalidOperationException>(() => MotorCorrelacao.ValidarParametros(Fabrica.Regra("ativo_exposto_com_vulnerabilidade_e_alerta", new { severidadeMinima = "Alta", severidadeMinimaAlerta = "Media", janelaHoras = 0 })));
    }

    [Fact]
    public void Ordem_dos_registros_nao_muda_os_vinculos()
    {
        var eventos = Exemplo().Eventos;
        var a = Correlacionar(eventos).Where(c => c.RegraId is "COR-TRES-FONTES" or "COR-EXPOSTO-E-FALHA").ToList();
        var b = Correlacionar(eventos.Reverse()).Where(c => c.RegraId is "COR-TRES-FONTES" or "COR-EXPOSTO-E-FALHA").ToList();
        Assert.Equal(a.Select(c => c.Chave), b.Select(c => c.Chave));
        Assert.Equal(a.SelectMany(c => c.EventoIds), b.SelectMany(c => c.EventoIds));
    }

    [Fact]
    public void Campos_explicitos_chegam_do_CSV_sem_trocar_coletor_pelo_alvo()
    {
        var e = Assert.Single(Exemplo().Eventos, e => e.Tipo == TiposEvento.Alerta);
        Assert.Equal("FW-DEMO", e.Ativo);
        Assert.Equal("SRV-DEMO-01", e.AtivoAlvo);
        Assert.Equal("EMPRESA-FICTICIA", e.AmbienteDeclarado);
    }

    [Fact]
    public void Relatorio_completo_mostra_vinculo_e_diretoria_mostra_consequencia_sem_codigo()
    {
        var lote = Exemplo();
        lote = lote with { Validacao = Fabrica.Validador().Validar(lote) };
        var completo = Fabrica.Gerador().Gerar(lote);
        var diretoria = Fabrica.Gerador().Gerar(lote, VersaoRelatorio.Diretoria);
        Assert.Contains("COR-TRES-FONTES", completo);
        Assert.Contains("destination.asset", completo);
        Assert.Contains("Máquinas expostas com falhas graves e sinais de ataque", diretoria);
        Assert.DoesNotContain("destination.asset", diretoria);
        Assert.DoesNotContain("CVE-", diretoria);
        Assert.DoesNotContain("T1190", diretoria);
    }
    [Fact]
    public void Tipo_e_servico_do_mesmo_ativo_sao_unicos_independentemente_da_ordem_das_fontes()
    {
        var vuln = Fabrica.Exemplo("tres-fontes-vulnerabilidades-ficticio.csv");
        var inv = Fabrica.Csv("inventario-sem-so.csv", ExposicaoInternetTests.Cabecalho,
            "SRV-DEMO-01,,true,2026-10-07T09:00:00-03:00,EMPRESA-FICTICIA");
        foreach (var arquivos in new[] { new[] { inv, vuln }, new[] { vuln, inv } })
        {
            var lote = Fabrica.ProcessadorTresFontes().Processar(arquivos);
            var ativo = lote.Eventos.Where(e => e.Ativo == "SRV-DEMO-01").ToList();
            Assert.Single(ativo.Select(e => e.TipoAtivo).Distinct());
            Assert.Single(ativo.Select(e => e.Servico).Distinct());
            Assert.Equal("servidor", ativo.First().TipoAtivo);
            Assert.True(Fabrica.Validador().Validar(lote).TudoCerto);
        }
    }

    [Fact]
    public void Inventario_divergente_em_arquivos_diferentes_nao_produz_cadeia()
    {
        var lote = Exemplo();
        var eventos = lote.Eventos.ToList();
        var exposto = eventos.Single(e => e.Tipo == TiposEvento.Exposicao && e.ExpostoInternet == true);
        eventos.Add(exposto with { Id = "EV-OUTRO", AmbienteDeclarado = "OUTRA-EMPRESA", Evidencia = exposto.Evidencia with { Arquivo = "outro-ficticio.csv" } });
        Assert.False(TemCadeia(eventos));
        var score = new CalculadoraScore(Fabrica.Regras.Score, Fabrica.Regras.Ativos).Calcular(eventos, new HashSet<string> { TiposEvento.Exposicao });
        Assert.Null(score.Fatores.Single(f => f.Id == "exposicao").Valor);
    }

    [Fact]
    public void Metadados_invalidos_nao_sao_guardados_como_identificadores_de_vinculo()
    {
        var marcador = "token=" + Guid.NewGuid().ToString("N");
        var lote = Fabrica.ProcessadorTresFontes().Processar([Fabrica.Csv("alerta-metadados-ficticio.csv",
            "timestamp,agent.name,rule.description,data.devname,rule.level,rule.id,destination.asset,environment.id",
            $"2026-10-07T09:05:00-03:00,COLETOR,Varredura simulada,FW-DEMO,12,99001,<script>,{marcador}")]);
        var evento = Assert.Single(lote.Eventos);
        Assert.Null(evento.AmbienteDeclarado);
        Assert.Null(evento.AtivoAlvo);
        Assert.Contains(lote.Lacunas, l => l.Campo == "environment.id");
        Assert.Contains(lote.Lacunas, l => l.Campo == "destination.asset");
        Assert.DoesNotContain(marcador, Fabrica.Gerador().Gerar(lote));
    }

}
