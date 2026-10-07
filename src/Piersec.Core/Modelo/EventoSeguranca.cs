namespace Piersec.Core.Modelo;

/// <summary>De onde veio o evento: arquivo, impressão digital (SHA-256) do arquivo e linha (RG2).</summary>
public sealed record Evidencia(string Arquivo, string Sha256Arquivo, long Linha);

/// <summary>
/// Modelo comum de evento (RF2): toda fonte, seja qual for o formato, vira este mesmo registro.
/// Campos que podem faltar são anuláveis; quando faltam, o adaptador registra uma <see cref="Lacuna"/>.
/// </summary>
public sealed record EventoSeguranca
{
    /// <summary>Id gerado pela plataforma, único dentro do lote (ex.: EV-00001).</summary>
    public required string Id { get; init; }

    /// <summary>Id original na fonte, quando existir.</summary>
    public string? IdOrigem { get; init; }

    public required string Fonte { get; init; }
    public required string Tipo { get; init; }

    /// <summary>Nome do ativo já normalizado (maiúsculas, sem espaços sobrando).</summary>
    public required string Ativo { get; init; }

    /// <summary>Usuário anonimizado na entrada (LGPD / RS5). O nome real nunca é guardado.</summary>
    public string? UsuarioAnonimo { get; init; }

    /// <summary>Texto da severidade como veio da fonte, para mostrar a conversão.</summary>
    public string? SeveridadeOrigem { get; init; }

    /// <summary>Severidade na escala da plataforma; nula quando não foi possível converter.</summary>
    public Severidade? Severidade { get; init; }

    /// <summary>Data e hora em UTC (um só fuso para todas as fontes).</summary>
    public DateTimeOffset? DataUtc { get; init; }

    public required string Descricao { get; init; }
    public required Evidencia Evidencia { get; init; }
}
