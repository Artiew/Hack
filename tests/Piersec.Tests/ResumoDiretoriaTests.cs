using Piersec.Core.Modelo;
using Piersec.Core.Relatorio;

namespace Piersec.Tests;

public class ResumoDiretoriaTests
{
    [Fact]
    public void Resumo_nao_esconde_reprovacao_nem_mostra_detalhe_tecnico_da_falha()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("alertas-ficticio.csv")]);
        lote = lote with { Validacao = new ResultadoValidacao([new Verificacao("V99", "detalhe-interno", false, "CVE-2099-9999")]) };
        var html = Fabrica.Gerador().Gerar(lote, VersaoRelatorio.Diretoria);
        Assert.Contains("RELATÓRIO NÃO VALIDADO", html);
        Assert.Contains("Não use estes números para decidir", html);
        Assert.DoesNotContain("CVE-2099-9999", html);
        Assert.DoesNotContain("detalhe-interno", html);
    }

    [Fact]
    public void Resumo_de_score_zero_com_cobertura_parcial_nao_afirma_seguranca_completa()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("operacao-normal-ficticio.csv")]);
        var html = Fabrica.Gerador().Gerar(lote, VersaoRelatorio.Diretoria);
        Assert.Contains("cobertura 20%", html);
        Assert.Contains("não comprova segurança completa", html);
        Assert.Contains("Custo para ampliar a análise", html);
        Assert.Contains("Completar a análise de segurança", html);
        Assert.DoesNotContain("CVE-", html);
    }

    [Fact]
    public void Resumo_nao_transforma_mudanca_de_cobertura_em_melhora()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("alertas-ficticio.csv")]);
        lote = lote with { Comparacao = new ComparacaoLote("anterior", Fabrica.Agora, 90, -80, "detalhe técnico") { MesmosFatoresMedidos = false } };
        var html = Fabrica.Gerador().Gerar(lote, VersaoRelatorio.Diretoria);
        Assert.Contains("Comparação indisponível", html);
        Assert.DoesNotContain("Melhorou", html);
        Assert.DoesNotContain("detalhe técnico", html);
    }
}
