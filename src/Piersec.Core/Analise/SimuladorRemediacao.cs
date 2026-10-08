using Piersec.Core.Coleta;
using Piersec.Core.Modelo;
using Piersec.Core.Padronizacao;

namespace Piersec.Core.Analise;

public sealed record SimulacaoRemediacao(ScoreRisco Score, int Acoes, int Falhas, int Maquinas, int PrazoDias,
    IReadOnlyDictionary<Nivel, int> Esforcos);

/// <summary>Projeção somente em memória: união das falhas corrigidas, sem gravar lote nem executar ações.</summary>
public sealed class SimuladorRemediacao(ConjuntoRegras regras)
{
    public static bool PodeSimular(AcaoRecomendada acao) => acao.EventoIds.Count > 0 &&
        acao.Tipo is PlanejadorAvaliacao.AcaoAtualizarProgramas or PlanejadorAvaliacao.AcaoCorrigirMaquinas;

    public SimulacaoRemediacao Simular(Lote lote, IEnumerable<int> ordens)
    {
        var avaliacao = lote.Avaliacao ?? throw new ArgumentException("Este lote não tem avaliação.");
        var selecionadas = ordens.Distinct().ToHashSet();
        var acoes = avaliacao.Acoes.Where(a => selecionadas.Contains(a.Ordem) && PodeSimular(a)).ToList();
        if (acoes.Count != selecionadas.Count)
            throw new ArgumentException("Selecione somente as correções disponíveis neste lote.");

        var ids = acoes.SelectMany(a => a.EventoIds).ToHashSet(StringComparer.Ordinal);
        var removidas = lote.Eventos.Where(e => e.Tipo == TiposEvento.Vulnerabilidade && ids.Contains(e.Id)).ToList();
        var idsRemovidos = removidas.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var restantes = lote.Eventos.Where(e => !idsRemovidos.Contains(e.Id)).ToList();
        var tiposMedidos = lote.Arquivos.Where(a => a.TipoEvento is not null).Select(a => a.TipoEvento!).ToHashSet();
        var populacao = lote.Eventos.Where(e => e.Tipo == TiposEvento.Vulnerabilidade && e.Ativo != ContextoConversao.NaoInformado)
            .GroupBy(e => e.Ativo, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().TipoAtivo, StringComparer.Ordinal);
        var score = acoes.Count == 0 ? avaliacao.Score
            : new CalculadoraScore(regras.Score, regras.Ativos).Calcular(restantes, tiposMedidos, populacao);
        return new(score, acoes.Count, removidas.Count, removidas.Select(e => e.Ativo).Distinct(StringComparer.Ordinal).Count(),
            acoes.Count == 0 ? 0 : acoes.Max(a => a.PrazoDias), acoes.GroupBy(a => a.Esforco).ToDictionary(g => g.Key, g => g.Count()));
    }
}
