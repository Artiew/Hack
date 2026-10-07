namespace Piersec.Core.Modelo;

/// <summary>Referências de frameworks de segurança ligadas a uma regra ou tipo de evento.</summary>
public sealed record Frameworks
{
    public IReadOnlyList<string> Mitre { get; init; } = [];
    public IReadOnlyList<string> Nist { get; init; } = [];
    public IReadOnlyList<string> Cis { get; init; } = [];
    public IReadOnlyList<string> Iso { get; init; } = [];

    public static Frameworks Vazio { get; } = new();
}

/// <summary>
/// Uma relação encontrada por uma regra de correlação (RF3): quais eventos, quais ativos,
/// por quê (justificativa em português) e quais controles de frameworks se aplicam.
/// </summary>
public sealed record Correlacao
{
    /// <summary>Id no lote (ex.: COR-00001).</summary>
    public required string Id { get; init; }

    public required string RegraId { get; init; }

    /// <summary>O que foi agrupado (ex.: a CVE ou o pacote). Mesma entrada → mesma chave.</summary>
    public required string Chave { get; init; }

    public required string Titulo { get; init; }
    public required string Justificativa { get; init; }
    public required Severidade Severidade { get; init; }
    public required IReadOnlyList<string> Ativos { get; init; }
    public required IReadOnlyList<string> EventoIds { get; init; }
    public required Frameworks Frameworks { get; init; }
}

/// <summary>
/// Resultado de cada regra no lote, inclusive das que não encontraram nada
/// (o manual pede mostrar também o que não correlacionou, e por quê).
/// </summary>
public sealed record RegraAvaliada(string RegraId, string Nome, int Ocorrencias, string? MotivoSemOcorrencia);
