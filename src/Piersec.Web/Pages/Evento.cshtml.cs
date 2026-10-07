using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Analise;
using Piersec.Core.Consulta;
using Piersec.Core.Modelo;
using Piersec.Core.Pipeline;
using Piersec.Core.Seguranca;
using Piersec.Web.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>
/// Um evento por inteiro: o que a plataforma leu, como classificou, onde está no arquivo de origem e em que correlações entra.
/// Aqui o analista pode "discordar da severidade": isso cria uma REVISÃO do lote (o original não muda).
/// </summary>
public sealed class EventoModel(LotesEmMemoria lotes, ConjuntoRegras regras, CicloAnalise ciclo, Auditoria auditoria) : PageModel
{
    public Lote Lote { get; private set; } = null!;
    public EventoSeguranca Evento { get; private set; } = null!;
    public IReadOnlyList<Correlacao> Correlacoes { get; private set; } = [];
    public RiscoServico? Servico { get; private set; }
    public AjusteSeveridade? Ajuste { get; private set; }
    public ConjuntoRegras Regras => regras;

    [BindProperty] public Severidade? NovaSeveridade { get; set; }
    [BindProperty] public string? Motivo { get; set; }
    public string? Erro { get; private set; }

    public IActionResult OnGet(string id, string eventoId) => Carregar(id, eventoId) ? Page() : NotFound();

    // Razor Pages valida o token anti-CSRF em todo POST; a política "Tecnico" já barrou o executivo.
    public async Task<IActionResult> OnPostDiscordarAsync(string id, string eventoId, CancellationToken ct)
    {
        if (!Carregar(id, eventoId))
            return NotFound();
        if (NovaSeveridade is not { } para || !Enum.IsDefined(para))
        {
            Erro = "Escolha a severidade que você acha correta.";
            return Page();
        }
        var usuario = User.Identity?.Name ?? Auditoria.Desconhecido;
        var resultado = await ciclo.RevisarAsync(Lote, Evento.Id, para, Motivo, usuario, ct);
        if (!resultado.Ok)
        {
            Erro = resultado.Mensagem;
            return Page();
        }
        var revisao = resultado.Ciclo!.Lote;
        // Na auditoria: ids e severidades. O motivo fica no lote (cifrado) e no relatório, já filtrado.
        auditoria.Registrar(usuario, "severidade_ajustada",
            $"lote {Lote.Id[..8]} → revisão {revisao.Id[..8]} · {Evento.Id} · {Evento.Severidade.Nome()} → {para.Nome()}");
        return Redirect($"/Lote/{revisao.Id}");
    }

    private bool Carregar(string id, string eventoId)
    {
        if (lotes.Obter(id) is not { } lote || lote.Eventos.FirstOrDefault(e => e.Id == eventoId) is not { } evento)
            return false;
        Lote = lote;
        Evento = evento;
        Correlacoes = ConsultaEventos.CorrelacoesDo(lote, evento.Id);
        Servico = lote.Servicos.FirstOrDefault(s => s.Id == evento.Servico);
        Ajuste = lote.Ajustes.LastOrDefault(a => a.EventoId == evento.Id);
        return true;
    }
}
