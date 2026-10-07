using System.Security.Cryptography;
using System.Text;
using Piersec.Core.Armazenamento;
using Piersec.Core.Padronizacao;
using Piersec.Core.Pipeline;
using Piersec.Core.Relatorio;

namespace Piersec.Tests;

/// <summary>Cifrador de teste: AES-GCM com chave fixa (cifra de verdade e recusa conteúdo alterado).</summary>
internal sealed class CifradorDeTeste : ICifrador
{
    private static readonly byte[] Chave = Enumerable.Range(10, 32).Select(i => (byte)i).ToArray();

    public byte[] Cifrar(byte[] aberto)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cifrado = new byte[aberto.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(Chave, 16);
        aes.Encrypt(nonce, aberto, cifrado, tag);
        return [.. nonce, .. tag, .. cifrado];
    }

    public byte[] Decifrar(byte[] dados)
    {
        var aberto = new byte[dados.Length - 28];
        using var aes = new AesGcm(Chave, 16);
        aes.Decrypt(dados.AsSpan(0, 12), dados.AsSpan(28), dados.AsSpan(12, 16), aberto); // lança se alterado
        return aberto;
    }
}

public class CifragemTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("piersec-cifra-").FullName;
    private readonly CifradorDeTeste _cifrador = new();

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    private string[] TextoDeTodosOsArquivos() =>
        Directory.GetFiles(_pasta, "*", SearchOption.AllDirectories).Select(f => Encoding.Latin1.GetString(File.ReadAllBytes(f))).ToArray();

    [Fact]
    public void Lote_gravado_nao_e_legivel_no_disco_mas_volta_inteiro()
    {
        var repositorio = new RepositorioLotes(_pasta, _cifrador);
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv")]);

        repositorio.Salvar(lote);

        Assert.All(TextoDeTodosOsArquivos(), t =>
        {
            Assert.DoesNotContain("FICT-SRV", t);
            Assert.DoesNotContain("CVE-2099", t);
        });
        Assert.Equal(lote.Eventos.Count, repositorio.Carregar(lote.Id)!.Eventos.Count);
        Assert.Equal(lote.Id, Assert.Single(repositorio.Listar()).Id);
    }

    [Fact]
    public void Arquivo_alterado_no_disco_e_recusado_e_nao_lido_pela_metade()
    {
        var repositorio = new RepositorioLotes(_pasta, _cifrador);
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv")]);
        repositorio.Salvar(lote);

        var arquivo = Directory.GetFiles(_pasta, "lote.json.cifrado", SearchOption.AllDirectories).Single();
        var bytes = File.ReadAllBytes(arquivo);
        bytes[^1] ^= 0xFF; // altera 1 byte
        File.WriteAllBytes(arquivo, bytes);

        Assert.Null(repositorio.Carregar(lote.Id));
    }

    [Fact]
    public void Relatorio_gravado_cifrado_volta_igual()
    {
        var repositorio = new RepositorioLotes(_pasta, _cifrador);
        var id = Guid.NewGuid().ToString("N");
        var html = Encoding.UTF8.GetBytes("<p>FICT-SRV-01 relatório</p>");

        repositorio.SalvarRelatorio(id, "html", html);

        Assert.True(repositorio.ExisteRelatorio(id, "html"));
        Assert.False(repositorio.ExisteRelatorio(id, "pdf"));
        Assert.Equal(html, repositorio.LerRelatorio(id, "html"));
        Assert.All(TextoDeTodosOsArquivos(), t => Assert.DoesNotContain("FICT-SRV", t));
    }

    [Fact]
    public void Chave_de_anonimizacao_fica_cifrada_e_e_sempre_a_mesma()
    {
        var primeira = ChaveAnonimizacao.Obter(_pasta, _cifrador);
        var segunda = ChaveAnonimizacao.Obter(_pasta, _cifrador);

        Assert.Equal(primeira, segunda);
        var gravado = File.ReadAllBytes(Directory.GetFiles(_pasta, "chave-anonimizacao.cifrado", SearchOption.AllDirectories).Single());
        Assert.False(gravado.AsSpan().IndexOf(primeira) >= 0, "a chave aparece aberta no arquivo");
    }

    [Fact]
    public async Task Ciclo_completo_nao_deixa_nada_legivel_nem_temporario_no_disco()
    {
        var conversor = new ConversorPdf();
        Assert.SkipWhen(conversor.Navegador is null, "Nenhum Edge/Chrome nesta máquina.");
        var repositorio = new RepositorioLotes(_pasta, _cifrador);
        var ciclo = new CicloAnalise(Fabrica.ProcessadorReal(), repositorio, Fabrica.Gerador(), conversor, Fabrica.Validador());

        var resultado = await ciclo.ExecutarAsync([Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")],
            TestContext.Current.CancellationToken);

        Assert.True(resultado.PdfGerado, resultado.MensagemPdf);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(repositorio.LerRelatorio(resultado.Lote.Id, "pdf")!, 0, 5));
        Assert.Contains("FICT-SRV", Encoding.UTF8.GetString(repositorio.LerRelatorio(resultado.Lote.Id, "html")!));
        Assert.All(TextoDeTodosOsArquivos(), t => Assert.DoesNotContain("FICT-SRV", t));
        var temporarias = Path.Combine(_pasta, "tmp");
        Assert.True(!Directory.Exists(temporarias) || Directory.GetFileSystemEntries(temporarias).Length == 0, "sobrou arquivo temporário");
    }
}
