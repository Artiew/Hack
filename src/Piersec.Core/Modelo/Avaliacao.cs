namespace Piersec.Core.Modelo;

/// <summary>Um fator do score (RF5). Valor nulo = não medido (sem dados), nunca zero.</summary>
public sealed record FatorScore
{
    public required string Id { get; init; }
    public required string Nome { get; init; }

    /// <summary>A pergunta que o fator responde, em linguagem simples.</summary>
    public required string Pergunta { get; init; }

    public required double Peso { get; init; }

    /// <summary>0 a 100 (100 = pior); null quando não há dados para medir.</summary>
    public double? Valor { get; init; }

    /// <summary>Quantos pontos do score final vieram deste fator (a soma dá o score).</summary>
    public double Contribuicao { get; init; }

    /// <summary>Como foi medido, ou por que não foi.</summary>
    public required string Explicacao { get; init; }
}

/// <summary>Score de risco geral, 0 = risco mínimo e 100 = risco máximo.</summary>
public sealed record ScoreRisco
{
    /// <summary>Nulo quando nenhum fator pôde ser medido (sem dados não é "risco zero").</summary>
    public double? Valor { get; init; }
    public required string Faixa { get; init; }
    public required string FraseFaixa { get; init; }
    public required IReadOnlyList<FatorScore> Fatores { get; init; }

    /// <summary>Parte do peso total (0 a 100%) que foi realmente medida.</summary>
    public required double PercentualMedido { get; init; }

    public required string Confianca { get; init; }
}

/// <summary>Um dos riscos principais, escrito para quem não é da área.</summary>
public sealed record RiscoNegocio
{
    public required string Titulo { get; init; }
    public required string OQueE { get; init; }
    public required string PorQueImporta { get; init; }
    public required string ComoResolver { get; init; }
    public required Severidade Severidade { get; init; }
    public IReadOnlyList<string> CorrelacaoIds { get; init; } = [];
}

public enum Nivel { Baixo = 1, Medio = 2, Alto = 3 }

/// <summary>Uma ação do plano de remediação, com impacto calculado (nunca inventado).</summary>
public sealed record AcaoRecomendada
{
    public required int Ordem { get; init; }
    public required string Tipo { get; init; }
    public required string Titulo { get; init; }

    /// <summary>A ação em poucas palavras, para frases-resumo (ex.: "atualizar openssl, java e chrome").</summary>
    public required string Resumo { get; init; }

    public required string ComoFazer { get; init; }

    /// <summary>Frase do impacto (ex.: "elimina 62% das falhas graves").</summary>
    public required string Impacto { get; init; }

    /// <summary>Parte das falhas graves eliminada por esta ação (0 a 100), quando dá para calcular.</summary>
    public double? PercentualFalhasGraves { get; init; }

    public required Nivel NivelImpacto { get; init; }
    public required Nivel Esforco { get; init; }
    public required int PrazoDias { get; init; }

    /// <summary>"Fazer agora", "Planejar", "Se sobrar tempo" ou "Não priorizar agora".</summary>
    public required string Quadrante { get; init; }

    public int Maquinas { get; init; }
    public IReadOnlyList<string> EventoIds { get; init; } = [];

    /// <summary>Quantos PONTOS do score esta ação derruba, recalculando o score sem os eventos que ela resolve.</summary>
    public double? QuedaScore { get; init; }

    /// <summary>A mesma queda em PORCENTAGEM do score atual ("corrigir isto melhora 18% do score").</summary>
    public double? PercentualScore { get; init; }

    /// <summary>Faixa de quanto custa fazer (premissa de regras/custos.json). Nula quando não há premissa para o tipo.</summary>
    public double? CustoMinimo { get; init; }
    public double? CustoMaximo { get; init; }
}

/// <summary>Score, riscos principais e plano de ação: o que o executivo precisa ler.</summary>
public sealed record Avaliacao
{
    public required ScoreRisco Score { get; init; }
    public required string FraseSituacao { get; init; }
    public required IReadOnlyList<RiscoNegocio> Riscos { get; init; }
    public required IReadOnlyList<AcaoRecomendada> Acoes { get; init; }

    /// <summary>Frase de uma linha: "atualizar N itens, em X dias, elimina Y% ...".</summary>
    public string? FraseUmaCoisa { get; init; }

    /// <summary>Score esperado se as ações que removem falhas forem feitas.</summary>
    public double? ScorePrevisto { get; init; }

    /// <summary>Os achados que mais pesam no score ("3 achados explicam X% do risco"), do que mais pesa para o que menos pesa.</summary>
    public IReadOnlyList<AchadoPrincipal> AchadosPrincipais { get; init; } = [];

    /// <summary>Quanto do score (0 a 100%) some se esses achados forem resolvidos juntos. Nulo quando não se aplica.</summary>
    public double? PercentualExplicado { get; init; }

    /// <summary>Score se esses achados forem resolvidos juntos.</summary>
    public double? ScoreSemAchados { get; init; }

    /// <summary>Quantas máquinas de cada tipo (ex.: "servidores" → 41).</summary>
    public required IReadOnlyDictionary<string, int> MaquinasPorTipo { get; init; }

    /// <summary>Faixa do custo de executar o plano inteiro (soma das ações). Nula quando não há premissa de custo.</summary>
    public double? CustoTotalMinimo { get; init; }
    public double? CustoTotalMaximo { get; init; }
}

/// <summary>Comparação com o lote anterior (tendência).</summary>
public sealed record ComparacaoLote(string LoteAnteriorId, DateTimeOffset DataAnterior, double ScoreAnterior, double Diferenca, string Motivo)
{
    /// <summary>Mesmos fatores com valores disponíveis nos dois lotes. Nulo quando o histórico não permite confirmar.</summary>
    public bool? MesmosFatoresMedidos { get; init; }
}

/// <summary>
/// Um dos problemas que mais pesam no score: um programa, uma máquina ou um grupo de alertas.
/// <paramref name="Descricao"/> é a versão técnica ("alertas da regra 81628 em FW-01"); <paramref name="Acao"/> é o que
/// o executivo lê ("Verificar os sinais de ataque repetidos registrados pelo firewall FW-01").
/// <paramref name="Pontos"/> = quanto o score cairia se só este problema fosse resolvido (calculado, não estimado).
/// </summary>
public sealed record AchadoPrincipal(string Descricao, double Pontos, int Eventos, string? Acao = null)
{
    /// <summary>A ação em linguagem simples; lotes antigos (sem ela) mostram a descrição.</summary>
    public string Texto => Acao ?? Descricao;
}
