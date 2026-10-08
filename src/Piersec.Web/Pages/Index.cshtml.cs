using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Armazenamento;
using Piersec.Core.Modelo;
using Piersec.Core.Pipeline;
using Piersec.Core.Seguranca;
using Piersec.Web.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>
/// Visão geral: enviar arquivos (arrastar e soltar, com progresso por etapa) e a lista de lotes.
/// Com JavaScript, o envio roda em segundo plano (handlers Iniciar e Estado); sem JavaScript, o formulário normal
/// faz o mesmo ciclo e abre o resultado no fim.
/// </summary>
public sealed class IndexModel(CicloAnalise ciclo, RepositorioLotes repositorio, Auditoria auditoria, ServicoEnvios envios,
    LotesEmMemoria lotes, ILogger<IndexModel> log) : PageModel
{
    public IReadOnlyList<ResumoLote> Lotes { get; private set; } = [];
    public string? Erro { get; private set; }

    public IActionResult OnGet()
    {
        Lotes = repositorio.Listar();

        // O executivo não escolhe arquivo nem lote: entra e já cai no painel da análise mais recente.
        // As análises anteriores continuam acessíveis pelo histórico dentro do próprio painel.
        //
        // Antes de mandar para lá, confere se o lote ABRE. Um lote gravado com outra chave (P-019) deixaria o
        // executivo preso numa tela de erro logo depois do login, sem lista para onde voltar. Se nenhum abrir,
        // ele fica nesta página mesmo, que pelo menos mostra as análises existentes.
        if (!Sessao.PodeEnviar(User) && PrimeiroQueAbre() is { } recente)
            return RedirectToPage("/Lote", new { id = recente });

        return Page();
    }

    private string? PrimeiroQueAbre()
    {
        foreach (var resumo in Lotes.Where(l => l.Score is not null).Concat(Lotes).DistinctBy(l => l.Id).Take(3))
        {
            try
            {
                if (lotes.Obter(resumo.Id) is not null)
                    return resumo.Id;
            }
            catch (Exception ex)
            {
                // Não esconder o problema: registra e tenta o próximo. Sem dado sensível no log.
                log.LogWarning(ex, "Lote {Lote} não pôde ser aberto para o painel do executivo.", resumo.Id);
            }
        }
        return null;
    }

    // Razor Pages valida o token anti-CSRF automaticamente em todo POST (inclusive os do JavaScript).
    public async Task<IActionResult> OnPostAsync(List<IFormFile>? arquivos, CancellationToken ct)
    {
        if (await Receber(arquivos, ct) is not { } enviados)
        {
            if (!Sessao.PodeEnviar(User))
                return Forbid();
            Lotes = repositorio.Listar();
            return Page();
        }

        var resultado = await ciclo.ExecutarAsync(enviados, ct);

        // Só números no log: nada de conteúdo dos arquivos (RS4).
        log.LogInformation("Lote {Id}: {Arquivos} arquivo(s), {Eventos} evento(s), {Lacunas} lacuna(s). PDF: {Pdf}",
            resultado.Lote.Id, resultado.Lote.Arquivos.Count, resultado.Lote.Eventos.Count,
            resultado.Lote.Lacunas.Count, resultado.MensagemPdf);
        var validacao = resultado.Lote.Validacao;
        auditoria.Registrar(User.Identity?.Name, "lote_processado",
            $"lote {resultado.Lote.Id[..8]} · {resultado.Lote.Arquivos.Count} arquivo(s) · verificações {validacao?.Aprovadas}/{validacao?.Total}");

        return RedirectToPage("/Lote", new { id = resultado.Lote.Id });
    }

    /// <summary>JavaScript: começa a análise em segundo plano e devolve o número de acompanhamento.</summary>
    public async Task<IActionResult> OnPostIniciarAsync(List<IFormFile>? arquivos, CancellationToken ct)
    {
        if (await Receber(arquivos, ct) is not { } enviados)
            return Sessao.PodeEnviar(User) ? BadRequest(new { erro = Erro }) : Forbid();
        var (id, erro) = envios.Iniciar(User.Identity!.Name!, enviados);
        return id is null ? StatusCode(StatusCodes.Status429TooManyRequests, new { erro }) : new JsonResult(new { id });
    }

    /// <summary>JavaScript: em que etapa está o envio (só para quem enviou).</summary>
    public IActionResult OnGetEstado(string? id) =>
        id is not null && envios.Estado(id, User.Identity?.Name ?? "") is { } estado ? new JsonResult(estado) : NotFound();

    /// <summary>Confere permissão e lê os arquivos para a memória. Null = não seguir (Erro explica, ou não tem permissão).</summary>
    private async Task<List<ArquivoEnviado>?> Receber(List<IFormFile>? arquivos, CancellationToken ct)
    {
        if (!Sessao.PodeEnviar(User))
        {
            auditoria.Registrar(User.Identity?.Name, "envio_negado");
            return null; // executivo só consulta
        }
        if (arquivos is null || arquivos.Count == 0)
        {
            Erro = "Selecione ao menos um arquivo .csv.";
            return null;
        }

        var enviados = new List<ArquivoEnviado>(arquivos.Count);
        foreach (var arquivo in arquivos)
        {
            if (arquivo.Length > ProcessadorLote.TamanhoMaximoBytes)
            {
                enviados.Add(new ArquivoEnviado(arquivo.FileName, arquivo.Length, null)); // recusado sem ler
                continue;
            }
            using var memoria = new MemoryStream((int)arquivo.Length);
            await arquivo.CopyToAsync(memoria, ct);
            enviados.Add(ArquivoEnviado.De(arquivo.FileName, memoria.ToArray()));
        }
        return enviados;
    }
}
