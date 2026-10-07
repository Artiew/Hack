using Piersec.Core.Pipeline;

namespace Piersec.Tests;

/// <summary>Linhas repetidas contam uma vez só (PROMPT 5.2) e arquivo esperado que não veio vira aviso (RF7).</summary>
public class DuplicidadeEFontesTests
{
    private const string CabecalhoVuln =
        "agent.name,host.os.name,package.name,package.version,vulnerability.id,vulnerability.description,vulnerability.severity,vulnerability.score.base,vulnerability.reference";
    private const string CabecalhoAlertas = "\"timestamp,agent.name,rule.description,data.devname,rule.level,rule.id\"";

    private const string Falha1 = "PC-01,Windows 10,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref";
    private const string Falha2 = "PC-01,Windows 10,java,8.0,CVE-2099-0002,d,High,7.5,ref";

    // ---------- duplicidade ----------

    [Fact]
    public void Linha_repetida_conta_uma_vez_so_e_fica_declarada()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("vuln.csv", CabecalhoVuln, Falha1, Falha2, Falha1, Falha1)]);

        Assert.Equal(2, lote.Eventos.Count);
        var arquivo = lote.Arquivos.Single();
        Assert.Equal(2, arquivo.RegistrosLidos);
        Assert.Equal(2, arquivo.RegistrosRepetidos);
        Assert.Contains(arquivo.Avisos, a => a.StartsWith("2 linhas repetidas foram contadas uma vez só (ex.: a linha"));
        var repetidas = lote.Lacunas.Where(l => l.Motivo == ProcessadorLote.MotivoLinhaRepetida).ToList();
        Assert.Equal(2, repetidas.Count);
        Assert.All(repetidas, l => Assert.NotNull(l.Linha));                       // dá para achar a linha no arquivo
        Assert.True(Fabrica.Validador().Validar(lote).TudoCerto);                   // V02: lidos = eventos continua fechando
    }

    [Fact]
    public void Linhas_parecidas_mas_com_algum_campo_diferente_nao_sao_repetidas()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("alertas.csv", CabecalhoAlertas,
            "\"Oct 7,2026 @ 08:00:00.000,coletor,desc,FW-01,12,100\"",
            "\"Oct 7,2026 @ 08:00:01.000,coletor,desc,FW-01,12,100\"",   // um segundo depois: outro alerta
            "\"Oct 7,2026 @ 08:00:00.000,coletor,desc,FW-02,12,100\"")]); // outro equipamento

        Assert.Equal(3, lote.Eventos.Count);
        Assert.Equal(0, lote.Arquivos.Single().RegistrosRepetidos);
    }

    [Fact]
    public void Repeticao_em_arquivos_diferentes_nao_e_tratada_como_linha_repetida()
    {
        // Mesmo conteúdo em dois arquivos com nomes diferentes = dois ambientes (premissa); o arquivo idêntico já é recusado inteiro.
        var lote = Fabrica.ProcessadorReal().Processar([
            Fabrica.Csv("vuln-a.csv", CabecalhoVuln, Falha1),
            Fabrica.Csv("vuln-b.csv", CabecalhoVuln, Falha1, Falha2)]);

        Assert.Equal(3, lote.Eventos.Count);
    }

    // ---------- fonte esperada ausente ----------

    [Fact]
    public void Lote_sem_arquivo_de_alertas_avisa_que_faltou()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("vuln.csv", CabecalhoVuln, Falha1)]);

        var aviso = Assert.Single(lote.FontesAusentes);
        Assert.Contains("Faltou o arquivo de alertas do firewall", aviso);
        Assert.Contains("NÃO significa que não há problemas", aviso);
        Assert.Contains(lote.Lacunas, l => l.Arquivo == ProcessadorLote.ArquivoDoLote && l.Motivo == aviso);

        var html = Fabrica.Gerador().Gerar(lote with { Validacao = Fabrica.Validador().Validar(lote) });
        Assert.Contains("Faltou o arquivo de alertas do firewall", html);
    }

    [Fact]
    public void Lote_sem_arquivo_de_vulnerabilidades_avisa_que_faltou()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("alertas.csv", CabecalhoAlertas, "\"Oct 7,2026 @ 08:00:00.000,coletor,desc,FW-01,3,100\"")]);

        Assert.Contains("Faltou o arquivo de vulnerabilidades", Assert.Single(lote.FontesAusentes));
    }

    [Fact]
    public void Com_os_dois_tipos_nao_ha_aviso_e_sem_nenhum_arquivo_reconhecido_vale_o_aviso_do_lote()
    {
        var completo = Fabrica.ProcessadorReal().Processar([
            Fabrica.Csv("vuln.csv", CabecalhoVuln, Falha1),
            Fabrica.Csv("alertas.csv", CabecalhoAlertas, "\"Oct 7,2026 @ 08:00:00.000,coletor,desc,FW-01,3,100\"")]);
        Assert.Empty(completo.FontesAusentes);

        var nada = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("outra.csv", "a,b,c", "1,2,3")]);
        Assert.Empty(nada.FontesAusentes);                                          // já existe "nenhum arquivo foi reconhecido"
        Assert.Contains(nada.Lacunas, l => l.Motivo.Contains("nenhum arquivo foi reconhecido"));
    }
}
