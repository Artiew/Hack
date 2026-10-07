using Piersec.Core.Analise;
using Piersec.Core.Coleta;
using Piersec.Core.Modelo;

namespace Piersec.Tests;

public class CorrelacaoTests
{
    private static readonly MotorCorrelacao Motor = new();
    private static readonly DateTimeOffset T0 = new(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Mesma_falha_em_varias_maquinas_e_correlacionada()
    {
        EventoSeguranca[] eventos =
        [
            Fabrica.Vulnerabilidade("EV-1", "A", "CVE-1"),
            Fabrica.Vulnerabilidade("EV-2", "B", "CVE-1"),
            Fabrica.Vulnerabilidade("EV-3", "C", "CVE-1"),
            Fabrica.Vulnerabilidade("EV-4", "A", "CVE-2"),
        ];
        var regra = Fabrica.Regra("mesma_vulnerabilidade_em_varios_ativos", new { minimoAtivos = 3, severidadeMinima = "Alta" });

        var (correlacoes, avaliadas) = Motor.Correlacionar(eventos, [regra]);

        var c = Assert.Single(correlacoes);
        Assert.Equal("CVE-1", c.Chave);
        Assert.Equal(["A", "B", "C"], c.Ativos);
        Assert.Equal(["EV-1", "EV-2", "EV-3"], c.EventoIds);
        Assert.Equal(1, avaliadas[0].Ocorrencias);
    }

    [Fact]
    public void Severidade_abaixo_do_minimo_nao_entra_na_regra()
    {
        EventoSeguranca[] eventos =
        [
            Fabrica.Vulnerabilidade("EV-1", "A", "CVE-1", severidade: Severidade.Media),
            Fabrica.Vulnerabilidade("EV-2", "B", "CVE-1", severidade: Severidade.Media),
        ];
        var regra = Fabrica.Regra("mesma_vulnerabilidade_em_varios_ativos", new { minimoAtivos = 2, severidadeMinima = "Alta" });

        var (correlacoes, avaliadas) = Motor.Correlacionar(eventos, [regra]);

        Assert.Empty(correlacoes);
        Assert.Contains("nenhuma vulnerabilidade de severidade Alta", avaliadas[0].MotivoSemOcorrencia);
    }

    [Fact]
    public void Pacote_com_muitas_falhas_e_correlacionado_ignorando_maiusculas()
    {
        EventoSeguranca[] eventos =
        [
            Fabrica.Vulnerabilidade("EV-1", "A", "CVE-1", pacote: "OpenThing"),
            Fabrica.Vulnerabilidade("EV-2", "A", "CVE-2", pacote: "openthing"),
            Fabrica.Vulnerabilidade("EV-3", "B", "CVE-3", pacote: "openthing"),
            Fabrica.Vulnerabilidade("EV-4", "B", "CVE-4", pacote: "outro"),
        ];
        var regra = Fabrica.Regra("pacote_concentra_vulnerabilidades", new { minimoCves = 3, severidadeMinima = "Alta" });

        var c = Assert.Single(Motor.Correlacionar(eventos, [regra]).Correlacoes);
        Assert.Equal("openthing", c.Chave);
        Assert.Equal(2, c.Ativos.Count);
    }

    [Fact]
    public void Maquina_que_acumula_falhas_e_correlacionada_e_ativo_desconhecido_fica_de_fora()
    {
        EventoSeguranca[] eventos =
        [
            Fabrica.Vulnerabilidade("EV-1", "A", "CVE-1"),
            Fabrica.Vulnerabilidade("EV-2", "A", "CVE-2"),
            Fabrica.Vulnerabilidade("EV-3", ContextoConversao.NaoInformado, "CVE-3"),
            Fabrica.Vulnerabilidade("EV-4", ContextoConversao.NaoInformado, "CVE-4"),
        ];
        var regra = Fabrica.Regra("ativo_acumula_vulnerabilidades", new { minimoFalhas = 2, severidadeMinima = "Critica" }, "Critica");

        var c = Assert.Single(Motor.Correlacionar(eventos, [regra]).Correlacoes);
        Assert.Equal("A", c.Chave);
        Assert.Equal(Severidade.Critica, c.Severidade);
    }

    [Fact]
    public void Rajada_dentro_da_janela_e_correlacionada()
    {
        var eventos = Enumerable.Range(0, 5).Select(i => Fabrica.Alerta($"EV-{i}", "FW", "100", T0.AddMinutes(i * 2))).ToList();
        var regra = Fabrica.Regra("rajada_de_alertas", new { minimoAlertas = 5, janelaMinutos = 10 });

        var c = Assert.Single(Motor.Correlacionar(eventos, [regra]).Correlacoes);
        Assert.Equal(5, c.EventoIds.Count);
    }

    [Fact]
    public void Alertas_espalhados_no_tempo_nao_sao_rajada()
    {
        var eventos = Enumerable.Range(0, 5).Select(i => Fabrica.Alerta($"EV-{i}", "FW", "100", T0.AddMinutes(i * 6))).ToList();
        var regra = Fabrica.Regra("rajada_de_alertas", new { minimoAlertas = 5, janelaMinutos = 10 });

        var (correlacoes, avaliadas) = Motor.Correlacionar(eventos, [regra]);

        Assert.Empty(correlacoes);
        Assert.Contains("o máximo foi 2", avaliadas[0].MotivoSemOcorrencia);
    }

    [Fact]
    public void Sem_maquina_em_comum_entre_fontes_explica_o_motivo()
    {
        EventoSeguranca[] eventos = [Fabrica.Vulnerabilidade("EV-1", "SRV", "CVE-1"), Fabrica.Alerta("EV-2", "FW", "100", T0)];
        var regra = Fabrica.Regra("alerta_e_vulnerabilidade_no_mesmo_ativo", new { severidadeMinima = "Alta" }, "Critica");

        var (correlacoes, avaliadas) = Motor.Correlacionar(eventos, [regra]);

        Assert.Empty(correlacoes);
        Assert.Contains("0 em comum", avaliadas[0].MotivoSemOcorrencia);
    }

    [Fact]
    public void Maquina_com_falha_e_alerta_e_correlacionada()
    {
        EventoSeguranca[] eventos = [Fabrica.Vulnerabilidade("EV-1", "SRV", "CVE-1"), Fabrica.Alerta("EV-2", "SRV", "100", T0)];
        var regra = Fabrica.Regra("alerta_e_vulnerabilidade_no_mesmo_ativo", new { severidadeMinima = "Alta" }, "Critica");

        var c = Assert.Single(Motor.Correlacionar(eventos, [regra]).Correlacoes);
        Assert.Equal(["EV-1", "EV-2"], c.EventoIds);
    }

    [Fact]
    public void Mesma_entrada_gera_exatamente_a_mesma_saida()
    {
        var lote1 = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")]);
        var lote2 = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")]);

        Assert.NotEmpty(lote1.Correlacoes);

        Assert.Equal(
            lote1.Correlacoes.Select(c => (c.Id, c.RegraId, c.Chave, c.Titulo, string.Join(",", c.EventoIds))),
            lote2.Correlacoes.Select(c => (c.Id, c.RegraId, c.Chave, c.Titulo, string.Join(",", c.EventoIds))));
    }

    [Fact]
    public void Textos_da_regra_sao_preenchidos_com_os_dados()
    {
        var regra = Fabrica.Regra("mesma_vulnerabilidade_em_varios_ativos", new { minimoAtivos = 2, severidadeMinima = "Alta" }) with
        {
            Titulo = "{cve} está em {ativos} máquinas",
            Justificativa = "nota {cvss}",
        };
        EventoSeguranca[] eventos = [Fabrica.Vulnerabilidade("EV-1", "A", "CVE-9", cvss: 9.1), Fabrica.Vulnerabilidade("EV-2", "B", "CVE-9", cvss: 9.8)];

        var c = Assert.Single(Motor.Correlacionar(eventos, [regra]).Correlacoes);
        Assert.Equal("CVE-9 está em 2 máquinas", c.Titulo);
        Assert.Equal("nota 9,8", c.Justificativa);
    }
}
