using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Piersec.Core.Modelo;
using Piersec.Core.Padronizacao;

namespace Piersec.Core.Analise;

/// <summary>Uma faixa de uma escala numérica (ex.: CVSS a partir de 9,0 = Crítica).</summary>
public sealed record FaixaSeveridade(double Minimo, Severidade Severidade, string Descricao);

/// <summary>Escala numérica convertida em severidade. As faixas ficam do maior mínimo para o menor.</summary>
public sealed record EscalaFaixas(string Fonte, IReadOnlyList<FaixaSeveridade> Faixas)
{
    public FaixaSeveridade Encontrar(double valor) => Faixas.First(f => valor >= f.Minimo);

    /// <summary>Menor valor que ainda dá esta severidade (ex.: 9,0 para Crítica no CVSS).</summary>
    public double MinimoPara(Severidade severidade) =>
        Faixas.Where(f => f.Severidade == severidade).Select(f => f.Minimo).DefaultIfEmpty(double.NaN).Min();
}

public sealed class RegrasSeveridade
{
    private readonly Dictionary<string, Severidade> _porTexto;

    public RegrasSeveridade(string descricao, IReadOnlyDictionary<Severidade, IReadOnlyList<string>> textos, EscalaFaixas cvss, EscalaFaixas nivelAlerta)
    {
        Descricao = descricao;
        Textos = textos;
        Cvss = cvss;
        NivelAlerta = nivelAlerta;
        _porTexto = textos.SelectMany(p => p.Value.Select(t => (Chave: Texto.NormalizarChave(t), Severidade: p.Key)))
            .ToDictionary(x => x.Chave, x => x.Severidade);
    }

    public string Descricao { get; }
    public IReadOnlyDictionary<Severidade, IReadOnlyList<string>> Textos { get; }
    public EscalaFaixas Cvss { get; }
    public EscalaFaixas NivelAlerta { get; }

    public Severidade? ConverterTexto(string? texto) =>
        _porTexto.TryGetValue(Texto.NormalizarChave(texto), out var s) ? s : null;
}

/// <summary>Controles de frameworks de um tipo de evento, com observação (ex.: por que não há MITRE).</summary>
public sealed record FrameworksDoTipo
{
    public IReadOnlyList<string> Mitre { get; init; } = [];
    public IReadOnlyList<string> Nist { get; init; } = [];
    public IReadOnlyList<string> Cis { get; init; } = [];
    public IReadOnlyList<string> Iso { get; init; } = [];
    public string? Observacao { get; init; }
}

/// <summary>Uma regra de correlação lida de regras/correlacao.json.</summary>
public sealed record RegraCorrelacao
{
    public const string SeveridadeMaiorDosEventos = "maior_dos_eventos";

    public required string Id { get; init; }
    public required string Nome { get; init; }
    public required string Tipo { get; init; }
    public required string Titulo { get; init; }
    public required string Justificativa { get; init; }

    /// <summary>"maior_dos_eventos" ou o nome de uma severidade (ex.: "Critica").</summary>
    public required string Severidade { get; init; }

    public Dictionary<string, JsonElement> Parametros { get; init; } = new();
    public Frameworks Frameworks { get; init; } = Frameworks.Vazio;

    public double Numero(string nome) =>
        Parametros.TryGetValue(nome, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble()
            : throw new InvalidOperationException($"Regra {Id}: parâmetro numérico '{nome}' ausente ou inválido.");

    public Severidade SeveridadeParametro(string nome) =>
        Parametros.TryGetValue(nome, out var v) && v.ValueKind == JsonValueKind.String && Enum.TryParse<Severidade>(v.GetString(), out var s)
            ? s
            : throw new InvalidOperationException($"Regra {Id}: parâmetro '{nome}' precisa ser Critica, Alta, Media ou Baixa.");
}

/// <summary>
/// Todas as regras da plataforma, lidas da pasta regras/ e validadas na inicialização.
/// Regra com erro impede a plataforma de subir (melhor falhar cedo do que analisar errado).
/// </summary>
public sealed partial class ConjuntoRegras
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public required RegrasSeveridade Severidade { get; init; }
    public required IReadOnlyList<RegraCorrelacao> Correlacoes { get; init; }
    public required IReadOnlyDictionary<string, FrameworksDoTipo> FrameworksPorTipo { get; init; }
    public required IReadOnlyDictionary<string, string> NomesControles { get; init; }
    public required RegrasAtivos Ativos { get; init; }
    public required RegrasScore Score { get; init; }
    public required RegrasPlano Plano { get; init; }

