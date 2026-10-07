using Piersec.Core.Consulta;

namespace Piersec.Web.Seguranca;

/// <summary>Monta os links da visão técnica com os filtros na URL (sempre escapados).</summary>
public static class Links
{
    public static string Eventos(string loteId, FiltroEventos filtro, int pagina = 1)
    {
        var partes = new List<string>();
        void Add(string nome, string? valor)
        {
            if (!string.IsNullOrWhiteSpace(valor))
                partes.Add($"{nome}={Uri.EscapeDataString(valor)}");
        }
        Add("fonte", filtro.Fonte);
        Add("severidade", filtro.Severidade);
        Add("tipo", filtro.Tipo);
        Add("ativo", filtro.Ativo);
        Add("servico", filtro.Servico);
        Add("ambiente", filtro.Ambiente);
        Add("arquivo", filtro.Arquivo);
        Add("regra", filtro.Regra);
        Add("correlacao", filtro.Correlacao);
        Add("acao", filtro.Acao?.ToString());
        Add("texto", filtro.Texto);
        if (pagina > 1)
            partes.Add($"pagina={pagina}");
        return $"/Lote/{loteId}/Eventos" + (partes.Count == 0 ? "" : "?" + string.Join("&", partes));
    }

    public static string Evento(string loteId, string eventoId) => $"/Lote/{loteId}/Evento/{Uri.EscapeDataString(eventoId)}";
}
