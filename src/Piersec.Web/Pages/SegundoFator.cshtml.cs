using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Seguranca;
using Piersec.Web.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>
/// Segunda etapa do login: código de 6 dígitos do app (ou um código de recuperação). Só abre com a sessão parcial
/// criada pela tela Entrar, e só por 5 minutos. Erros contam nas mesmas tentativas da senha.
/// </summary>
public sealed class SegundoFatorModel(ServicoAutenticacao servico, RepositorioUsuarios repositorio, Auditoria auditoria, TimeProvider relogio) : PageModel
{
    [BindProperty] public string? Codigo { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public string? Erro { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await ForaDaEtapa() ?? Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (await ForaDaEtapa() is { } fora)
            return fora;

        // O formulário deixa a validação de presença no servidor para sempre mostrar uma mensagem na página.
        // Campo vazio não é uma tentativa de adivinhar o código e não deve bloquear a conta.
        if (string.IsNullOrWhiteSpace(Codigo))
        {
            Erro = "Digite o código de 6 dígitos do aplicativo ou um código de recuperação antes de confirmar.";
            return Page();
        }

        var login = User.Identity!.Name!;
        var tentativa = servico.ConfirmarSegundoFator(login, Codigo);
        Codigo = null;
        if (!tentativa.Ok)
        {
            auditoria.Registrar(login, tentativa.Bloqueado ? "2fa_bloqueado" : "2fa_falhou");
            if (tentativa.Bloqueado)
                return await VoltarParaEntrar(EntrarModel.ErroGenerico);
            Erro = "Código inválido. Digite o código que aparece agora no app (ou um código de recuperação).";
            return Page();
        }

        await Sessao.EntrarAsync(HttpContext, tentativa.Usuario!); // agora sim: sessão completa, com o papel
        auditoria.Registrar(login, "entrada", tentativa.UsouRecuperacao
            ? $"com código de recuperação ({Formatar(tentativa.RecuperacaoRestantes)})"
            : "com 2FA");
        if (tentativa.UsouRecuperacao)
            TempData["Aviso2fa"] = $"Você entrou com um código de recuperação. {Formatar(tentativa.RecuperacaoRestantes)}." +
                (tentativa.RecuperacaoRestantes <= 2 ? " Gere códigos novos nesta página." : "");
        if (tentativa.Usuario!.TrocarSenha)
            return RedirectToPage("/TrocarSenha");
        return tentativa.UsouRecuperacao ? RedirectToPage("/DoisFatores") : LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
    }

    private static string Formatar(int n) => n == 1 ? "Resta 1 código" : $"Restam {n} códigos";

    /// <summary>Sem sessão parcial, ou com o prazo vencido, ou com o usuário apagado: volta para a senha.</summary>
    private async Task<IActionResult?> ForaDaEtapa()
    {
        if (!Sessao.FaltaSegundoFator(User))
            return LocalRedirect("/");
        if (!Sessao.SegundoFatorNoPrazo(User, relogio.GetUtcNow()) || repositorio.Obter(User.Identity!.Name!) is not { Tem2fa: true })
            return await VoltarParaEntrar("O tempo para digitar o código acabou. Entre de novo com a senha.");
        return null;
    }

    private async Task<IActionResult> VoltarParaEntrar(string mensagem)
    {
        await Sessao.SairAsync(HttpContext);
        TempData["Aviso"] = mensagem;
        return RedirectToPage("/Entrar");
    }
}
