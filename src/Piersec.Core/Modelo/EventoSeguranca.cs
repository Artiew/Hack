namespace Piersec.Core.Modelo;

/// <summary>De onde veio o evento: arquivo, impressão digital (SHA-256) do arquivo e linha (RG2).</summary>
public sealed record Evidencia(string Arquivo, string Sha256Arquivo, long Linha);

/// <summary>Tipos de evento conhecidos (chave usada nas regras e no mapa de frameworks).</summary>
public static class TiposEvento
{
    public const string Vulnerabilidade = "vulnerabilidade";
    public const string Alerta = "alerta";
}

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

    /// <summary>Ver <see cref="TiposEvento"/>.</summary>
    public required string Tipo { get; init; }

    /// <summary>Ambiente de origem. Premissa da Fase 2: o nome do arquivo (os dados não trazem cliente/ambiente).</summary>
    public required string Ambiente { get; init; }

    /// <summary>Nome do ativo já normalizado (maiúsculas, sem espaços sobrando).</summary>
    public required string Ativo { get; init; }

    /// <summary>Usuário anonimizado na entrada (LGPD / RS5). O nome real nunca é guardado.</summary>
    public string? UsuarioAnonimo { get; init; }

    /// <summary>Texto da severidade como veio da fonte, para mostrar a conversão.</summary>
    public string? SeveridadeOrigem { get; init; }

    /// <summary>Nota CVSS (0 a 10) da vulnerabilidade, quando a fonte informa.</summary>
    public double? Cvss { get; init; }

    /// <summary>Nível numérico do alerta na fonte (ex.: 0 a 15 no SIEM).</summary>
    public int? NivelOrigem { get; init; }

    /// <summary>Identificador público da falha (ex.: CVE-2024-1234).</summary>
    public string? Cve { get; init; }

    public string? Pacote { get; init; }
    public string? VersaoPacote { get; init; }
    public string? SistemaOperacional { get; init; }

    /// <summary>Id da regra que gerou o alerta na fonte.</summary>
    public string? RegraOrigem { get; init; }

    /// <summary>Severidade na escala da plataforma; nula quando não foi possível classificar.</summary>
    public Severidade? Severidade { get; init; }

    /// <summary>Por que esta severidade (RF4 / RG4), em português para leigo.</summary>
    public string? Justificativa { get; init; }

    /// <summary>Data e hora em UTC (um só fuso para todas as fontes).</summary>
    public DateTimeOffset? DataUtc { get; init; }

    /// <summary>Resumo curto do evento.</summary>
    public required string Descricao { get; init; }

    /// <summary>Texto técnico completo da fonte, quando houver.</summary>
    public string? Detalhe { get; init; }

    /// <summary>Link de referência da fonte. Só exibido como texto; a plataforma nunca acessa.</summary>
    public string? Referencia { get; init; }

    public required Evidencia Evidencia { get; init; }
}
