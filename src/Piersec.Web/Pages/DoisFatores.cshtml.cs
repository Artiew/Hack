using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Seguranca;
using Piersec.Web.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>
/// Verificação em duas etapas da própria conta: cadastrar (QR code + confirmação com um código), gerar códigos de
/// recuperação novos e desligar (só quem não é admin). Os códigos de recuperação aparecem UMA vez.
/// </summary>
public sealed class DoisFatoresModel(ServicoAutenticacao servico, RepositorioUsuarios repositorio, Auditoria auditoria) : PageModel
{
    [BindProperty] public string? Codigo { get; set; }

    public Usuario Usuario { get; private set; } = null!;
    public string? QrSvg { get; private set; }
    public string? Chave { get; private set; }
    public string? Erro { get; private set; }
    public string? Mensagem { get; private set; }

    /// <summary>Códigos de recuperação recém-gerados: só nesta resposta, nunca de novo.</summary>
    public IReadOnlyList<string> CodigosNovos { get; private set; } = [];

    public bool Obrigatorio => ServicoAutenticacao.Exige2fa(Usuario);

    private string Login => User.Identity!.Name!;

    public IActionResult OnGet()
    {
        Mensagem = TempData["Aviso2fa"] as string;
        return Montar();
    }

    public async Task<IActionResult> OnPostConfirmarAsync()
    {
        var (erro, codigos) = servico.Confirmar2fa(Login, Codigo);
        if (erro is not null)
        {
            auditoria.Registrar(Login, "2fa_confirmacao_falhou");
            return Montar(erro);
        }
        auditoria.Registrar(Login, "2fa_ativado");
        await Sessao.EntrarAsync(HttpContext, repositorio.Obter(Login)!); // tira a marca "precisa configurar"
        CodigosNovos = codigos;
        return Montar(mensagem: "Verificação em duas etapas ativada. A partir do próximo login, o site vai pedir o código do app.");
    }

    public IActionResult OnPostNovosCodigos()
    {
        var (erro, codigos) = servico.NovosCodigosRecuperacao(Login, Codigo);
        if (erro is not null)
            return Montar(erro);
        auditoria.Registrar(Login, "2fa_codigos_novos");
        CodigosNovos = codigos;
        return Montar(mensagem: "Códigos de recuperação novos gerados. Os antigos não valem mais.");
    }

    public IActionResult OnPostDesativar()
    {
        if (servico.Desativar2fa(Login, Codigo) is { } erro)
            return Montar(erro);
        auditoria.Registrar(Login, "2fa_desativado");
        TempData["Aviso2fa"] = "Verificação em duas etapas desligada. Pode apagar a conta Piersec do app.";
        return RedirectToPage("/DoisFatores"); // POST → redireciona → GET (F5 não repete)
    }

    private PageResult Montar(string? erro = null, string? mensagem = null)
    {
        Codigo = null;
        Erro = erro;
        Mensagem = mensagem ?? Mensagem;
        Usuario = repositorio.Obter(Login)!;
        if (!Usuario.Tem2fa)
        {
            var (chave, endereco) = servico.Iniciar2fa(Login);
            Chave = Base32.EmGrupos(chave);
            QrSvg = QrCode.Gerar(endereco).Svg("QR code para cadastrar a Piersec no app de autenticação");
        }
        return Page();
    }
}
