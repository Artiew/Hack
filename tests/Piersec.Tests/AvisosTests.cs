using Piersec.Core.Pipeline;

namespace Piersec.Tests;

public class AvisosTests
{
    private const string CabecalhoVuln =
        "agent.name,host.os.name,package.name,package.version,vulnerability.id,vulnerability.description,vulnerability.severity,vulnerability.score.base,vulnerability.reference";

    private static ArquivoEnviado Vuln(string nome, params string[] maquinas) =>
        Fabrica.Csv(nome, [CabecalhoVuln, .. maquinas.Select((m, i) => $"{m},Windows 10,p,1,CVE-2099-{i:0000},d,Critical,9.8,ref")]);

    // ---------- ambientes possivelmente diferentes ----------

    [Fact]
    public void Dois_arquivos_do_mesmo_tipo_sem_maquina_em_comum_geram_aviso()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Vuln("empresa-a.csv", "A1", "A2", "A3"), Vuln("empresa-b.csv", "B1", "B2", "B3")]);

        var aviso = Assert.Single(lote.AvisosAmbiente);
        Assert.Contains("empresa-a.csv", aviso);
        Assert.Contains("empresa-b.csv", aviso);
        Assert.Contains("lote separado", aviso);
        Assert.Contains(lote.Lacunas, l => l.Campo == "ambiente");
    }

    [Fact]
    public void Com_maquina_em_comum_nao_ha_aviso()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Vuln("parte-1.csv", "A1", "A2", "A3"), Vuln("parte-2.csv", "A3", "B2", "B3")]);

        Assert.Empty(lote.AvisosAmbiente);
    }

    [Fact]
    public void Tipos_diferentes_sem_maquina_em_comum_nao_geram_este_aviso()
    {
        // vulnerabilidades (máquinas) × alertas (firewall): máquinas diferentes são esperadas
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")]);

        Assert.Empty(lote.AvisosAmbiente);
    }

    [Fact]
    public void Arquivos_com_poucas_maquinas_nao_sao_comparados()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Vuln("a.csv", "A1", "A2"), Vuln("b.csv", "B1", "B2")]);

        Assert.Empty(lote.AvisosAmbiente);
    }

    [Fact]
    public void Aviso_de_ambiente_aparece_na_primeira_pagina()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Vuln("empresa-a.csv", "A1", "A2", "A3"), Vuln("empresa-b.csv", "B1", "B2", "B3")]);
        var html = Fabrica.Gerador().Gerar(lote);

        var pagina1 = html[html.IndexOf("Resumo para a diretoria", StringComparison.Ordinal)..html.IndexOf("2. Serviços da empresa", StringComparison.Ordinal)];
        Assert.Contains("empresas ou ambientes diferentes", pagina1);
    }

    // ---------- confiança baixa junto do número ----------

    [Fact]
    public void Confianca_baixa_com_score_baixo_avisa_que_nao_da_para_afirmar_que_esta_seguro()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("alertas-ficticio.csv")]); // só 20% medido, score baixo
        var html = Fabrica.Gerador().Gerar(lote);

        Assert.Equal("baixa", lote.Avaliacao!.Score.Confianca);
        var pagina1 = html[html.IndexOf("Resumo para a diretoria", StringComparison.Ordinal)..html.IndexOf("O que é mais grave", StringComparison.Ordinal)];
        Assert.Contains("só 1 de 5 fatores pôde ser avaliado (20%)", pagina1);
        Assert.Contains("Faltam dados de: falhas nos programas, exposição à internet, erro humano e detecção e resposta", pagina1);
        Assert.Contains("Não dá para afirmar que o ambiente está seguro", pagina1);
    }

    [Fact]
    public void Confianca_baixa_com_score_alto_avisa_que_o_risco_real_pode_variar()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv")]); // 35% medido, score alto

        var aviso = Fabrica.Gerador().AvisoConfianca(lote.Avaliacao!.Score);
        Assert.Contains("pode ser maior ou menor", aviso);
        Assert.Contains("só 1 de 5 fatores pôde ser avaliado", aviso);
    }

    [Fact]
    public void Confianca_media_nao_mostra_o_aviso()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")]);

        Assert.Null(Fabrica.Gerador().AvisoConfianca(lote.Avaliacao!.Score));
        Assert.DoesNotContain("fatores puderam ser avaliados", Fabrica.Gerador().Gerar(lote));
    }

    [Fact]
    public void Relatorio_explica_por_que_mais_fontes_podem_mudar_o_score()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv")]);

        Assert.Contains("Por que o score pode mudar quando chegam mais fontes", Fabrica.Gerador().Gerar(lote));
    }
}
