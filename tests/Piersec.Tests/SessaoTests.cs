using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Piersec.Core.Seguranca;
using Piersec.Web.Seguranca;

namespace Piersec.Tests;

public sealed class SessaoTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resposta_usa_mesma_identidade_do_cookie_apos_mudar_etapa(bool tem2fa)
    {
        var autenticacao = new AutenticacaoFicticia();
        using var servicos = new ServiceCollection().AddSingleton<IAuthenticationService>(autenticacao).BuildServiceProvider();
        var contexto = new DefaultHttpContext
        {
            RequestServices = servicos,
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.Name, "admin-ficticio"),
                new Claim(Sessao.ClaimConfigurar2fa, "1"),
            ], "etapa-anterior")),
        };
        var usuario = new Usuario
        {
            Login = "admin-ficticio", Papel = Papeis.Admin, HashSenha = "não utilizado neste teste",
            CriadoEmUtc = Fabrica.Agora, Segredo2fa = tem2fa ? "cadastro-ficticio" : null,
        };

        await Sessao.EntrarAsync(contexto, usuario);

        Assert.Same(autenticacao.PrincipalGravado, contexto.User);
        Assert.True(contexto.User.IsInRole(Papeis.Admin));
        Assert.Equal(!tem2fa, Sessao.PrecisaConfigurar2fa(contexto.User));
        Assert.False(Sessao.FaltaSegundoFator(contexto.User));
    }

    private sealed class AutenticacaoFicticia : IAuthenticationService
    {
        public ClaimsPrincipal? PrincipalGravado { get; private set; }
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            PrincipalGravado = principal;
            return Task.CompletedTask;
        }
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
