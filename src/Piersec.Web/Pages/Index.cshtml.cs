using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Armazenamento;
using Piersec.Core.Modelo;
using Piersec.Core.Pipeline;

namespace Piersec.Web.Pages;

/// <summary>Tela "Enviar arquivos": recebe os CSVs, roda o ciclo e abre o resultado do lote.</summary>
public sealed class IndexModel(CicloAnalise ciclo, RepositorioLotes repositorio, ILogger<IndexModel> log) : PageModel
{
    public IReadOnlyList<ResumoLote> Lotes { get; private set; } = [];
    public string? Erro { get; private set; }

    public void OnGet() => Lotes = repositorio.Listar();

    // Razor Pages valida o token anti-CSRF automaticamente em todo POST.
    public async Task<IActionResult> OnPostAsync(List<IFormFile>? arquivos, CancellationToken ct)
    {
        if (arquivos is null || arquivos.Count == 0)
        {
            Erro = "Selecione ao menos um arquivo .csv.";
            Lotes = repositorio.Listar();
            return Page();
        }

        var enviados = new List<ArquivoEnviado>(arquivos.Count);
        foreach (var arquivo in arquivos)
        {
            if (arquivo.Length > ProcessadorLote.TamanhoMaximoBytes)
            {
                enviados.Add(new ArquivoEnviado(arquivo.FileName, arquivo.Length, null)); // recusado sem ler
                continue;
            }
            using var memoria = new MemoryStream((int)arquivo.Length);
            await arquivo.CopyToAsync(memoria, ct);
            enviados.Add(ArquivoEnviado.De(arquivo.FileName, memoria.ToArray()));
        }

        var resultado = await ciclo.ExecutarAsync(enviados, ct);

        // Só números no log: nada de conteúdo dos arquivos (RS4).
        log.LogInformation("Lote {Id}: {Arquivos} arquivo(s), {Eventos} evento(s), {Lacunas} lacuna(s). PDF: {Pdf}",
            resultado.Lote.Id, resultado.Lote.Arquivos.Count, resultado.Lote.Eventos.Count,
            resultado.Lote.Lacunas.Count, resultado.MensagemPdf);

        return RedirectToPage("/Lote", new { id = resultado.Lote.Id });
    }
}
