using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Piersec.Core.Modelo;
using Piersec.Core.Operacao;
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
    public required RegrasAutomacao Automacao { get; init; }
    public required RegrasServicos Servicos { get; init; }
    public required RegrasMitre Mitre { get; init; }
    public required RegrasCustos Custos { get; init; }

    public static ConjuntoRegras Carregar(string pasta)
    {
        var severidade = Ler<SeveridadeJson>(pasta, "severidade.json");
        var correlacao = Ler<CorrelacaoJson>(pasta, "correlacao.json");
        var frameworks = Ler<FrameworksJson>(pasta, "frameworks.json");
        var ativos = Ler<RegrasAtivos>(pasta, "ativos.json");
        var score = Ler<RegrasScore>(pasta, "score.json");
        var plano = Ler<RegrasPlano>(pasta, "plano.json");
        var automacao = Ler<RegrasAutomacao>(pasta, "automacao.json");
        var servicos = Ler<RegrasServicos>(pasta, "servicos.json");
        var mitre = Ler<RegrasMitre>(pasta, "mitre.json");
        var custos = Ler<RegrasCustos>(pasta, "custos.json");

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
            Automacao = automacao,
            Servicos = servicos,
            Mitre = mitre,
            Custos = custos,
        };
        regras.Validar();
        return regras;
    }

    private void Validar()
    {
        ValidarMitre();
        ValidarCustos();

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

        if (Servicos.Problemas(Ativos).FirstOrDefault() is { } problemaServicos)
            throw Erro("servicos.json", problemaServicos);
        if (Automacao.Problemas(Ativos).FirstOrDefault() is { } problemaAutomacao)
            throw Erro("automacao.json", problemaAutomacao);

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

    /// <summary>
    /// O custo é premissa declarada, então a premissa é OBRIGATÓRIA: faixa sem explicação viraria número mágico.
    /// </summary>
    private void ValidarCustos()
    {
        if (Custos.HoraTecnica.Minimo <= 0 || Custos.HoraTecnica.Maximo < Custos.HoraTecnica.Minimo)
            throw Erro("custos.json", "'horaTecnica' precisa de 'minimo' maior que zero e 'maximo' maior ou igual a ele");
        if (string.IsNullOrWhiteSpace(Custos.HoraTecnica.Premissa))
            throw Erro("custos.json", "'horaTecnica' precisa declarar a premissa (de onde veio o valor)");
        if (Custos.HorasPorAcao.Count == 0)
            throw Erro("custos.json", "falta 'horasPorAcao' com pelo menos um tipo de ação");

        foreach (var (tipo, h) in Custos.HorasPorAcao)
        {
            if (h.FixoMinimo < 0 || h.PorMaquinaMinimo < 0)
                throw Erro("custos.json", $"'{tipo}' não pode ter horas negativas");
            if (h.FixoMaximo < h.FixoMinimo || h.PorMaquinaMaximo < h.PorMaquinaMinimo)
                throw Erro("custos.json", $"em '{tipo}', todo máximo precisa ser maior ou igual ao mínimo");
            if (string.IsNullOrWhiteSpace(h.Premissa))
                throw Erro("custos.json", $"'{tipo}' precisa declarar a premissa das horas");
        }
    }

    /// <summary>
    /// Nenhuma técnica inventada: tudo que as regras citam precisa existir no catálogo, e toda confiança
    /// declarada precisa ser uma das três previstas. Regra errada impede a plataforma de subir.
    /// </summary>
    private void ValidarMitre()
    {
        if (Mitre.Catalogo.Count == 0)
            throw Erro("mitre.json", "falta 'catalogo' com pelo menos uma técnica");

        foreach (var (codigo, tecnica) in Mitre.Catalogo)
        {
            if (!codigo.StartsWith('T') || codigo.Length < 5)
                throw Erro("mitre.json", $"'{codigo}' não parece um código de técnica (esperado algo como T1190)");
            foreach (var m in tecnica.Mitigacoes)
                if (!Mitre.Mitigacoes.ContainsKey(m))
                    throw Erro("mitre.json", $"a técnica {codigo} cita a mitigação '{m}', que não está em 'mitigacoes'");
        }

        foreach (var (tipo, doTipo) in Mitre.PorTipo)
        {
            if (!ConfiancaMitre.Todas.Contains(doTipo.Confianca))
                throw Erro("mitre.json", $"o tipo '{tipo}' usa a confiança '{doTipo.Confianca}'; use observada, provavel ou possivel");
            if (doTipo.Tecnicas.Count > 0 && string.IsNullOrWhiteSpace(doTipo.Premissa))
                throw Erro("mitre.json", $"o tipo '{tipo}' aponta técnicas sem declarar a premissa");
            foreach (var t in doTipo.Tecnicas)
                if (!Mitre.Catalogo.ContainsKey(t))
                    throw Erro("mitre.json", $"o tipo '{tipo}' cita a técnica '{t}', que não está no catálogo");
        }

        foreach (var termos in Mitre.PorDescricaoAlerta)
        {
            if (!Mitre.Catalogo.ContainsKey(termos.Tecnica))
                throw Erro("mitre.json", $"'porDescricaoAlerta' cita a técnica '{termos.Tecnica}', que não está no catálogo");
            if (termos.Contem.Count == 0)
                throw Erro("mitre.json", $"a técnica '{termos.Tecnica}' está em 'porDescricaoAlerta' sem nenhum termo em 'contem'");
        }

        foreach (var regra in Correlacoes)
            foreach (var t in regra.Frameworks.Mitre)
                if (!Mitre.Catalogo.ContainsKey(t))
                    throw Erro("correlacao.json", $"a regra '{regra.Id}' cita a técnica MITRE '{t}', que não está em mitre.json");

        foreach (var (tipo, f) in FrameworksPorTipo)
            foreach (var t in f.Mitre)
                if (!Mitre.Catalogo.ContainsKey(t))
                    throw Erro("frameworks.json", $"o tipo '{tipo}' cita a técnica MITRE '{t}', que não está em mitre.json");
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
