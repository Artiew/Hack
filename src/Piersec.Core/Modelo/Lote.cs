namespace Piersec.Core.Modelo;

/// <summary>
/// Algo que faltou ou veio inválido (RF7). Nada some em silêncio: vira lacuna declarada no relatório.
/// <paramref name="Linha"/> é nula quando a lacuna é do arquivo inteiro ou do lote.
/// </summary>
public sealed record Lacuna(string Arquivo, long? Linha, string Campo, string Motivo);

/// <summary>O que aconteceu com cada arquivo enviado (aparece na tela e no relatório).</summary>
public sealed record ResultadoArquivo
{
    public required string Nome { get; init; }
    public required long TamanhoBytes { get; init; }

    /// <summary>SHA-256 do conteúdo; nulo quando o arquivo foi recusado antes de ser lido.</summary>
    public string? Sha256 { get; init; }

    /// <summary>Fonte reconhecida; nula quando o arquivo não foi processado.</summary>
    public string? Fonte { get; init; }

    /// <summary>Por que o arquivo não foi processado (extensão, tamanho, cabeçalho desconhecido...).</summary>
    public string? MotivoNaoProcessado { get; init; }

    public int RegistrosLidos { get; init; }
    public int RegistrosRejeitados { get; init; }
    public int Lacunas { get; init; }
}

/// <summary>
/// Os arquivos enviados juntos formam um lote, analisado em conjunto (um relatório consolidado).
/// </summary>
public sealed record Lote
{
    /// <summary>Guid em 32 caracteres hexadecimais (formato "N").</summary>
    public required string Id { get; init; }
    public required DateTimeOffset CriadoEmUtc { get; init; }
    public required IReadOnlyList<ResultadoArquivo> Arquivos { get; init; }
    public required IReadOnlyList<EventoSeguranca> Eventos { get; init; }
    public required IReadOnlyList<Lacuna> Lacunas { get; init; }

    /// <summary>Quantos eventos têm a severidade informada (null = sem severidade).</summary>
    public int ContarPorSeveridade(Severidade? severidade) =>
        Eventos.Count(e => e.Severidade == severidade);
}
