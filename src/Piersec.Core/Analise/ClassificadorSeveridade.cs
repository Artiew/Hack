using Piersec.Core.Modelo;
using Piersec.Core.Relatorio;

namespace Piersec.Core.Analise;

/// <summary>
/// Decide a severidade de cada evento (RF4) e escreve a justificativa em português,
/// inclusive "por que NÃO é crítica". Ordem: nota CVSS → nível do alerta → texto da fonte.
/// </summary>
public sealed class ClassificadorSeveridade(RegrasSeveridade regras)
{
    public (EventoSeguranca Evento, Lacuna? Lacuna) Classificar(EventoSeguranca evento)
    {
        if (evento.Cvss is { } nota)
            return (PorCvss(evento, nota), null);
        if (evento.NivelOrigem is { } nivel)
            return (PorNivel(evento, nivel), null);

        var porTexto = regras.ConverterTexto(evento.SeveridadeOrigem);
        if (porTexto is { } severidade)
        {
            var justificativa = $"Severidade informada pela fonte (\"{evento.SeveridadeOrigem}\"), convertida pela tabela de textos; não há nota técnica para refinar."
                + (severidade == Severidade.Critica ? "" : " Não é Crítica porque a própria fonte não a classificou assim.");
            return (evento with { Severidade = severidade, Justificativa = justificativa }, null);
        }

        var motivo = string.IsNullOrWhiteSpace(evento.SeveridadeOrigem)
            ? "severidade não informada"
            : "severidade de origem fora da tabela de conversão";
        var semClassificacao = evento with
        {
            Severidade = null,
            Justificativa = "Sem informação suficiente para classificar: não há nota CVSS, nível de alerta nem severidade reconhecível.",
        };
        return (semClassificacao, new Lacuna(evento.Evidencia.Arquivo, evento.Evidencia.Linha, "severidade", motivo));
    }

    private EventoSeguranca PorCvss(EventoSeguranca evento, double nota)
    {
        var faixa = regras.Cvss.Encontrar(nota);
        var texto = $"Nota CVSS {Num(nota)}: {faixa.Severidade.Nome()} ({faixa.Descricao} na escala CVSS).";

        var daFonte = regras.ConverterTexto(evento.SeveridadeOrigem);
        if (daFonte is { } s && s != faixa.Severidade)
            texto += $" A fonte marcou \"{evento.SeveridadeOrigem}\", mas a nota separa falhas que a fonte classifica igual.";
        if (faixa.Severidade != Severidade.Critica)
            texto += $" Não é Crítica porque a nota está abaixo de {Num(regras.Cvss.MinimoPara(Severidade.Critica))}.";

        return evento with { Severidade = faixa.Severidade, Justificativa = texto };
    }

    private EventoSeguranca PorNivel(EventoSeguranca evento, int nivel)
    {
        var faixa = regras.NivelAlerta.Encontrar(nivel);
        var texto = $"Alerta de nível {nivel} (escala de 0 a 15 do SIEM): {faixa.Severidade.Nome()} — {faixa.Descricao}.";
        if (faixa.Severidade != Severidade.Critica)
            texto += $" Não é Crítica porque só o nível {regras.NivelAlerta.MinimoPara(Severidade.Critica):0} ou acima é tratado como ataque grave.";
        return evento with { Severidade = faixa.Severidade, Justificativa = texto };
    }

    private static string Num(double valor) => valor.ToString("0.0", Formatar.PtBr);
}
