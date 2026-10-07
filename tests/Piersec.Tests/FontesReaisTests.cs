using System.Text;
using Piersec.Core.Coleta;
using Piersec.Core.Modelo;

namespace Piersec.Tests;

/// <summary>Adaptadores das fontes do desafio, testados com arquivos FICTÍCIOS no mesmo formato.</summary>
public class FontesReaisTests
{
    private const string CabecalhoVuln =
        "agent.name,host.os.name,package.name,package.version,vulnerability.id,vulnerability.description,vulnerability.severity,vulnerability.score.base,vulnerability.reference";

    private const string CabecalhoAlertas = "\"timestamp,agent.name,rule.description,data.devname,rule.level,rule.id\"";

    [Fact]
    public void Vulnerabilidade_vira_evento_com_CVE_pacote_e_nota()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("vuln.csv", CabecalhoVuln,
            "srv-a,SO Fict,pacote-x,1.2,cve-2099-0001,descricao,Critical,7.5,https://exemplo.invalid/x")]);

        var e = Assert.Single(lote.Eventos);
        Assert.Equal(TiposEvento.Vulnerabilidade, e.Tipo);
        Assert.Equal("SRV-A", e.Ativo);
        Assert.Equal("CVE-2099-0001", e.Cve);
        Assert.Equal("pacote-x", e.Pacote);
        Assert.Equal(7.5, e.Cvss);
        Assert.Equal("vuln", e.Ambiente);
        Assert.Equal(Severidade.Alta, e.Severidade); // a nota decide, não o "Critical" da fonte
    }

    [Fact]
    public void Limitacoes_da_fonte_viram_uma_lacuna_por_arquivo_e_nao_uma_por_linha()
    {
        var linhas = Enumerable.Range(1, 30).Select(i => $"srv-{i},SO,p,1,CVE-2099-{i:0000},d,Critical,9.0,ref");
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("vuln.csv", [CabecalhoVuln, .. linhas])]);

        var daFonte = lote.Lacunas.Where(l => l.Campo == "fonte").ToList();
        Assert.Equal(new AdaptadorVulnerabilidades().LimitacoesDaFonte.Count, daFonte.Count);
        Assert.All(daFonte, l => Assert.Null(l.Linha));
    }

    [Fact]
    public void Alertas_embrulhados_com_data_partida_sao_lidos_nas_colunas_certas()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("alertas.csv", CabecalhoAlertas,
            "\"Oct 7,2026 @ 08:49:03.123,coletor,\"\"Bloqueio, com virgula\"\",FW-01,12,99001\"")]);

        var e = Assert.Single(lote.Eventos);
        Assert.Equal("FW-01", e.Ativo);
        Assert.Equal("Bloqueio, com virgula", e.Descricao);
        Assert.Equal(12, e.NivelOrigem);
        Assert.Equal("99001", e.RegraOrigem);
        Assert.Equal(new DateTimeOffset(2026, 10, 7, 11, 49, 3, 123, TimeSpan.Zero), e.DataUtc); // 08:49 em Brasília
        Assert.Equal(Severidade.Alta, e.Severidade);

        var arquivo = Assert.Single(lote.Arquivos);
        Assert.Contains(arquivo.Avisos, a => a.Contains("aspas"));
        Assert.Contains(arquivo.Avisos, a => a.Contains("1 linha(s) corrigida(s)"));
        Assert.Equal(0, arquivo.RegistrosRejeitados);
    }

    [Fact]
    public void Coluna_a_mais_que_nao_e_a_data_nao_e_consertada_a_forca()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("alertas.csv", CabecalhoAlertas,
            "\"Oct 7,2026 @ 08:49:03.123,coletor,desc,FW-01,12,99001,extra\"")]);

        Assert.Empty(lote.Eventos);
        Assert.Equal(1, lote.Arquivos[0].RegistrosRejeitados);
        Assert.Contains(lote.Lacunas, l => l.Linha == 2 && l.Motivo.Contains("8 colunas"));
    }

    [Fact]
    public void Nivel_de_alerta_invalido_vira_lacuna_e_o_alerta_fica_sem_severidade()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("alertas.csv", CabecalhoAlertas,
            "\"Oct 7,2026 @ 08:49:03.123,coletor,desc,FW-01,99,99001\"")]);

        Assert.Null(Assert.Single(lote.Eventos).Severidade);
        Assert.Contains(lote.Lacunas, l => l.Campo == "rule.level");
    }

    [Fact]
    public void Arquivo_com_numero_redondo_de_linhas_avisa_possivel_corte()
    {
        var linhas = Enumerable.Range(1, 1000).Select(i => $"srv-{i % 7},SO,p,1,CVE-2099-{i:0000},d,Critical,9.0,ref");
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("vuln.csv", [CabecalhoVuln, .. linhas])]);

        Assert.Contains(lote.Lacunas, l => l.Motivo.Contains("cortada"));
    }

    [Fact]
    public void Exemplos_ficticios_de_ponta_a_ponta()
    {
        var lote = Fabrica.ProcessadorReal().Processar([
            Fabrica.Exemplo("vulnerabilidades-ficticio.csv"),
            Fabrica.Exemplo("alertas-ficticio.csv"),
            Fabrica.Exemplo("fonte-desconhecida.csv")]);

        Assert.Equal(["Vulnerabilidades (scanner)", "Alertas do SIEM", null], lote.Arquivos.Select(a => a.Fonte));
        Assert.Equal(22, lote.Arquivos[0].RegistrosLidos);     // 22 linhas, nenhuma perdida
        Assert.Equal(25, lote.Arquivos[1].RegistrosLidos);     // 26 linhas: 1 com coluna a mais rejeitada
        Assert.Equal(1, lote.Arquivos[1].RegistrosRejeitados);

        // As regras reais encontram o que os exemplos foram feitos para mostrar
        Assert.Contains(lote.Correlacoes, c => c.RegraId == "COR-VULN-ESPALHADA" && c.Chave == "CVE-2099-0001" && c.Ativos.Count == 12);
        Assert.Contains(lote.Correlacoes, c => c.RegraId == "COR-PACOTE-CONCENTRA" && c.Chave == "pacote-beta");
        Assert.Contains(lote.Correlacoes, c => c.RegraId == "COR-ATIVO-ACUMULA" && c.Chave == "FICT-SRV-01");
        Assert.Contains(lote.Correlacoes, c => c.RegraId == "COR-RAJADA-ALERTAS" && c.EventoIds.Count == 22);
        Assert.Contains(lote.RegrasAvaliadas, r => r.RegraId == "COR-ALERTA-E-FALHA-MESMO-ATIVO" && r.Ocorrencias == 0 && r.MotivoSemOcorrencia!.Contains("0 em comum"));
    }
}
