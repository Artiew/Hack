using Piersec.Core.Modelo;

namespace Piersec.Core.Relatorio;

/// <summary>Lacunas iguais juntas: "data em formato não reconhecido — 37 linhas (ex.: 5, 9, 12)".</summary>
public sealed record GrupoLacuna(string Arquivo, string Campo, string Motivo, int Quantidade, IReadOnlyList<long> LinhasExemplo);

/// <summary>Quantos eventos de uma fonte, com certa severidade de origem, viraram cada severidade da plataforma.</summary>
public sealed record TransicaoSeveridade(string Fonte, string Origem, IReadOnlyDictionary<Severidade, int> PorSeveridade, int SemSeveridade, int Total);

/// <summary>Agrupamentos usados igual na tela e no relatório.</summary>
public static class Agrupamentos
{
    public static IReadOnlyList<GrupoLacuna> Lacunas(IEnumerable<Lacuna> lacunas, int exemplos = 5) =>
        lacunas.GroupBy(l => (l.Arquivo, l.Campo, l.Motivo))
            .Select(g => new GrupoLacuna(g.Key.Arquivo, g.Key.Campo, g.Key.Motivo, g.Count(),
                g.Where(l => l.Linha is not null).Select(l => l.Linha!.Value).Order().Take(exemplos).ToList()))
            .OrderByDescending(g => g.Quantidade).ThenBy(g => g.Arquivo, StringComparer.Ordinal).ThenBy(g => g.Motivo, StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<TransicaoSeveridade> Transicoes(IEnumerable<EventoSeguranca> eventos) =>
        eventos.GroupBy(e => (e.Fonte, Origem: e.SeveridadeOrigem ?? "(não informada)"))
            .Select(g => new TransicaoSeveridade(
                g.Key.Fonte,
                g.Key.Origem,
                SeveridadeExtensoes.DoMaisGrave.ToDictionary(s => s, s => g.Count(e => e.Severidade == s)),
                g.Count(e => e.Severidade is null),
                g.Count()))
            .OrderBy(t => t.Fonte, StringComparer.Ordinal).ThenByDescending(t => t.Total)
            .ToList();

    /// <summary>Ordem "mais grave primeiro" usada nas listas de eventos.</summary>
    public static IEnumerable<EventoSeguranca> MaisGraves(IEnumerable<EventoSeguranca> eventos) =>
        eventos.OrderByDescending(e => e.Severidade ?? 0)
            .ThenByDescending(e => e.Cvss ?? -1)
            .ThenByDescending(e => e.NivelOrigem ?? -1)
            .ThenByDescending(e => e.DataUtc)
            .ThenBy(e => e.Id, StringComparer.Ordinal);

    public static IEnumerable<Correlacao> CorrelacoesMaisGraves(IEnumerable<Correlacao> correlacoes) =>
        correlacoes.OrderByDescending(c => c.Severidade)
            .ThenByDescending(c => c.Ativos.Count)
            .ThenByDescending(c => c.EventoIds.Count)
            .ThenBy(c => c.Id, StringComparer.Ordinal);

    public static string Frameworks(Frameworks f)
    {
        static string Lista(IReadOnlyList<string> itens) => itens.Count == 0 ? "—" : string.Join(", ", itens);
        return $"NIST: {Lista(f.Nist)} · CIS: {Lista(f.Cis)} · ISO: {Lista(f.Iso)} · MITRE: {Lista(f.Mitre)}";
    }
}