    public static ConjuntoRegras Carregar(string pasta)
    {
        var severidade = Ler<SeveridadeJson>(pasta, "severidade.json");
        var correlacao = Ler<CorrelacaoJson>(pasta, "correlacao.json");
        var frameworks = Ler<FrameworksJson>(pasta, "frameworks.json");
        var ativos = Ler<RegrasAtivos>(pasta, "ativos.json");
        var score = Ler<RegrasScore>(pasta, "score.json");
        var plano = Ler<RegrasPlano>(pasta, "plano.json");

        var regras = new ConjuntoRegras
        {
            Severidade = new RegrasSeveridade(
                severidade.Descricao ?? "",
                (severidade.Textos ?? throw Erro("severidade.json", "falta 'textos'"))
                    .ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value),
                Escala(severidade.Cvss, "cvss"),
                Escala(severidade.NivelAlerta, "nivelAlerta")),
            Correlacoes = correlacao.Regras ?? throw Erro("correlacao.json", "falta 'regras'"),
            FrameworksPorTipo = frameworks.PorTipo ?? new Dictionary<string, FrameworksDoTipo>(),
            NomesControles = frameworks.Controles ?? new Dictionary<string, string>(),
            Ativos = ativos,
            Score = score,
            Plano = plano,
        };
        regras.Validar();
        return regras;
    }

    private void Validar()
    {
        if (!Ativos.Tipos.ContainsKey(RegrasAtivos.Desconhecido))
            throw Erro("ativos.json", $"'tipos' precisa ter '{RegrasAtivos.Desconhecido}'");
        foreach (var tipo in Ativos.Regras.Select(r => r.Tipo).Append(Ativos.TipoDosAlertas))
            if (!Ativos.Tipos.ContainsKey(tipo))
                throw Erro("ativos.json", $"o tipo '{tipo}' é usado mas não está em 'tipos'");

        if (Score.Fatores.Count == 0 || Score.Fatores.Any(f => f.Peso <= 0))
            throw Erro("score.json", "precisa de fatores, todos com peso maior que zero");
        if (Score.Fatores.GroupBy(f => f.Id).Any(g => g.Count() > 1))
            throw Erro("score.json", "fatores com id repetido");
        foreach (var s in SeveridadeExtensoes.DoMaisGrave)
        {
            if (!Score.NotaPelaPiorFalha.ContainsKey(s) || !Score.PesoPorAlerta.ContainsKey(s))
                throw Erro("score.json", $"'notaPelaPiorFalha' e 'pesoPorAlerta' precisam ter a severidade {s}");
            if (!Plano.PrazoDias.ContainsKey(s))
                throw Erro("plano.json", $"'prazoDias' precisa ter a severidade {s}");
        }
        if (Score.Faixas.Min(f => f.Minimo) != 0 || Score.Confianca.Min(c => c.MinimoPercentualMedido) != 0)
            throw Erro("score.json", "a menor faixa e o menor nível de confiança precisam começar em 0");

        foreach (var s in SeveridadeExtensoes.DoMaisGrave)
            if (!Severidade.Textos.ContainsKey(s))
                throw Erro("severidade.json", $"'textos' não tem a severidade {s}");

        var repetidos = Correlacoes.GroupBy(r => r.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (repetidos.Count > 0)
            throw Erro("correlacao.json", $"ids repetidos: {string.Join(", ", repetidos)}");

        foreach (var regra in Correlacoes)
        {
            if (!MotorCorrelacao.CamposPorTipo.TryGetValue(regra.Tipo, out var campos))
                throw Erro("correlacao.json", $"regra {regra.Id}: tipo '{regra.Tipo}' não existe");
            if (regra.Severidade != RegraCorrelacao.SeveridadeMaiorDosEventos && !Enum.TryParse<Severidade>(regra.Severidade, out _))
                throw Erro("correlacao.json", $"regra {regra.Id}: severidade '{regra.Severidade}' inválida");
            foreach (var texto in new[] { regra.Titulo, regra.Justificativa })
                foreach (Match m in Marcador().Matches(texto))
                    if (!campos.Contains(m.Groups[1].Value))
                        throw Erro("correlacao.json", $"regra {regra.Id}: o texto usa {{{m.Groups[1].Value}}}, que não existe para o tipo '{regra.Tipo}'");
            MotorCorrelacao.ValidarParametros(regra); // lança se faltar parâmetro
        }
    }

    private static EscalaFaixas Escala(EscalaJson? json, string nome)
    {
        if (json?.Faixas is not { Count: > 0 } faixas)
            throw Erro("severidade.json", $"'{nome}' precisa de faixas");
        var ordenadas = faixas.OrderByDescending(f => f.Minimo)
            .Select(f => new FaixaSeveridade(f.Minimo, f.Severidade, f.Descricao ?? "")).ToList();
        if (ordenadas[^1].Minimo != 0)
            throw Erro("severidade.json", $"a menor faixa de '{nome}' precisa começar em 0");
        return new EscalaFaixas(json.Fonte ?? "", ordenadas);
    }

    private static T Ler<T>(string pasta, string arquivo)
    {
        var caminho = Path.Combine(pasta, arquivo);
        if (!File.Exists(caminho))
            throw new InvalidOperationException($"Arquivo de regras não encontrado: {caminho}");
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(caminho), Json) ?? throw Erro(arquivo, "arquivo vazio");
        }
        catch (JsonException ex)
        {
            throw Erro(arquivo, $"JSON inválido ({ex.Message})");
        }
    }

    private static InvalidOperationException Erro(string arquivo, string motivo) =>
        new($"Regras inválidas em {arquivo}: {motivo}.");

    [GeneratedRegex(@"\{([a-zA-Z]+)\}")]
    internal static partial Regex Marcador();

    private sealed record SeveridadeJson(string? Descricao, Dictionary<Severidade, List<string>>? Textos, EscalaJson? Cvss, EscalaJson? NivelAlerta);
    private sealed record EscalaJson(string? Fonte, List<FaixaJson>? Faixas);
    private sealed record FaixaJson(double Minimo, Severidade Severidade, string? Descricao);
    private sealed record CorrelacaoJson(List<RegraCorrelacao>? Regras);
    private sealed record FrameworksJson(Dictionary<string, FrameworksDoTipo>? PorTipo, Dictionary<string, string>? Controles);
}
