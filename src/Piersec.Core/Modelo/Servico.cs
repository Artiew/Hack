using Piersec.Core.Analise;

namespace Piersec.Core.Modelo;

/// <summary>Como está um serviço da empresa neste lote (visão executiva: "o faturamento está em risco alto").</summary>
public sealed record RiscoServico
{
    public required string Id { get; init; }
    public required string Nome { get; init; }
    public required Severidade Criticidade { get; init; }
    public required int Maquinas { get; init; }
    public required int MaquinasComFalhaGrave { get; init; }
    public required int AlertasRelevantes { get; init; }

    /// <summary>0 a 100 (100 = pior), com as mesmas contas do score; nulo sem dados.</summary>
    public double? Nota { get; init; }

    /// <summary>Faixa da nota (mesmos nomes do score: Crítico, Alto, Elevado…).</summary>
    public required string Faixa { get; init; }

    /// <summary>Nota a partir do limite de regras/servicos.json: entra no prejuízo potencial.</summary>
    public required bool EmRisco { get; init; }

    public ImpactoServico? Impacto { get; init; }
    public required int Eventos { get; init; }
}

/// <summary>Prejuízo potencial somando os serviços em risco (faixa, nunca um número exato).</summary>
public sealed record ResumoImpacto(decimal Minimo, decimal Maximo, int ServicosEmRisco);
