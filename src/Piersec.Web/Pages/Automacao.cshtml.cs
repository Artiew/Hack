using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Operacao;
using Piersec.Core.Seguranca;
using Piersec.Web.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>
/// Operação automática (SOC simulado): o que a plataforma fez sozinha e por quê, a fila de aprovação humana,
/// os chamados abertos (com desfazer) e o log de decisão. Executivo vê; admin e analista decidem.
/// </summary>
public sealed class AutomacaoModel(ServicoOperacao operacao, Auditoria auditoria) : PageModel
{
    public EstadoOperacao Estado { get; private set; } = EstadoOperacao.Vazio;
    public RegrasAutomacao Config => operacao.Config;
    public bool PodeDecidir => Sessao.PodeEnviar(User);

    [TempData]
    public string? Mensagem { get; set; }

    [TempData]
    public bool MensagemOk { get; set; }

    public int AutomaticasHoje { get; private set; }

    public void OnGet()
    {
        Estado = operacao.Estado();
        AutomaticasHoje = operacao.AutomaticasHoje();
    }

    public IActionResult OnPostAprovar(string numero) =>
        Decidir(numero, "chamado_aprovado", usuario => operacao.Aprovar(numero, usuario));

    public IActionResult OnPostRejeitar(string numero, string? motivo) =>
        Decidir(numero, "chamado_rejeitado", usuario => operacao.Rejeitar(numero, usuario, motivo));

    public IActionResult OnPostDesfazer(string numero) =>
        Decidir(numero, "chamado_desfeito", usuario => operacao.Desfazer(numero, usuario));

    private IActionResult Decidir(string numero, string acaoAuditoria, Func<string, ResultadoAcao> fazer)
    {
        var usuario = User.Identity?.Name ?? Auditoria.Desconhecido;
        if (!PodeDecidir)
        {
            auditoria.Registrar(usuario, "automacao_negada");
            return Forbid(); // executivo só acompanha
        }
        var resultado = fazer(usuario);
        // Na auditoria vão só o número e o playbook: o motivo digitado fica no histórico do chamado (já filtrado).
        if (resultado.Ok)
            auditoria.Registrar(usuario, acaoAuditoria, $"{resultado.Chamado!.Numero} · {resultado.Chamado.PlaybookId}");
        Mensagem = resultado.Mensagem;
        MensagemOk = resultado.Ok;
        return RedirectToPage();
    }
}
