using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>Cadastro de usuários (só admin). A senha inicial aparece UMA vez, nesta resposta, e não é guardada.</summary>
public sealed class UsuariosModel(ServicoAutenticacao servico, RepositorioUsuarios repositorio, Auditoria auditoria, TimeProvider relogio) : PageModel
{
    [BindProperty] public string? NovoLogin { get; set; }
    [BindProperty] public string NovoPapel { get; set; } = Papeis.Analista;

    public IReadOnlyList<Usuario> Usuarios { get; private set; } = [];
    public string? Erro { get; private set; }
    public (string Login, string Senha)? Criado { get; private set; }
    public DateTimeOffset Agora => relogio.GetUtcNow();

    public void OnGet() => Usuarios = repositorio.Listar();

    public IActionResult OnPost()
    {
        try
        {
            var (usuario, senha) = servico.Criar(NovoLogin ?? "", NovoPapel);
            Criado = (usuario.Login, senha);
            auditoria.Registrar(User.Identity!.Name, "usuario_criado", $"{usuario.Login} ({usuario.Papel})");
            NovoLogin = null;
        }
        catch (ArgumentException ex)
        {
            Erro = ex.Message;
        }
        Usuarios = repositorio.Listar();
        return Page();
    }
}
