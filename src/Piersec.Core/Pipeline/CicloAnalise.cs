using System.Text;
using Piersec.Core.Armazenamento;
using Piersec.Core.Modelo;
using Piersec.Core.Relatorio;

namespace Piersec.Core.Pipeline;

public sealed record ResultadoCiclo(Lote Lote, bool PdfGerado, string MensagemPdf);

/// <summary>
/// O ciclo completo, sem depender da tela: processa o lote, salva e gera o relatório (HTML + PDF).
/// Fica no núcleo para a automação (fases seguintes) poder chamá-lo sem ninguém clicar em nada.
/// </summary>
public sealed class CicloAnalise(
    ProcessadorLote processador,
    RepositorioLotes repositorio,
    GeradorRelatorioHtml gerador,
    ConversorPdf conversorPdf)
{
    public async Task<ResultadoCiclo> ExecutarAsync(IReadOnlyList<ArquivoEnviado> arquivos, CancellationToken ct = default)
    {
        var lote = processador.Processar(arquivos);
        repositorio.Salvar(lote);

        var caminhoHtml = repositorio.CaminhoRelatorio(lote.Id, "html");
        await File.WriteAllTextAsync(caminhoHtml, gerador.Gerar(lote), new UTF8Encoding(false), ct);

        var pdf = await conversorPdf.GerarAsync(caminhoHtml, repositorio.CaminhoRelatorio(lote.Id, "pdf"), ct);
        return new ResultadoCiclo(lote, pdf.Sucesso, pdf.Mensagem);
    }
}
