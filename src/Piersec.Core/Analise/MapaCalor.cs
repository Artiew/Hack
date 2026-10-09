using Piersec.Core.Modelo;

namespace Piersec.Core.Analise;

/// <summary>Um quadrado do mapa: quantos problemas daquele serviço naquela gravidade existem hoje e quantos sobram.</summary>
public sealed record CelulaCalor(Severidade Severidade, int Hoje, int Restantes);

public sealed record LinhaCalor(string ServicoId, string Nome, string Faixa, IReadOnlyList<CelulaCalor> Celulas);

/// <summary>
/// Mapa de calor serviço × gravidade do painel: cada quadrado conta os eventos daquele serviço naquela gravidade.
/// Com uma simulação, "Restantes" desconta os eventos que as correções marcadas resolveriam (os mesmos que o
/// simulador tira do score), e o painel mostra o mapa esfriando junto com o ponteiro.
/// Evento sem serviço ou sem gravidade não entra: ele já aparece como lacuna.
/// </summary>
public static class MapaCalor
{
    public static readonly IReadOnlyList<Severidade> Colunas = [Severidade.Critica, Severidade.Alta, Severidade.Media, Severidade.Baixa];

    public static IReadOnlyList<LinhaCalor> Montar(Lote lote, IReadOnlySet<string>? resolvidos = null)
    {
        resolvidos ??= new HashSet<string>(StringComparer.Ordinal);
        var contagem = lote.Eventos
            .Where(e => e.Servico is not null && e.Severidade is not null)
            .GroupBy(e => (Servico: e.Servico!, Severidade: e.Severidade!.Value))
            .ToDictionary(g => g.Key, g => (Hoje: g.Count(), Restantes: g.Count(e => !resolvidos.Contains(e.Id))));

        return lote.Servicos
            .OrderByDescending(s => s.Nota ?? -1).ThenBy(s => s.Nome, StringComparer.Ordinal)
            .Select(s => new LinhaCalor(s.Id, s.Nome, s.Faixa, Colunas
                .Select(sev => contagem.TryGetValue((s.Id, sev), out var c) ? new CelulaCalor(sev, c.Hoje, c.Restantes) : new CelulaCalor(sev, 0, 0))
                .ToList()))
            .ToList();
    }

    /// <summary>
    /// Intensidade da cor, de 0 a 1, em escala logarítmica: com 10 e com 10.000 problemas no mesmo mapa, os dois
    /// quadrados precisam continuar distinguíveis. A escala é sempre a de HOJE, para o mapa esfriar ao simular.
    /// </summary>
    public static double Intensidade(int valor, int maximoHoje) =>
        valor <= 0 || maximoHoje <= 0 ? 0 : Math.Min(1, Math.Log(1 + valor) / Math.Log(1 + maximoHoje));
}
