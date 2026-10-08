namespace Piersec.Core.Analise;

/// <summary>Uma técnica do catálogo MITRE ATT&amp;CK (regras/mitre.json), explicada em português para leigo.</summary>
public sealed record TecnicaMitre
{
    public required string Nome { get; init; }
    public required string Tatica { get; init; }
    public required string Explicacao { get; init; }
    public IReadOnlyList<string> Mitigacoes { get; init; } = [];
}

/// <summary>Técnicas assumidas para um tipo de evento, com a premissa declarada. Nunca afirmadas como observadas.</summary>
public sealed record MitreDoTipo
{
    public IReadOnlyList<string> Tecnicas { get; init; } = [];
    public string Confianca { get; init; } = ConfiancaMitre.Possivel;
    public string? Premissa { get; init; }
}

/// <summary>Termos do texto do alerta que apontam para uma técnica (dedução da plataforma, não da fonte).</summary>
public sealed record TermosMitre
{
    public required string Tecnica { get; init; }
    public IReadOnlyList<string> Contem { get; init; } = [];
}

/// <summary>
/// Quanto a plataforma confia na técnica apontada. Anda sempre junto da técnica, na tela e no relatório:
/// sem isso, um palpite pareceria um fato.
/// </summary>
public static class ConfiancaMitre
{
    public const string Observada = "observada";
    public const string Provavel = "provavel";
    public const string Possivel = "possivel";

    public static readonly IReadOnlyList<string> Todas = [Observada, Provavel, Possivel];

    /// <summary>Texto para o leitor, com acento.</summary>
    public static string Descricao(string confianca) => confianca switch
    {
        Observada => "observada",
        Provavel => "provável",
        _ => "possível",
    };
}

/// <summary>Catálogo MITRE ATT&amp;CK e as deduções permitidas (regras/mitre.json).</summary>
public sealed record RegrasMitre
{
    public string? Descricao { get; init; }
    public IReadOnlyDictionary<string, string> Confiancas { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, TecnicaMitre> Catalogo { get; init; } = new Dictionary<string, TecnicaMitre>();
    public IReadOnlyDictionary<string, string> Mitigacoes { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, MitreDoTipo> PorTipo { get; init; } = new Dictionary<string, MitreDoTipo>();
    public IReadOnlyList<TermosMitre> PorDescricaoAlerta { get; init; } = [];

    public static RegrasMitre Vazio { get; } = new();
}
