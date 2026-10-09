using System.Text.Json;
using System.Text.Json.Serialization;
using Piersec.Core.Modelo;
using Piersec.Core.Pipeline;

namespace Piersec.Tests;

public class ComparacaoCoberturaTests
{
    private static Lote OperacaoNormal() => Fabrica.ProcessadorReal().Processar(
        [Fabrica.Exemplo("operacao-normal-ficticio.csv")]);

    private static ResumoLote Anterior(Lote atual, double score, Dictionary<string, double?>? fatores) =>
        new("anterior", atual.CriadoEmUtc.AddDays(-1), 1, 12, 0, 0, Score: score, Fatores: fatores);

    [Fact]
    public void Retirar_scanner_nao_vira_melhora_de_risco_no_relatorio()
    {
        var atual = OperacaoNormal();
        var antes = atual.Avaliacao!.Score.Fatores.ToDictionary(f => f.Id, f => f.Valor);
        antes["vulnerabilidades"] = 92;
        antes["ameacas"] = null;
        var comparacao = CicloAnalise.Comparar(atual, Anterior(atual, 92, antes))!;

        Assert.False(comparacao.MesmosFatoresMedidos);
        Assert.Equal(-92, comparacao.Diferenca);
        var html = Fabrica.Gerador().Gerar(atual with { Comparacao = comparacao });
        Assert.Contains("Comparação direta indisponível", html);
        Assert.Contains("não comparável", html);
        Assert.DoesNotContain("Melhorou:", html);
    }

    [Fact]
    public void Mesmos_fatores_permitem_mostrar_a_variacao()
    {
        var atual = OperacaoNormal();
        var antes = atual.Avaliacao!.Score.Fatores.ToDictionary(f => f.Id, f => f.Valor);
        antes["ameacas"] = 50;
        var comparacao = CicloAnalise.Comparar(atual, Anterior(atual, 50, antes))!;

        Assert.True(comparacao.MesmosFatoresMedidos);
        Assert.Equal(-50, comparacao.Diferenca);
        Assert.Contains("Melhorou:", Fabrica.Gerador().Gerar(atual with { Comparacao = comparacao }));
    }

    [Fact]
    public void Score_igual_nao_torna_fontes_diferentes_comparaveis()
    {
        var atual = OperacaoNormal();
        var antes = atual.Avaliacao!.Score.Fatores.ToDictionary(f => f.Id, f => f.Valor);
        antes["vulnerabilidades"] = 0;
        antes["ameacas"] = null;
        var comparacao = CicloAnalise.Comparar(atual, Anterior(atual, 0, antes))!;

        Assert.False(comparacao.MesmosFatoresMedidos);
        Assert.Equal(0, comparacao.Diferenca);
    }

    [Fact]
    public void Historico_sem_fatores_ou_sem_campo_novo_nao_confirma_cobertura()
    {
        var atual = OperacaoNormal();
        var comparacao = CicloAnalise.Comparar(atual, Anterior(atual, 92, null))!;
        var jsonLegado = JsonSerializer.Serialize(comparacao,
            new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        Assert.DoesNotContain("MesmosFatoresMedidos", jsonLegado);
        var legado = JsonSerializer.Deserialize<ComparacaoLote>(jsonLegado)!;

        Assert.Null(legado.MesmosFatoresMedidos);
        var html = Fabrica.Gerador().Gerar(atual with { Comparacao = legado });
        Assert.Contains("Comparação direta indisponível", html);
        Assert.DoesNotContain("Melhorou:", html);
    }

    [Fact]
    public void Operacao_normal_identifica_custo_de_ampliar_cobertura()
    {
        var atual = OperacaoNormal();
        Assert.Equal(0, atual.Avaliacao!.Score.Valor);
        var html = Fabrica.Gerador().Gerar(atual);
        Assert.Contains("Quanto custa ampliar a análise", html);
        Assert.DoesNotContain("Quanto custa corrigir", html);
    }
}
