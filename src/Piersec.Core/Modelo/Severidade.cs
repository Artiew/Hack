namespace Piersec.Core.Modelo;

/// <summary>
/// Os quatro níveis exigidos pelo manual (RF4). O número serve só para ordenar: 4 = mais grave.
/// </summary>
public enum Severidade
{
    Baixa = 1,
    Media = 2,
    Alta = 3,
    Critica = 4,
}

public static class SeveridadeExtensoes
{
    /// <summary>Todos os níveis, do mais grave para o menos grave.</summary>
    public static readonly IReadOnlyList<Severidade> DoMaisGrave =
        [Severidade.Critica, Severidade.Alta, Severidade.Media, Severidade.Baixa];

    /// <summary>Nome exibido em todas as telas e no relatório (sempre o mesmo texto).</summary>
    public static string Nome(this Severidade severidade) => severidade switch
    {
        Severidade.Critica => "Crítica",
        Severidade.Alta => "Alta",
        Severidade.Media => "Média",
        Severidade.Baixa => "Baixa",
        _ => "Indefinida",
    };

    public static string Nome(this Severidade? severidade) =>
        severidade is { } s ? s.Nome() : "Sem severidade";

    /// <summary>
    /// "Ícone" em texto para a severidade continuar legível em preto e branco
    /// (o manual pede severidade com cor + ícone + texto).
    /// </summary>
    public static string Simbolo(this Severidade? severidade) => severidade switch
    {
        Severidade.Critica => "●●●●",
        Severidade.Alta => "●●●○",
        Severidade.Media => "●●○○",
        Severidade.Baixa => "●○○○",
        _ => "○○○○",
    };

    /// <summary>Classe CSS usada para a cor (ex.: "sev-critica").</summary>
    public static string ClasseCss(this Severidade? severidade) => severidade switch
    {
        Severidade.Critica => "sev-critica",
        Severidade.Alta => "sev-alta",
        Severidade.Media => "sev-media",
        Severidade.Baixa => "sev-baixa",
        _ => "sev-nenhuma",
    };
}
