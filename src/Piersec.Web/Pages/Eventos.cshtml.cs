using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Analise;
using Piersec.Core.Consulta;
using Piersec.Core.Modelo;
using Piersec.Web.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>
/// Visão técnica: todos os eventos do lote, filtráveis. É o destino dos números clicáveis da tela do lote
/// (número → eventos que o formam → evento → arquivo e linha de origem). Só admin e analista.
/// </summary>
public sealed class EventosModel(LotesEmMemoria lotes, ConjuntoRegras regras) : PageModel
{
    public const int PorPagina = 50;

    [BindProperty(SupportsGet = true)] public string? Fonte { get; set; }
    [BindProperty(SupportsGet = true)] public string? Severidade { get; set; }
    [BindProperty(SupportsGet = true)] public string? Tipo { get; set; }
    [BindProperty(SupportsGet = true)] public string? Ativo { get; set; }
    [BindProperty(SupportsGet = true)] public string? Servico { get; set; }
    [BindProperty(SupportsGet = true)] public string? Ambiente { get; set; }
    [BindProperty(SupportsGet = true)] public string? Arquivo { get; set; }
    [BindProperty(SupportsGet = true)] public string? Regra { get; set; }
    [BindProperty(SupportsGet = true)] public string? Correlacao { get; set; }
    [BindProperty(SupportsGet = true)] public int? Acao { get; set; }
    [BindProperty(SupportsGet = true)] public string? Texto { get; set; }
    [BindProperty(SupportsGet = true)] public int Pagina { get; set; } = 1;

    public Lote Lote { get; private set; } = null!;
    public FiltroEventos Filtro { get; private set; } = new();
    public IReadOnlyList<EventoSeguranca> Encontrados { get; private set; } = [];
    public IReadOnlyList<EventoSeguranca> NaPagina { get; private set; } = [];

    /// <summary>Mesmos filtros, mas sem o de severidade: os contadores do topo viram atalhos para trocar de severidade.</summary>
    public IReadOnlyList<EventoSeguranca> SemFiltroDeSeveridade { get; private set; } = [];

    public ConjuntoRegras Regras => regras;
    public int TotalPaginas { get; private set; }

    public IActionResult OnGet(string id)
    {
        if (lotes.Obter(id) is not { } lote)
            return NotFound();
        Lote = lote;
        Filtro = new FiltroEventos
        {
            Fonte = Fonte, Severidade = Severidade, Tipo = Tipo, Ativo = Ativo, Servico = Servico, Ambiente = Ambiente,
            Arquivo = Arquivo, Regra = Regra, Correlacao = Correlacao, Acao = Acao, Texto = Texto,
        }.Limpo();
        Encontrados = ConsultaEventos.Filtrar(lote, Filtro);
        SemFiltroDeSeveridade = Filtro.Severidade is null ? Encontrados : ConsultaEventos.Filtrar(lote, Filtro with { Severidade = null });
        TotalPaginas = Math.Max(1, (int)Math.Ceiling(Encontrados.Count / (double)PorPagina));
        Pagina = Math.Clamp(Pagina, 1, TotalPaginas);
        NaPagina = Encontrados.Skip((Pagina - 1) * PorPagina).Take(PorPagina).ToList();
        return Page();
    }

    /// <summary>Link para a mesma lista em outra página, mantendo os filtros.</summary>
    public string LinkPagina(int pagina) => Links.Eventos(Lote.Id, Filtro, pagina);
}
