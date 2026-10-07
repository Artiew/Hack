using System.Text;
using Piersec.Core.Modelo;
using Piersec.Core.Pipeline;

namespace Piersec.Tests;

public class ProcessadorLoteTests
{
    private const string LinhaValida = "1,2026-09-01 10:00,SRV-01,ana,Alta,firewall,teste";

    [Fact]
    public void Extensao_que_nao_e_csv_e_recusada_com_motivo()
    {
        var lote = Fabrica.Processador().Processar([Fabrica.CsvExemplo("dados.txt", LinhaValida)]);

        var arquivo = Assert.Single(lote.Arquivos);
        Assert.Null(arquivo.Fonte);
        Assert.Contains("extensão", arquivo.MotivoNaoProcessado);
        Assert.Empty(lote.Eventos);
    }

    [Fact]
    public void Cabecalho_desconhecido_informa_o_motivo_e_vira_lacuna()
    {
        var lote = Fabrica.Processador().Processar([Fabrica.Csv("outra.csv", "coluna_a,coluna_b", "x,y")]);

        Assert.Contains("nenhuma fonte conhecida", lote.Arquivos[0].MotivoNaoProcessado);
        Assert.Contains(lote.Lacunas, l => l.Arquivo == "outra.csv");
    }

    [Fact]
    public void Arquivo_repetido_no_mesmo_lote_nao_conta_duas_vezes()
    {
        var a = Fabrica.CsvExemplo("a.csv", LinhaValida);
        var copia = Fabrica.CsvExemplo("copia.csv", LinhaValida);

        var lote = Fabrica.Processador().Processar([a, copia]);

        Assert.Single(lote.Eventos);
        Assert.Contains("idêntico", lote.Arquivos[1].MotivoNaoProcessado);
    }

    [Fact]
    public void Calcula_o_SHA256_correto()
    {
        var lote = Fabrica.Processador().Processar([ArquivoEnviado.De("x.csv", Encoding.ASCII.GetBytes("abc"))]);

        // Valor oficial de SHA-256("abc") publicado no padrão FIPS 180-2.
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", lote.Arquivos[0].Sha256);
    }

    [Fact]
    public void Soma_por_severidade_mais_sem_severidade_e_igual_ao_total()
    {
        var lote = Fabrica.Processador().Processar([Fabrica.CsvExemplo("a.csv",
            "1,2026-09-01 10:00,SRV-01,ana,Crítica,web,x",
            "2,2026-09-01 10:00,SRV-02,ana,Alta,web,x",
            "3,2026-09-01 10:00,SRV-03,ana,Média,web,x",
            "4,2026-09-01 10:00,SRV-04,ana,Baixa,web,x",
            "5,2026-09-01 10:00,SRV-05,ana,???,web,x")]);

        var soma = SeveridadeExtensoes.DoMaisGrave.Sum(s => lote.ContarPorSeveridade(s)) + lote.ContarPorSeveridade(null);
        Assert.Equal(5, lote.Eventos.Count);
        Assert.Equal(lote.Eventos.Count, soma);
    }

    [Fact]
    public void Arquivo_grande_demais_e_recusado_sem_ser_lido()
    {
        var grande = new ArquivoEnviado("grande.csv", ProcessadorLote.TamanhoMaximoBytes + 1, null);

        var lote = Fabrica.Processador().Processar([grande]);

        Assert.Contains("maior que o limite", lote.Arquivos[0].MotivoNaoProcessado);
        Assert.Null(lote.Arquivos[0].Sha256);
    }

    [Fact]
    public void Lote_sem_arquivo_reconhecido_declara_que_nao_ha_dados()
    {
        var lote = Fabrica.Processador().Processar([Fabrica.Csv("outra.csv", "coluna_a", "x")]);

        Assert.Contains(lote.Lacunas, l => l.Arquivo == ProcessadorLote.ArquivoDoLote && l.Motivo.Contains("NÃO significa"));
    }

    [Fact]
    public void Ids_sao_unicos_no_lote_mesmo_com_varios_arquivos()
    {
        var lote = Fabrica.Processador().Processar([
            Fabrica.CsvExemplo("a.csv", LinhaValida),
            Fabrica.CsvExemplo("b.csv", "9,2026-09-02 10:00,SRV-09,bia,Baixa,web,y")]);

        Assert.Equal(2, lote.Eventos.Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public void Arquivo_em_Windows1252_mantem_aspas_curvas_e_acentos()
    {
        // "Ação “teste” – ok" em Windows-1252: ç=E7, ã=E3, “=93, ”=94, –=96 (os 3 últimos não existem em Latin-1)
        byte[] descricao = [0x41, 0xE7, 0xE3, 0x6F, 0x20, 0x93, 0x74, 0x65, 0x73, 0x74, 0x65, 0x94, 0x20, 0x96, 0x20, 0x6F, 0x6B];
        byte[] conteudo = [.. Encoding.ASCII.GetBytes(Fabrica.CabecalhoExemplo + "\n1,2026-09-01 10:00,SRV-01,ana,Alta,web,"), .. descricao];

        var lote = Fabrica.Processador().Processar([ArquivoEnviado.De("win.csv", conteudo)]);

        Assert.Equal("Ação “teste” – ok", Assert.Single(lote.Eventos).Descricao);
        Assert.Contains(lote.Lacunas, l => l.Motivo.Contains("Windows-1252"));
    }

    [Theory]
    [InlineData(@"..\..\segredo.csv", "segredo.csv")]
    [InlineData("../../etc/dados.csv", "dados.csv")]
    [InlineData("   ", "(sem nome)")]
    public void Nome_do_arquivo_perde_qualquer_caminho(string recebido, string esperado)
    {
        Assert.Equal(esperado, ProcessadorLote.NomeSeguro(recebido));
    }
}
