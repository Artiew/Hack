using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using Microsoft.Extensions.WebEncoders;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Piersec.Core.Analise;
using Piersec.Core.Armazenamento;
using Piersec.Core.Coleta;
using Piersec.Core.Padronizacao;
using Piersec.Core.Pipeline;
using Piersec.Core.Relatorio;
using Piersec.Core.Seguranca;
using Piersec.Core.Validacao;
using Piersec.Web.Seguranca;

var builder = WebApplication.CreateBuilder(args);

// Pasta dos dados gerados (lotes, relatórios, usuários, auditoria). Fica em var/, que está no .gitignore.
var pastaDados = Path.GetFullPath(builder.Configuration["Piersec:PastaDados"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", "var"));
Directory.CreateDirectory(pastaDados);

// Limite do envio inteiro (vários arquivos). O limite por arquivo é checado no ProcessadorLote.
const long LimiteRequisicaoBytes = 60L * 1024 * 1024;
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = LimiteRequisicaoBytes;
    o.AddServerHeader = false; // não anunciar qual servidor é usado
});
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = LimiteRequisicaoBytes);

// Escape de HTML continua ligado (< > & aspas), mas acentos saem legíveis ("ã" em vez de "&#xE3;").
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

// Regras de severidade, correlação, score e plano (regras/*.json). Regra inválida impede a plataforma de subir.
var pastaRegras = Path.GetFullPath(builder.Configuration["Piersec:PastaRegras"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", "regras"));
var regras = ConjuntoRegras.Carregar(pastaRegras);

// Cifra em repouso (RS3): chaves em var/chaves, protegidas pelo Windows (DPAPI). Também protegem os cookies de sessão.
var protecao = builder.Services.AddDataProtection()
    .SetApplicationName("piersec")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(pastaDados, "chaves")));
if (OperatingSystem.IsWindows())
    protecao.ProtectKeysWithDpapi();
builder.Services.AddSingleton<ICifrador, CifradorDataProtection>();

// Peças do núcleo. Para adicionar uma fonte nova: mais uma linha AddSingleton<IAdaptadorFonte, ...>.
var fuso = NormalizadorDatas.FusoBrasilia;
builder.Services.AddSingleton(regras);
builder.Services.AddSingleton(sp => new RepositorioLotes(pastaDados, sp.GetRequiredService<ICifrador>()));
builder.Services.AddSingleton(sp => new Anonimizador(ChaveAnonimizacao.Obter(pastaDados, sp.GetRequiredService<ICifrador>())));
builder.Services.AddSingleton(new NormalizadorDatas(fuso));
builder.Services.AddSingleton<IAdaptadorFonte, AdaptadorVulnerabilidades>();
builder.Services.AddSingleton<IAdaptadorFonte, AdaptadorAlertasSiem>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ProcessadorLote>();
builder.Services.AddSingleton<ValidadorLote>();
builder.Services.AddSingleton(new GeradorRelatorioHtml(fuso, regras));
builder.Services.AddSingleton(new ConversorPdf());
builder.Services.AddSingleton<CicloAnalise>();

// Acesso (RS1): usuários, auditoria e sessão por cookie protegido que expira.
builder.Services.AddSingleton(new RepositorioUsuarios(pastaDados));
builder.Services.AddSingleton<ServicoAutenticacao>();
builder.Services.AddSingleton(sp => new Auditoria(pastaDados, sp.GetRequiredService<TimeProvider>()));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Entrar";
        o.LogoutPath = "/Sair";
        o.AccessDeniedPath = "/Negado";
        o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        o.SlidingExpiration = true;
        o.Cookie.Name = "__Host-piersec";          // __Host-: só HTTPS, só este site, sem subdomínios
        o.Cookie.HttpOnly = true;                  // script nenhum consegue ler
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.Strict;   // outro site não consegue usar a sessão
    });
builder.Services.AddAuthorization(o =>
{
    // Nada abre sem autenticar, a não ser o que for liberado explicitamente.
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    o.AddPolicy("Admin", p => p.RequireRole(Papeis.Admin));
});
builder.Services.AddRazorPages(o =>
{
    o.Conventions.AllowAnonymousToPage("/Entrar");
    o.Conventions.AllowAnonymousToPage("/Erro");
    o.Conventions.AuthorizePage("/Usuarios", "Admin");
    o.Conventions.AuthorizePage("/Auditoria", "Admin");
});

