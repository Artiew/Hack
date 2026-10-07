using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Seguranca;
using Piersec.Web.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>Tela de login. O erro é sempre o mesmo, para não revelar se o usuário existe ou está bloqueado.</summary>
public sealed class EntrarModel(ServicoAutenticacao servico, Auditoria auditoria) : PageModel
{
    public const string ErroGenerico = "Não foi possível entrar. Confira usuário e senha ou tente novamente mais tarde.";

    [BindProperty] public string? Login { get; set; }
    [BindProperty] public string? Senha { get; set; }
    public string? Erro { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        var tentativa = servico.Entrar(Login, Senha);
        Senha = null; // nunca devolver a senha para a página

        if (tentativa.Resultado == ResultadoEntrada.Falhou)
        {
            // Login inexistente vira "(desconhecido)": não gravamos o que foi digitado (pode ser uma senha).
            auditoria.Registrar(tentativa.Usuario?.Login, tentativa.Bloqueado ? "entrada_bloqueada" : "entrada_falhou");
            Erro = ErroGenerico;
            return Page();
        }

        await Sessao.EntrarAsync(HttpContext, tentativa.Usuario!);
        auditoria.Registrar(tentativa.Usuario!.Login, "entrada");
        if (tentativa.Resultado == ResultadoEntrada.PrecisaTrocarSenha)
            return RedirectToPage("/TrocarSenha");
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
