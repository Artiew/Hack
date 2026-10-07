using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>Trilha de auditoria (só admin): quem fez o quê e quando, sem dados sensíveis.</summary>
public sealed class AuditoriaModel(Auditoria auditoria) : PageModel
{
    public const int Quantidade = 200;
    public IReadOnlyList<RegistroAuditoria> Registros { get; private set; } = [];

    public void OnGet() => Registros = auditoria.Ultimos(Quantidade);
}
