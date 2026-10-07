using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Armazenamento;
using Piersec.Core.Modelo;
using Piersec.Core.Relatorio;
using Piersec.Web.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>Resultado de um lote: o que aconteceu com cada arquivo e links do relatório.</summary>
public sealed class LoteModel(RepositorioLotes repositorio, LotesEmMemoria lotes, GeradorRelatorioHtml gerador) : PageModel
{
    public Lote Lote { get; private set; } = null!;
    public bool PdfDisponivel { get; private set; }

    /// <summary>Mesmo aviso de confiança baixa da página 1 do relatório (null quando a confiança não é baixa).</summary>
    public string? AvisoConfianca { get; private set; }

    /// <summary>"Três problemas respondem por…" ou "O risco está espalhado…" (null quando não se aplica).</summary>
    public string? FraseAchados { get; private set; }

    /// <summary>Revisões feitas a partir deste lote (quando alguém discordou de severidades).</summary>
    public IReadOnlyList<ResumoLote> Revisoes { get; private set; } = [];

    public IActionResult OnGet(string id)
    {
        var lote = lotes.Obter(id); // id inválido → null → 404
        if (lote is null)
            return NotFound();
        Lote = lote;
        PdfDisponivel = repositorio.ExisteRelatorio(id, "pdf");
        AvisoConfianca = lote.Avaliacao is { } a ? gerador.AvisoConfianca(a.Score) : null;
        FraseAchados = lote.Avaliacao is { } av ? gerador.FraseAchados(av) : null;
        Revisoes = repositorio.Listar().Where(r => r.RevisaoDe == lote.Id).ToList();
        return Page();
    }
}