// Limite de tentativas de login: no máximo 10 por minuto vindas do mesmo endereço.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        HttpMethods.IsPost(ctx.Request.Method) && ctx.Request.Path.StartsWithSegments("/Entrar")
            ? RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) })
            : RateLimitPartition.GetNoLimiter("livre"));
});

var app = builder.Build();

// Primeira execução: cria o "admin". Senha de ADMIN_SENHA_INICIAL (exatamente 8 caracteres) ou sorteada e mostrada UMA vez.
var (adminCriado, senhaSorteada) = app.Services.GetRequiredService<ServicoAutenticacao>()
    .GarantirAdmin(Environment.GetEnvironmentVariable(ServicoAutenticacao.VariavelSenhaInicial));
if (adminCriado)
{
    app.Services.GetRequiredService<Auditoria>().Registrar("sistema", "admin_criado", "primeira execução");
    Console.WriteLine(senhaSorteada is null
        ? $"\n  Usuário 'admin' criado com a senha inicial de {ServicoAutenticacao.VariavelSenhaInicial}. Troca obrigatória no primeiro acesso.\n"
        : $"\n  ====================================================================\n" +
          $"   USUÁRIO: admin    SENHA INICIAL: {senhaSorteada}\n" +
          $"   Anote agora: ela NÃO será mostrada de novo. Troca obrigatória no 1º acesso.\n" +
          $"  ====================================================================\n");
}

// "Esqueci a senha do admin": o rodar.ps1 -RedefinirSenhaAdmin liga esta variável. Nova senha temporária, mostrada UMA vez.
if (!adminCriado && Environment.GetEnvironmentVariable("PIERSEC_REDEFINIR_ADMIN") == "1")
{
    var nova = app.Services.GetRequiredService<ServicoAutenticacao>().RedefinirSenha(Papeis.Admin);
    app.Services.GetRequiredService<Auditoria>().Registrar("sistema", "senha_redefinida", "admin (pelo computador local)");
    Console.WriteLine(
        $"\n  ====================================================================\n" +
        $"   SENHA DO ADMIN REDEFINIDA\n" +
        $"   USUÁRIO: admin    SENHA TEMPORÁRIA: {nova}\n" +
        $"   Anote agora: ela NÃO será mostrada de novo. Troca obrigatória ao entrar.\n" +
        $"  ====================================================================\n");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Erro"); // em produção, erro nunca mostra detalhes técnicos
    app.UseHsts();
}

app.UseMiddleware<CabecalhosSeguranca>();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ExigirTrocaDeSenha>();

app.MapRazorPages();
app.MapGet("/lotes/{id}/relatorio.html", (string id, HttpContext ctx, RepositorioLotes repo, Auditoria auditoria) =>
    ArquivoRelatorio(ctx, repo, auditoria, id, "html", "text/html; charset=utf-8"));
app.MapGet("/lotes/{id}/relatorio.pdf", (string id, HttpContext ctx, RepositorioLotes repo, Auditoria auditoria) =>
    ArquivoRelatorio(ctx, repo, auditoria, id, "pdf", "application/pdf"));

// O rodar.ps1 liga esta variável para o navegador abrir sozinho quando o site estiver no ar.
if (Environment.GetEnvironmentVariable("PIERSEC_ABRIR_NAVEGADOR") == "1")
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var url = app.Urls.FirstOrDefault(u => u.StartsWith("https")) ?? app.Urls.FirstOrDefault();
        if (url is not null)
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    });
}

app.Logger.LogInformation("Dados da plataforma em {Pasta}", pastaDados);
app.Logger.LogInformation("Regras carregadas de {Pasta}: {Correlacoes} regra(s) de correlação", pastaRegras, regras.Correlacoes.Count);
app.Run();

static IResult ArquivoRelatorio(HttpContext ctx, RepositorioLotes repo, Auditoria auditoria, string id, string extensao, string tipo)
{
    if (!RepositorioLotes.IdValido(id) || repo.LerRelatorio(id, extensao) is not { } conteudo)
        return Results.NotFound();
    auditoria.Registrar(ctx.User.Identity?.Name, "relatorio_aberto", $"lote {id[..8]} · {extensao}");
    ctx.Response.Headers.ContentSecurityPolicy = CabecalhosSeguranca.CspRelatorio;
    return Results.Bytes(conteudo, tipo); // decifrado só na memória, na hora de enviar
}
