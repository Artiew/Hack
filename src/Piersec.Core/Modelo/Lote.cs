namespace Piersec.Core.Modelo;

/// <summary>
/// Algo que faltou, veio inválido ou limita a análise (RF7). Nada some em silêncio: vira lacuna declarada.
/// <paramref name="Linha"/> é nula quando a lacuna é do arquivo inteiro, da fonte ou do lote.
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

    /// <summary>Tipo de evento que a fonte produz (ex.: "vulnerabilidade"), para saber o que foi medido.</summary>
    public string? TipoEvento { get; init; }

    /// <summary>Por que o arquivo não foi processado (extensão, tamanho, cabeçalho desconhecido...).</summary>
    public string? MotivoNaoProcessado { get; init; }

    public int RegistrosLidos { get; init; }
    public int RegistrosRejeitados { get; init; }
    public int Lacunas { get; init; }

    /// <summary>Ajustes feitos na leitura (ex.: linhas desembrulhadas, datas remontadas), para transparência.</summary>
    public IReadOnlyList<string> Avisos { get; init; } = [];
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
    public IReadOnlyList<Correlacao> Correlacoes { get; init; } = [];
    public IReadOnlyList<RegraAvaliada> RegrasAvaliadas { get; init; } = [];
    public Avaliacao? Avaliacao { get; init; }
    public ComparacaoLote? Comparacao { get; init; }

    /// <summary>Indícios de que o lote mistura empresas ou ambientes diferentes (aviso, não erro).</summary>
    public IReadOnlyList<string> AvisosAmbiente { get; init; } = [];

    /// <summary>Verificações de consistência feitas antes de emitir o relatório (RF8).</summary>
    public ResultadoValidacao? Validacao { get; init; }

    /// <summary>Quantos eventos têm a severidade informada (null = sem severidade).</summary>
    public int ContarPorSeveridade(Severidade? severidade) =>
        Eventos.Count(e => e.Severidade == severidade);
}

/// <summary>Versão leve do lote para listas e comparação (não carrega os eventos).</summary>
public sealed record ResumoLote(
    string Id,
    DateTimeOffset CriadoEmUtc,
    int Arquivos,
    int Eventos,
    int Lacunas,
    int Correlacoes,
    double? Score = null,
    string? Faixa = null,
    IReadOnlyDictionary<string, double?>? Fatores = null,
    int? VerificacoesAprovadas = null,
    int? VerificacoesTotal = null)
{
    public bool Validado => VerificacoesTotal > 0 && VerificacoesAprovadas == VerificacoesTotal;

    public static ResumoLote De(Lote lote) => new(
        lote.Id, lote.CriadoEmUtc, lote.Arquivos.Count, lote.Eventos.Count, lote.Lacunas.Count, lote.Correlacoes.Count,
        lote.Avaliacao?.Score.Valor,
        lote.Avaliacao?.Score.Faixa,
        lote.Avaliacao?.Score.Fatores.ToDictionary(f => f.Id, f => f.Valor),
        lote.Validacao?.Aprovadas,
        lote.Validacao?.Total);
}
