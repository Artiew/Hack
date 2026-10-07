using Piersec.Core.Modelo;

namespace Piersec.Core.Padronizacao;

/// <summary>
/// Converte o texto de severidade de cada fonte para a escala da plataforma.
/// Fase 1: tabela fixa no código. Na Fase 2 ela vai para regras/severidade.json.
/// A tabela inteira aparece na metodologia do relatório.
/// </summary>
public static class ConversorSeveridade
{
    public static readonly IReadOnlyDictionary<string, Severidade> Tabela = new Dictionary<string, Severidade>
    {
        ["critica"] = Severidade.Critica,
        ["critico"] = Severidade.Critica,
        ["critical"] = Severidade.Critica,
        ["alta"] = Severidade.Alta,
        ["alto"] = Severidade.Alta,
        ["high"] = Severidade.Alta,
        ["media"] = Severidade.Media,
        ["medio"] = Severidade.Media,
        ["moderada"] = Severidade.Media,
        ["medium"] = Severidade.Media,
        ["moderate"] = Severidade.Media,
        ["baixa"] = Severidade.Baixa,
        ["baixo"] = Severidade.Baixa,
        ["low"] = Severidade.Baixa,
        ["informativa"] = Severidade.Baixa,
        ["info"] = Severidade.Baixa,
        ["informational"] = Severidade.Baixa,
    };

    public static Severidade? Converter(string? texto) =>
        Tabela.TryGetValue(Texto.NormalizarChave(texto), out var severidade) ? severidade : null;
}
