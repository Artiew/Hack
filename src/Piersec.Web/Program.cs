using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Piersec.Core.Analise;
using Piersec.Core.Armazenamento;
using Piersec.Core.Coleta;
using Piersec.Core.Padronizacao;
using Piersec.Core.Pipeline;
using Piersec.Core.Relatorio;
using Piersec.Core.Validacao;

var builder = WebApplication.CreateBuilder(args);

// Pasta dos dados gerados (lotes, relatórios, chave). Fica em var/, que está no .gitignore.
var pastaDados = Path.GetFullPath(builder.Configuration["Piersec:PastaDados"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", "var"));
Directory.CreateDirectory(pastaDados);

// Limite do envio inteiro (vários arquivos). O limite por arquivo é checado no ProcessadorLote.
const long LimiteRequisicaoBytes = 60L * 1024 * 1024;
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = LimiteRequisicaoBytes);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = LimiteRequisicaoBytes);

// Regras de severidade, correlação e frameworks (regras/*.json). Regra inválida impede a plataforma de subir.
var pastaRegras = Path.GetFullPath(builder.Configuration["Piersec:PastaRegras"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", "regras"));
var regras = ConjuntoRegras.Carregar(pastaRegras);

// Peças do núcleo. Para adicionar uma fonte nova: mais uma linha AddSingleton<IAdaptadorFonte, ...>.
var fuso = NormalizadorDatas.FusoBrasilia;
builder.Services.AddSingleton(regras);
builder.Services.AddSingleton(new RepositorioLotes(pastaDados));
builder.Services.AddSingleton(new Anonimizador(ChaveAnonimizacao.Obter(pastaDados)));
builder.Services.AddSingleton(new NormalizadorDatas(fuso));
builder.Services.AddSingleton<IAdaptadorFonte, AdaptadorVulnerabilidades>();
builder.Services.AddSingleton<IAdaptadorFonte, AdaptadorAlertasSiem>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ProcessadorLote>();
builder.Services.AddSingleton<ValidadorLote>();
builder.Services.AddSingleton(new GeradorRelatorioHtml(fuso, regras));
builder.Services.AddSingleton(new ConversorPdf());
builder.Services.AddSingleton<CicloAnalise>();
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Erro"); // em produção, erro nunca mostra detalhes técnicos

app.UseStaticFiles();
app.MapRazorPages();
app.MapGet("/lotes/{id}/relatorio.html", (string id, RepositorioLotes repo) => ArquivoRelatorio(repo, id, "html", "text/html; charset=utf-8"));
app.MapGet("/lotes/{id}/relatorio.pdf", (string id, RepositorioLotes repo) => ArquivoRelatorio(repo, id, "pdf", "application/pdf"));

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

static IResult ArquivoRelatorio(RepositorioLotes repo, string id, string extensao, string tipo)
{
    if (!RepositorioLotes.IdValido(id))
        return Results.NotFound();
    var caminho = repo.CaminhoRelatorio(id, extensao);
    if (!File.Exists(caminho))
        return Results.NotFound();
    return TypedResults.PhysicalFile(caminho, tipo);
}
