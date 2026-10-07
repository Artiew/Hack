using Piersec.Core.Coleta;
using Piersec.Core.Modelo;
using Piersec.Core.Padronizacao;

namespace Piersec.Core.Analise;

/// <summary>Faixa de prejuízo de um serviço, se um incidente o atingir (premissa declarada, não dado da empresa).</summary>
public sealed record ImpactoServico(decimal Minimo, decimal Maximo, string Premissa);

/// <summary>Um serviço da empresa e como reconhecer as máquinas dele (regras/servicos.json).</summary>
public sealed record DefinicaoServico
{
    public required string Id { get; init; }
    public required string Nome { get; init; }

    /// <summary>Quanto o serviço importa para o negócio (Alta, Media, Baixa).</summary>
    public required Severidade Criticidade { get; init; }

    /// <summary>Pedaços de nome de máquina que indicam o serviço (sem acento, sem diferença de maiúsculas).</summary>
    public IReadOnlyList<string> NomeContem { get; init; } = [];

    /// <summary>Tipos de máquina (ativos.json) que caem neste serviço quando o nome não diz nada.</summary>
    public IReadOnlyList<string> TipoAtivo { get; init; } = [];

    /// <summary>Nulo = sem premissa de prejuízo (aparece como "não estimado").</summary>
    public ImpactoServico? Impacto { get; init; }
}

/// <summary>regras/servicos.json: mapa máquina → serviço da empresa e faixas de prejuízo.</summary>
public sealed record RegrasServicos
{
    public required string Premissa { get; init; }
    public required string PremissaImpacto { get; init; }

    /// <summary>A partir desta nota (0 a 100) o serviço conta como "em risco" e entra no prejuízo potencial.</summary>
    public required double FaixaEmRiscoAPartirDe { get; init; }

    public required IReadOnlyList<DefinicaoServico> Servicos { get; init; }

    public DefinicaoServico? Definicao(string? id) => Servicos.FirstOrDefault(s => s.Id == id);

