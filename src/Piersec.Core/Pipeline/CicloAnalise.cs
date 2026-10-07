using System.Text;
using Piersec.Core.Armazenamento;
using Piersec.Core.Modelo;
using Piersec.Core.Operacao;
using Piersec.Core.Relatorio;
using Piersec.Core.Validacao;

namespace Piersec.Core.Pipeline;

public sealed record ResultadoCiclo(Lote Lote, bool PdfGerado, string MensagemPdf);

/// <summary>
/// O ciclo completo, sem depender da tela: processa o lote, compara com o lote anterior, VALIDA, salva e gera o
/// relatório (HTML + PDF). Se a validação reprovar, o relatório sai marcado como "não validado"; se o relatório
/// pronto contiver algo com cara de credencial, ele é retido. Fica no núcleo para a automação poder chamá-lo.
/// </summary>
public sealed class CicloAnalise(
    ProcessadorLote processador,
    RepositorioLotes repositorio,
    GeradorRelatorioHtml gerador,
    ConversorPdf conversorPdf,
    ValidadorLote validador,
    ServicoOperacao? operacao = null)
{
    public async Task<ResultadoCiclo> ExecutarAsync(IReadOnlyList<ArquivoEnviado> arquivos, CancellationToken ct = default)
    {
        var lote = processador.Processar(arquivos);
        var anterior = repositorio.Listar().FirstOrDefault(r => r.Score is not null);
        lote = lote with { Comparacao = Comparar(lote, anterior) };
        lote = lote with { Validacao = validador.Validar(lote) };

        // Operação automática (simulada): roda depois da validação, porque lote não validado não dispara nada sozinho.
        // Em seguida, V13 confere por outro caminho que nenhuma ação feita sozinha furou as travas.
        if (operacao is not null)
        {
            var resultado = operacao.Processar(lote);
            lote = lote with
            {
                Operacao = resultado,
                Validacao = new ResultadoValidacao([.. lote.Validacao!.Verificacoes, MotorDecisao.Conferir(resultado, operacao.Config)]),
            };
        }

        // Relatório gerado e relido por inteiro antes de sair (V12). Se algo escapou do filtro, é retido.
        var html = gerador.Gerar(lote);
        var final = ValidadorLote.ConferirRelatorioFinal(html);
        lote = lote with { Validacao = new ResultadoValidacao([.. lote.Validacao!.Verificacoes, final]) };
        html = final.Aprovada ? gerador.Gerar(lote) : gerador.GerarRetido(lote);

        repositorio.Salvar(lote);
        repositorio.SalvarRelatorio(lote.Id, "html", new UTF8Encoding(false).GetBytes(html));

        // O navegador precisa do HTML aberto para "imprimir" o PDF: arquivos temporários em var/tmp, apagados no fim.
        var temporaria = repositorio.CriarPastaTemporaria();
        try
        {
            var caminhoHtml = Path.Combine(temporaria, "relatorio.html");
            var caminhoPdf = Path.Combine(temporaria, "relatorio.pdf");
            await File.WriteAllTextAsync(caminhoHtml, html, new UTF8Encoding(false), ct);
            var pdf = await conversorPdf.GerarAsync(caminhoHtml, caminhoPdf, ct);
            if (pdf.Sucesso)
                repositorio.SalvarRelatorio(lote.Id, "pdf", await File.ReadAllBytesAsync(caminhoPdf, ct));
            return new ResultadoCiclo(lote, pdf.Sucesso, pdf.Mensagem);
        }
        finally
        {
            ApagarTemporaria(temporaria);
        }
    }

    private static void ApagarTemporaria(string pasta)
    {
        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            try
            {
                Directory.Delete(pasta, recursive: true);
                return;
            }
            catch (IOException) { Thread.Sleep(200); } // o navegador pode segurar o arquivo por instantes
            catch (UnauthorizedAccessException) { Thread.Sleep(200); }
        }
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
