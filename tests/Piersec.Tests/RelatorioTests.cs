using Piersec.Core.Armazenamento;
using Piersec.Core.Padronizacao;
using Piersec.Core.Pipeline;
using Piersec.Core.Relatorio;

namespace Piersec.Tests;

public class RelatorioTests
{
    private static string GerarHtml(params string[] linhas)
    {
        var lote = Fabrica.Processador().Processar([Fabrica.CsvExemplo("a.csv", linhas)]);
        return Fabrica.Gerador().Gerar(lote);
    }

    [Fact]
    public void Texto_malicioso_nos_dados_e_escapado()
    {
        var html = GerarHtml("1,2026-09-01 10:00,SRV-01,ana,Alta,web,\"<script>alert('x')</script>\"");

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Relatorio_das_fontes_reais_mostra_correlacoes_justificativas_e_regras_sem_ocorrencia()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")]);
        var html = Fabrica.Gerador().Gerar(lote);

        Assert.Contains("CVE-2099-0001 está em 12 máquinas", html);
        Assert.Contains("Não é Crítica porque a nota está abaixo de 9,0", html); // "por que não é crítica"
        Assert.Contains("0 em comum", html);                                      // o que não correlacionou
        Assert.Contains("ID.RA-01", html);                                        // frameworks
        Assert.Contains("linha(s) corrigida(s)", html);                           // ajuste de leitura declarado
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void Lacunas_iguais_aparecem_agrupadas_com_a_quantidade()
    {
        var html = GerarHtml(
            "1,data ruim,SRV-01,ana,Alta,web,x",
            "2,data ruim,SRV-01,ana,Alta,web,x",
            "3,data ruim,SRV-01,ana,Alta,web,x");

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "data em formato não reconhecido"));
        Assert.Contains("2, 3, 4", html); // linhas de exemplo
    }

    [Fact]
    public void Tem_selo_de_confidencialidade()
    {
        Assert.Contains(GeradorRelatorioHtml.SeloConfidencialidade, GerarHtml());
    }

    [Fact]
    public void Nao_contem_o_nome_original_do_usuario()
    {
        var html = GerarHtml("1,2026-09-01 10:00,SRV-01,usuario.ficticio01,Alta,web,teste");

        Assert.DoesNotContain("usuario.ficticio01", html);
    }

    [Fact]
    public void Nao_carrega_nada_de_fora()
    {
        var html = GerarHtml("1,2026-09-01 10:00,SRV-01,ana,Alta,web,teste");

        Assert.DoesNotContain("src=", html);
        Assert.DoesNotContain("<link", html);
        Assert.Contains("default-src 'none'", html);
    }

    [Fact]
    public void Lote_vazio_avisa_que_zero_eventos_nao_e_ambiente_limpo()
    {
        Assert.Contains("<strong>não</strong> significa ambiente sem problemas", GerarHtml());
    }
}

public class RepositorioLotesTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "piersec-teste-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(@"..\..\windows")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void Id_invalido_e_recusado(string? id)
    {
        Assert.False(RepositorioLotes.IdValido(id));
        Assert.Null(new RepositorioLotes(_pasta).Carregar(id!));
    }

    [Fact]
    public void Salva_e_carrega_o_mesmo_lote()
    {
        var repositorio = new RepositorioLotes(_pasta);
        var lote = Fabrica.Processador().Processar([Fabrica.CsvExemplo("a.csv", "1,2026-09-01 10:00,SRV-01,ana,Alta,web,teste")]);

        repositorio.Salvar(lote);
        var carregado = repositorio.Carregar(lote.Id)!;

        Assert.Equal(lote.Id, carregado.Id);
        Assert.Equal(lote.Eventos.Count, carregado.Eventos.Count);
        Assert.Equal(lote.Eventos[0].Severidade, carregado.Eventos[0].Severidade);
        Assert.Single(repositorio.Listar());
    }

    public void Dispose()
    {
        if (Directory.Exists(_pasta))
            Directory.Delete(_pasta, recursive: true);
    }
}

public class ConversorPdfTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("piersec-pdf-teste-").FullName;

    [Fact]
    public async Task Gera_um_PDF_valido_a_partir_do_HTML()
    {
        var conversor = new ConversorPdf();
        Assert.SkipWhen(conversor.Navegador is null, "Nenhum Edge/Chrome nesta máquina.");

        var html = Path.Combine(_pasta, "relatorio.html");
        var pdf = Path.Combine(_pasta, "relatorio.pdf");
        var lote = Fabrica.Processador().Processar([Fabrica.CsvExemplo("a.csv", "1,2026-09-01 10:00,SRV-01,ana,Alta,web,teste")]);
        await File.WriteAllTextAsync(html, Fabrica.Gerador().Gerar(lote), TestContext.Current.CancellationToken);

        var resultado = await conversor.GerarAsync(html, pdf, TestContext.Current.CancellationToken);

        Assert.True(resultado.Sucesso, resultado.Mensagem);
        Assert.True(ConversorPdf.ArquivoEhPdf(pdf));
    }

    [Fact]
    public async Task Sem_navegador_falha_com_mensagem_clara()
    {
        var conversor = new ConversorPdf(caminhoNavegador: Path.Combine(_pasta, "nao-existe.exe"));

        var resultado = await conversor.GerarAsync("x.html", Path.Combine(_pasta, "x.pdf"), TestContext.Current.CancellationToken);

        Assert.Null(conversor.Navegador);
        Assert.False(resultado.Sucesso);
        Assert.Contains("Imprimir", resultado.Mensagem);
    }

    public void Dispose() => Directory.Delete(_pasta, recursive: true);
}
