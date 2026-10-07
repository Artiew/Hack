using System.Text;
using Piersec.Core.Armazenamento;
using Piersec.Core.Modelo;
using Piersec.Core.Relatorio;

namespace Piersec.Core.Pipeline;

public sealed record ResultadoCiclo(Lote Lote, bool PdfGerado, string MensagemPdf);

/// <summary>
/// O ciclo completo, sem depender da tela: processa o lote, compara com o lote anterior, salva e gera o
/// relatório (HTML + PDF). Fica no núcleo para a automação (fases seguintes) poder chamá-lo sem ninguém clicar.
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
        var anterior = repositorio.Listar().FirstOrDefault(r => r.Score is not null);
        lote = lote with { Comparacao = Comparar(lote, anterior) };
        repositorio.Salvar(lote);

        var caminhoHtml = repositorio.CaminhoRelatorio(lote.Id, "html");
        await File.WriteAllTextAsync(caminhoHtml, gerador.Gerar(lote), new UTF8Encoding(false), ct);

        var pdf = await conversorPdf.GerarAsync(caminhoHtml, repositorio.CaminhoRelatorio(lote.Id, "pdf"), ct);
        return new ResultadoCiclo(lote, pdf.Sucesso, pdf.Mensagem);
    }

    /// <summary>Tendência: quanto o score mudou e qual fator mais explica a mudança.</summary>
    public static ComparacaoLote? Comparar(Lote atual, ResumoLote? anterior)
    {
        if (anterior?.Score is not { } scoreAnterior || atual.Avaliacao?.Score.Valor is not { } scoreAtual)
            return null;

        var motivo = "os fatores medidos não mudaram";
        var fatoresAntes = anterior.Fatores ?? new Dictionary<string, double?>();
        var mudancas = atual.Avaliacao.Score.Fatores
            .Select(f => (f.Nome, Antes: fatoresAntes.GetValueOrDefault(f.Id), Agora: f.Valor))
            .Where(m => m.Antes != m.Agora)
            .OrderByDescending(m => Math.Abs((m.Agora ?? 0) - (m.Antes ?? 0)))
            .ToList();
        if (mudancas.Count > 0)
        {
            var m = mudancas[0];
            motivo = (m.Antes, m.Agora) switch
            {
                (null, { } agora) => $"o fator \"{m.Nome}\" passou a ser medido ({agora:0})",
                ({ } antes, null) => $"o fator \"{m.Nome}\" deixou de ser medido (era {antes:0})",
                ({ } antes, { } agora) => $"o fator \"{m.Nome}\" foi de {antes:0} para {agora:0}",
                _ => motivo,
            };
        }
        return new ComparacaoLote(anterior.Id, anterior.CriadoEmUtc, scoreAnterior, Math.Round(scoreAtual - scoreAnterior, 1), motivo);
    }
}
