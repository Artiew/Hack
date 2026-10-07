using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Piersec.Core.Seguranca;

namespace Piersec.Web.Seguranca;

/// <summary>Cria e encerra a sessão (cookie) do usuário.</summary>
public static class Sessao
{
    /// <summary>Marca na sessão que o usuário ainda precisa trocar a senha inicial.</summary>
    public const string ClaimTrocarSenha = "piersec:trocar_senha";

    public static Task EntrarAsync(HttpContext contexto, Usuario usuario)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, usuario.Login),
            new(ClaimTypes.Role, usuario.Papel),
        };
        if (usuario.TrocarSenha)
            claims.Add(new Claim(ClaimTrocarSenha, "1"));
        var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade));
    }

    public static Task SairAsync(HttpContext contexto) =>
        contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    public static bool PrecisaTrocarSenha(ClaimsPrincipal usuario) => usuario.HasClaim(ClaimTrocarSenha, "1");

    public static bool PodeEnviar(ClaimsPrincipal usuario) => usuario.IsInRole(Papeis.Admin) || usuario.IsInRole(Papeis.Analista);
}

/// <summary>
/// Cabeçalhos de segurança em toda resposta (RS2 / OWASP): CSP restrita, sem "sniffing" de tipo, sem ser
/// embutido em outro site, sem enviar o endereço de origem e sem guardar páginas em cache.
/// </summary>
public sealed class CabecalhosSeguranca(RequestDelegate proximo)
{
    public const string CspSite =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; object-src 'none'; " +
        "base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

    /// <summary>O relatório tem CSS embutido e nenhum script: só isso é permitido nele.</summary>
    public const string CspRelatorio = "default-src 'none'; style-src 'unsafe-inline'; img-src data:; frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext contexto)
    {
        var h = contexto.Response.Headers;
        h.ContentSecurityPolicy = CspSite;
        h.XContentTypeOptions = "nosniff";
        h.XFrameOptions = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        h["Cross-Origin-Opener-Policy"] = "same-origin";
        if (!contexto.Request.Path.StartsWithSegments("/css"))
            h.CacheControl = "no-store";
        return proximo(contexto);
    }
}

/// <summary>Enquanto a senha inicial não for trocada, toda página leva para "Trocar senha" (troca obrigatória no 1º acesso).</summary>
public sealed class ExigirTrocaDeSenha(RequestDelegate proximo)
{
    public Task InvokeAsync(HttpContext contexto)
    {
        var caminho = contexto.Request.Path;
        if (Sessao.PrecisaTrocarSenha(contexto.User) &&
            !caminho.StartsWithSegments("/TrocarSenha") && !caminho.StartsWithSegments("/Sair") && !caminho.StartsWithSegments("/css"))
        {
            contexto.Response.Redirect("/TrocarSenha");
            return Task.CompletedTask;
        }
        return proximo(contexto);
    }
}