    public IEnumerable<string> Problemas(RegrasAtivos ativos)
    {
        if (Servicos.Count == 0)
            yield return "precisa de pelo menos um serviço";
        foreach (var id in Servicos.GroupBy(s => s.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            yield return $"serviço com id repetido: {id}";
        if (FaixaEmRiscoAPartirDe is < 0 or > 100)
            yield return "'faixaEmRiscoAPartirDe' precisa estar entre 0 e 100";
        foreach (var s in Servicos)
        {
            if (s.NomeContem.Count == 0 && s.TipoAtivo.Count == 0)
                yield return $"serviço {s.Id}: precisa de 'nomeContem' ou 'tipoAtivo'";
            foreach (var tipo in s.TipoAtivo.Where(t => !ativos.Tipos.ContainsKey(t)))
                yield return $"serviço {s.Id}: o tipo '{tipo}' não existe em ativos.json";
            if (s.Impacto is { } i && (i.Minimo < 0 || i.Maximo < i.Minimo || string.IsNullOrWhiteSpace(i.Premissa)))
                yield return $"serviço {s.Id}: 'impacto' precisa de mínimo ≥ 0, máximo ≥ mínimo e uma premissa";
        }
        // Toda máquina precisa cair em algum serviço: cada tipo de máquina tem que estar em pelo menos uma regra.
        foreach (var tipo in ativos.Tipos.Keys.Where(t => !Servicos.Any(s => s.TipoAtivo.Contains(t))))
            yield return $"nenhum serviço recebe máquinas do tipo '{tipo}' (elas ficariam sem serviço)";
    }
}

/// <summary>Liga cada máquina a um serviço: primeiro pelo nome, depois pelo tipo. A primeira regra que combinar vence.</summary>
public sealed class ClassificadorServicos(RegrasServicos regras)
{
    private readonly Dictionary<(string, string?), string> _cache = [];

    public string Classificar(string ativo, string? tipoAtivo)
    {
        if (_cache.TryGetValue((ativo, tipoAtivo), out var id))
            return id;
        var nome = Texto.NormalizarChave(ativo);
        var tipo = tipoAtivo ?? RegrasAtivos.Desconhecido;
        id = regras.Servicos.FirstOrDefault(s =>
                 s.NomeContem.Any(p => nome.Contains(Texto.NormalizarChave(p), StringComparison.Ordinal)) || s.TipoAtivo.Contains(tipo))?.Id
             ?? regras.Servicos[^1].Id; // não acontece com regras válidas (todo tipo tem serviço)
        _cache[(ativo, tipoAtivo)] = id;
        return id;
    }
}

/// <summary>
/// Risco de cada serviço da empresa e o prejuízo potencial em faixas. A nota do serviço usa as mesmas contas do score:
/// média das notas das máquinas (pior falha + acúmulo) e, para quem só tem alertas, o peso médio dos alertas.
/// </summary>
public sealed class AnalisadorServicos(ConjuntoRegras regras)
{
    private readonly CalculadoraScore _calculadora = new(regras.Score, regras.Ativos);

    public (IReadOnlyList<RiscoServico> Servicos, ResumoImpacto? Impacto) Analisar(IReadOnlyList<EventoSeguranca> eventos)
    {
        var servicos = new List<RiscoServico>();
        var porServico = eventos.Where(e => e.Ativo != ContextoConversao.NaoInformado && e.Servico is not null)
            .GroupBy(e => e.Servico!).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var def in regras.Servicos.Servicos)
        {
            if (!porServico.TryGetValue(def.Id, out var evs))
                continue;
            var vulnerabilidades = evs.Where(e => e.Tipo == TiposEvento.Vulnerabilidade).ToList();
            var alertas = evs.Where(e => e.Tipo == TiposEvento.Alerta && e.Severidade is not null).ToList();

            double? notaFalhas = vulnerabilidades.Count == 0 ? null
                : vulnerabilidades.GroupBy(e => e.Ativo, StringComparer.Ordinal).Average(g => _calculadora.PontosDaMaquina(g));
            double? notaAlertas = alertas.Count == 0 ? null
                : 100 * alertas.Average(e => regras.Score.PesoPorAlerta[e.Severidade!.Value]);
            double? nota = (notaFalhas, notaAlertas) switch
            {
                (null, null) => null,
                ({ } f, null) => f,
                (null, { } a) => a,
                ({ } f, { } a) => Math.Max(f, a),
            };

            servicos.Add(new RiscoServico
            {
                Id = def.Id,
                Nome = def.Nome,
                Criticidade = def.Criticidade,
                Maquinas = evs.Select(e => e.Ativo).Distinct(StringComparer.Ordinal).Count(),
                MaquinasComFalhaGrave = vulnerabilidades.Where(e => e.Severidade >= Severidade.Alta).Select(e => e.Ativo).Distinct(StringComparer.Ordinal).Count(),
                AlertasRelevantes = alertas.Count(e => regras.Score.PesoPorAlerta[e.Severidade!.Value] > 0),
                Nota = nota is { } n ? Math.Round(n, 1) : null,
                Faixa = nota is { } v ? regras.Score.FaixaDe(v).Nome : "Sem dados",
                EmRisco = nota >= regras.Servicos.FaixaEmRiscoAPartirDe,
                Impacto = def.Impacto,
                Eventos = evs.Count,
            });
        }

        var ordenados = servicos
            .OrderByDescending(s => s.EmRisco).ThenByDescending(s => s.Criticidade).ThenByDescending(s => s.Nota ?? -1)
            .ThenBy(s => s.Nome, StringComparer.Ordinal)
            .ToList();

        var comPrejuizo = ordenados.Where(s => s.EmRisco && s.Impacto is not null).ToList();
        var impacto = comPrejuizo.Count == 0 ? null
            : new ResumoImpacto(comPrejuizo.Sum(s => s.Impacto!.Minimo), comPrejuizo.Sum(s => s.Impacto!.Maximo), comPrejuizo.Count);
        return (ordenados, impacto);
    }
}
