using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Armazenamento;
using Piersec.Core.Consulta;
using Piersec.Web.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>Busca da barra lateral e item "Achados": leva aos eventos do lote mais recente (só admin e analista).</summary>
public sealed class BuscarModel(RepositorioLotes repositorio) : PageModel
{
    public IActionResult OnGet(string? q)
    {
        var ultimo = repositorio.Listar().FirstOrDefault();
        if (ultimo is null)
            return Page(); // nenhum lote ainda: a página explica o que fazer
        return Redirect(Links.Eventos(ultimo.Id, new FiltroEventos { Texto = q }.Limpo()));
    }
}
