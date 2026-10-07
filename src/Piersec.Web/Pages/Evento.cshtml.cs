using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Analise;
using Piersec.Core.Consulta;
using Piersec.Core.Modelo;
using Piersec.Web.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>Um evento por inteiro: o que a plataforma leu, como classificou, onde está no arquivo de origem e em que correlações entra.</summary>
public sealed class EventoModel(LotesEmMemoria lotes, ConjuntoRegras regras) : PageModel
{
    public Lote Lote { get; private set; } = null!;
    public EventoSeguranca Evento { get; private set; } = null!;
    public IReadOnlyList<Correlacao> Correlacoes { get; private set; } = [];
    public RiscoServico? Servico { get; private set; }
    public ConjuntoRegras Regras => regras;

    public IActionResult OnGet(string id, string eventoId)
    {
        if (lotes.Obter(id) is not { } lote || lote.Eventos.FirstOrDefault(e => e.Id == eventoId) is not { } evento)
            return NotFound();
        Lote = lote;
        Evento = evento;
        Correlacoes = ConsultaEventos.CorrelacoesDo(lote, evento.Id);
        Servico = lote.Servicos.FirstOrDefault(s => s.Id == evento.Servico);
        return Page();
    }
}
