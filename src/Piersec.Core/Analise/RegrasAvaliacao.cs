using Piersec.Core.Modelo;

namespace Piersec.Core.Analise;

/// <summary>regras/ativos.json: como deduzir o tipo de cada máquina.</summary>
public sealed record RegrasAtivos
{
    public required string Premissa { get; init; }
    public required IReadOnlyList<RegraTipoAtivo> Regras { get; init; }
    public required string TipoDosAlertas { get; init; }
    public required IReadOnlyDictionary<string, TipoAtivo> Tipos { get; init; }

    public const string Desconhecido = "desconhecido";

    public TipoAtivo Tipo(string? id) =>
        id is not null && Tipos.TryGetValue(id, out var t) ? t : Tipos[Desconhecido];
}

public sealed record RegraTipoAtivo(IReadOnlyList<string> SistemaContem, string Tipo);

public sealed record TipoAtivo(string Singular, string Plural, double PesoNoScore, string PorQue);

/// <summary>regras/score.json: fatores, pesos, pontos e faixas do score.</summary>
public sealed record RegrasScore
{
    public required string Direcao { get; init; }
    public required string Descricao { get; init; }
    public required IReadOnlyList<DefinicaoFator> Fatores { get; init; }
    /// <summary>Nota inicial da máquina pela sua pior falha: basta UMA falha crítica conhecida para alguém entrar.</summary>
    public required IReadOnlyDictionary<Severidade, double> NotaPelaPiorFalha { get; init; }

    /// <summary>Pontos a mais por cada falha além da pior (acúmulo).</summary>
    public required double PontosPorFalhaAdicional { get; init; }

    public required double TetoPorMaquina { get; init; }
    public required IReadOnlyDictionary<Severidade, double> PesoPorAlerta { get; init; }
    public required IReadOnlyList<FaixaScore> Faixas { get; init; }
    public required IReadOnlyList<NivelConfianca> Confianca { get; init; }

    public FaixaScore FaixaDe(double valor) => Faixas.OrderByDescending(f => f.Minimo).First(f => valor >= f.Minimo);

    public string ConfiancaDe(double percentualMedido) =>
        Confianca.OrderByDescending(c => c.MinimoPercentualMedido).First(c => percentualMedido >= c.MinimoPercentualMedido).Nome;
}

public sealed record DefinicaoFator(string Id, string Nome, string Pergunta, double Peso, string? TipoDeEvento, string SemDados);
public sealed record FaixaScore(double Minimo, string Nome, string Frase);
public sealed record NivelConfianca(double MinimoPercentualMedido, string Nome);

/// <summary>regras/plano.json: prazos e limites usados no plano de ação.</summary>
public sealed record RegrasPlano
{
    public required string Descricao { get; init; }
    public required IReadOnlyDictionary<Severidade, int> PrazoDias { get; init; }
    public required int PrazoInvestigacaoDias { get; init; }
    public required int PrazoMedicaoDias { get; init; }
    public required int ProgramasNaPrimeiraAcao { get; init; }
    public required int MaquinasNaSegundaAcao { get; init; }
    public required LimitesEsforco Esforco { get; init; }
    public required double ImpactoAltoAPartirDePercentual { get; init; }
    public required double ImpactoMedioAPartirDePercentual { get; init; }

    public Nivel EsforcoDe(int atualizacoes) =>
        atualizacoes <= Esforco.BaixoAteAtualizacoes ? Nivel.Baixo
        : atualizacoes <= Esforco.MedioAteAtualizacoes ? Nivel.Medio
        : Nivel.Alto;

    public Nivel ImpactoDe(double percentual) =>
        percentual >= ImpactoAltoAPartirDePercentual ? Nivel.Alto
        : percentual >= ImpactoMedioAPartirDePercentual ? Nivel.Medio
        : Nivel.Baixo;
}

public sealed record LimitesEsforco(int BaixoAteAtualizacoes, int MedioAteAtualizacoes);
