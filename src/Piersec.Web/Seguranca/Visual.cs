using Microsoft.AspNetCore.Html;
using Piersec.Core.Modelo;

namespace Piersec.Web.Seguranca;

/// <summary>Peças visuais repetidas nas telas, para ficarem iguais em todo lugar.</summary>
public static class Visual
{
    /// <summary>
    /// Severidade com cor + ícone + texto (o PROMPT pede os três: ninguém depende só da cor). O ícone muda de forma
    /// (triângulo, círculo, quadrado, círculo vazado) para continuar distinguível em preto e branco ou para daltônicos.
    /// Os textos vêm de uma lista fixa (Nome()), nunca dos dados: não há o que escapar.
    /// </summary>
    public static IHtmlContent Selo(Severidade? severidade)
    {
        var classe = severidade.ClasseCss();
        var icone = severidade is null ? "" : $"<svg class=\"icone\" aria-hidden=\"true\"><use href=\"#i-{classe}\"/></svg>";
        return new HtmlString($"<span class=\"sev {classe}\">{icone}{severidade.Nome()}</span>");
    }
}
