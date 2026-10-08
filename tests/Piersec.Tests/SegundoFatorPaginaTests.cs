using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Seguranca;
using Piersec.Web.Pages;
using Piersec.Web.Seguranca;

namespace Piersec.Tests;

public sealed class SegundoFatorPaginaTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("piersec-2fa-pagina-").FullName;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Codigo_vazio_exibe_orientacao_sem_consumir_tentativa(string? codigo)
    {
        var relogio = new RelogioFixo(Fabrica.Agora);
        var repositorio = new RepositorioUsuarios(_pasta);
        repositorio.Salvar(new Usuario
        {
            Login = "usuario-ficticio", Papel = Papeis.Analista,
            HashSenha = HashSenha.Gerar(Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))),
            CriadoEmUtc = Fabrica.Agora,
            Segredo2fa = Convert.ToBase64String(Totp.GerarSegredo()),
            TentativasFalhas = 2,
        });
        var contexto = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.Name, "usuario-ficticio"),
                new Claim(Sessao.ClaimFalta2fa, (Fabrica.Agora + Sessao.PrazoSegundoFator).ToUnixTimeSeconds().ToString()),
            ], "teste")),
        };
        var pagina = new SegundoFatorModel(new ServicoAutenticacao(repositorio, relogio),
            repositorio, new Auditoria(_pasta, relogio), relogio)
        {
            PageContext = new PageContext { HttpContext = contexto },
            Codigo = codigo,
        };

        Assert.IsType<PageResult>(await pagina.OnPostAsync());
        Assert.Equal("Digite o código de 6 dígitos do aplicativo ou um código de recuperação antes de confirmar.", pagina.Erro);
        Assert.Equal(2, repositorio.Obter("usuario-ficticio")!.TentativasFalhas);
        Assert.True(Sessao.FaltaSegundoFator(contexto.User));
    }

    public void Dispose() => Directory.Delete(_pasta, recursive: true);
}
