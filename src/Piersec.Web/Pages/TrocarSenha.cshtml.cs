using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Seguranca;
using Piersec.Web.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>Troca de senha (obrigatória no primeiro acesso).</summary>
public sealed class TrocarSenhaModel(ServicoAutenticacao servico, RepositorioUsuarios repositorio, Auditoria auditoria) : PageModel
{
    [BindProperty] public string? Atual { get; set; }
    [BindProperty] public string? Nova { get; set; }
    [BindProperty] public string? Confirmacao { get; set; }
    public string? Erro { get; private set; }
    public bool Obrigatoria => Sessao.PrecisaTrocarSenha(User);

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var login = User.Identity!.Name!;
        Erro = servico.TrocarSenha(login, Atual, Nova, Confirmacao);
        Atual = Nova = Confirmacao = null;
        if (Erro is not null)
        {
            auditoria.Registrar(login, "troca_senha_recusada");
            return Page();
        }

        await Sessao.EntrarAsync(HttpContext, repositorio.Obter(login)!); // sessão nova, sem a marca de troca pendente
        auditoria.Registrar(login, "senha_trocada");
        return RedirectToPage("/Index");
    }
}
