using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Seguranca;
using Piersec.Web.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>Tela de login. O erro é sempre o mesmo, para não revelar se o usuário existe ou está bloqueado.</summary>
public sealed class EntrarModel(ServicoAutenticacao servico, Auditoria auditoria, TimeProvider relogio) : PageModel
{
    public const string ErroGenerico = "Não foi possível entrar. Confira usuário e senha ou tente novamente mais tarde.";

    [BindProperty] public string? Login { get; set; }
    [BindProperty] public string? Senha { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public string? Erro { get; private set; }

    /// <summary>Mensagem vinda de outra tela (ex.: "o tempo para digitar o código acabou").</summary>
    [TempData] public string? Aviso { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
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

        var destino = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/";
        if (tentativa.Resultado == ResultadoEntrada.PrecisaSegundoFator)
        {
            // Senha certa, mas ainda NÃO entrou: a sessão parcial só abre a tela do código.
            await Sessao.EntrarParcialAsync(HttpContext, tentativa.Usuario!, relogio.GetUtcNow());
            auditoria.Registrar(tentativa.Usuario!.Login, "senha_ok_aguarda_2fa");
            return RedirectToPage("/SegundoFator", new { returnUrl = destino });
        }

        await Sessao.EntrarAsync(HttpContext, tentativa.Usuario!);
        auditoria.Registrar(tentativa.Usuario!.Login, "entrada");
        if (tentativa.Resultado == ResultadoEntrada.PrecisaTrocarSenha)
            return RedirectToPage("/TrocarSenha");
        return LocalRedirect(destino);
    }
}
