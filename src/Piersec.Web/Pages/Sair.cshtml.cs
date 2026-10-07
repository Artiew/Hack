using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Seguranca;
using Piersec.Web.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>Encerra a sessão. Só por POST (com token anti-CSRF): um link externo não consegue deslogar ninguém.</summary>
public sealed class SairModel(Auditoria auditoria) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        auditoria.Registrar(User.Identity?.Name, "saida");
        await Sessao.SairAsync(HttpContext);
        return RedirectToPage("/Entrar");
    }
}
