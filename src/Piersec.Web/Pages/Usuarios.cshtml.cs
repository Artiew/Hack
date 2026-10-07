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

    /// <summary>Usuário a confirmar a exclusão (passo 1: GET ?excluir=login mostra "Tem certeza?").</summary>
    public Usuario? AConfirmar { get; private set; }

    public string? Mensagem { get; private set; }

    public void OnGet(string? excluir)
    {
        Usuarios = repositorio.Listar();
        AConfirmar = excluir is null ? null : repositorio.Obter(excluir);
    }

    /// <summary>Passo 2: exclui de fato (POST com token). A sessão aberta da pessoa cai no próximo clique.</summary>
    public IActionResult OnPostExcluir(string? login)
    {
        var quem = User.Identity!.Name!;
        var erro = login is null ? "Usuário não encontrado." : servico.Excluir(login, quem);
        if (erro is null)
        {
            auditoria.Registrar(quem, "usuario_excluido", login!);
            Mensagem = $"Usuário {login} excluído. Se estava com o site aberto, perde o acesso no próximo clique.";
        }
        else
            Erro = erro;
        Usuarios = repositorio.Listar();
        return Page();
    }

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
