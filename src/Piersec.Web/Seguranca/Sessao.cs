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

    /// <summary>Admin sem 2FA: só pode cadastrar o 2FA (ou sair) até terminar.</summary>
    public const string ClaimConfigurar2fa = "piersec:configurar_2fa";

    /// <summary>Senha certa, falta o código do app. Guarda até quando a etapa vale (horário UTC, em segundos).</summary>
    public const string ClaimFalta2fa = "piersec:falta_2fa_ate";

    /// <summary>Tempo para digitar o código depois da senha. Passou disso, volta para a senha.</summary>
    public static readonly TimeSpan PrazoSegundoFator = TimeSpan.FromMinutes(5);

    public static Task EntrarAsync(HttpContext contexto, Usuario usuario)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, usuario.Login),
            new(ClaimTypes.Role, usuario.Papel),
        };
        if (usuario.TrocarSenha)
            claims.Add(new Claim(ClaimTrocarSenha, "1"));
        if (ServicoAutenticacao.PrecisaConfigurar2fa(usuario))
            claims.Add(new Claim(ClaimConfigurar2fa, "1"));
        return Gravar(contexto, claims);
    }

    /// <summary>
    /// Sessão "meio caminho": só o login e o prazo, SEM o papel. Com ela não se abre nada além da tela do código
    /// (ver <see cref="ExigirTrocaDeSenha"/>), e as regras de acesso por papel também recusam.
    /// </summary>
    public static Task EntrarParcialAsync(HttpContext contexto, Usuario usuario, DateTimeOffset agora) =>
        Gravar(contexto,
        [
            new(ClaimTypes.Name, usuario.Login),
            new(ClaimFalta2fa, (agora + PrazoSegundoFator).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ]);

    private static Task Gravar(HttpContext contexto, List<Claim> claims) =>
        contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

    public static Task SairAsync(HttpContext contexto) =>
        contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    public static bool PrecisaTrocarSenha(ClaimsPrincipal usuario) => usuario.HasClaim(ClaimTrocarSenha, "1");

    public static bool PrecisaConfigurar2fa(ClaimsPrincipal usuario) => usuario.HasClaim(ClaimConfigurar2fa, "1");

    public static bool FaltaSegundoFator(ClaimsPrincipal usuario) => usuario.HasClaim(c => c.Type == ClaimFalta2fa);

    /// <summary>A etapa do código ainda está no prazo?</summary>
    public static bool SegundoFatorNoPrazo(ClaimsPrincipal usuario, DateTimeOffset agora) =>
        long.TryParse(usuario.FindFirst(ClaimFalta2fa)?.Value, out var ate) && agora.ToUnixTimeSeconds() <= ate;

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

/// <summary>
/// Etapas obrigatórias, nesta ordem: código do 2FA (se a senha já foi aceita) → troca da senha inicial → cadastro do
/// 2FA (admin sem 2FA). Enquanto houver uma pendente, toda página leva para ela; só ela, "Sair" e o CSS abrem.
/// </summary>
public sealed class ExigirTrocaDeSenha(RequestDelegate proximo)
{
    public Task InvokeAsync(HttpContext contexto)
    {
        var usuario = contexto.User;
        var destino = Sessao.FaltaSegundoFator(usuario) ? "/SegundoFator"
            : Sessao.PrecisaTrocarSenha(usuario) ? "/TrocarSenha"
            : Sessao.PrecisaConfigurar2fa(usuario) ? "/DoisFatores"
            : null;
        var caminho = contexto.Request.Path;
        if (destino is not null &&
            !caminho.StartsWithSegments(destino) && !caminho.StartsWithSegments("/Sair") && !caminho.StartsWithSegments("/css"))
        {
            contexto.Response.Redirect(destino);
            return Task.CompletedTask;
        }
        return proximo(contexto);
    }
}
