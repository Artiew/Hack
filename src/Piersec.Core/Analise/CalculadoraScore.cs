using Piersec.Core.Coleta;
using Piersec.Core.Modelo;
using Piersec.Core.Relatorio;

namespace Piersec.Core.Analise;

/// <summary>Deduz o tipo da máquina (servidor, computador de funcionário...) pelas regras de regras/ativos.json.</summary>
public sealed class ClassificadorAtivos(RegrasAtivos regras)
{
    public string Classificar(EventoSeguranca evento)
    {
        if (evento.Tipo == TiposEvento.Alerta)
            return regras.TipoDosAlertas;
        if (string.IsNullOrWhiteSpace(evento.SistemaOperacional))
            return RegrasAtivos.Desconhecido;
        foreach (var regra in regras.Regras)
            if (regra.SistemaContem.Any(p => evento.SistemaOperacional.Contains(p, StringComparison.OrdinalIgnoreCase)))
                return regra.Tipo;
        return RegrasAtivos.Desconhecido;
    }
}

/// <summary>
/// Score de risco 0–100 (RF5). Média ponderada só dos fatores MEDIDOS; fator sem dados aparece como
/// "não medido" e baixa a confiança, mas nunca entra como zero. A soma das contribuições é o score.
/// </summary>
public sealed class CalculadoraScore(RegrasScore regras, RegrasAtivos ativos)
{
    /// <param name="maquinasConhecidas">
    /// Para projeções ("e se corrigirmos?"): máquinas (nome → tipo) que devem continuar na média
    /// mesmo sem falhas restantes, contando 0. Sem isso, máquina corrigida sumiria da conta.
    /// </param>
    public ScoreRisco Calcular(IReadOnlyList<EventoSeguranca> eventos, IReadOnlySet<string> tiposMedidos,
        IReadOnlyDictionary<string, string?>? maquinasConhecidas = null)
    {
        var brutos = regras.Fatores.Select(def =>
        {
            var medido = def.TipoDeEvento is not null && tiposMedidos.Contains(def.TipoDeEvento);
            var (valor, explicacao) = (def.Id, medido) switch
            {
                ("vulnerabilidades", true) => Vulnerabilidades(eventos, maquinasConhecidas),
                ("ameacas", true) => Ameacas(eventos),
                _ => ((double?)null, $"Não medido: {def.SemDados}."),
            };
            return new FatorScore
            {
                Id = def.Id,
                Nome = def.Nome,
                Pergunta = def.Pergunta,
                Peso = def.Peso,
                Valor = valor is { } v ? Math.Round(v, 1) : null,
                Explicacao = explicacao,
            };
        }).ToList();

        var pesoTotal = brutos.Sum(f => f.Peso);
        var pesoMedido = brutos.Where(f => f.Valor is not null).Sum(f => f.Peso);
        var fatores = brutos.Select(f => f with
        {
            Contribuicao = f.Valor is { } v && pesoMedido > 0 ? Math.Round(f.Peso * v / pesoMedido, 1) : 0,
        }).ToList();

        var percentualMedido = Math.Round(100 * pesoMedido / pesoTotal, 1);
        if (pesoMedido == 0)
        {
            return new ScoreRisco
            {
                Valor = null,
                Faixa = "Não calculado",
                FraseFaixa = "Não há dados suficientes para calcular o risco. Isso NÃO significa que o ambiente esteja seguro.",
                Fatores = fatores,
                PercentualMedido = 0,
                Confianca = regras.ConfiancaDe(0),
            };
        }

        var valor = Math.Round(fatores.Where(f => f.Valor is not null).Sum(f => f.Peso * f.Valor!.Value) / pesoMedido, 1);
        var faixa = regras.FaixaDe(valor);
        return new ScoreRisco
        {
            Valor = valor,
            Faixa = faixa.Nome,
            FraseFaixa = faixa.Frase,
            Fatores = fatores,
            PercentualMedido = percentualMedido,
            Confianca = regras.ConfiancaDe(percentualMedido),
        };
    }

    /// <summary>
    /// Risco de uma máquina (0 a 100): começa na nota da sua pior falha e ganha pontos por falha adicional
    /// (uma por CVE), até o teto. Uma única falha crítica já deixa a máquina em risco alto.
    /// </summary>
    public double PontosDaMaquina(IEnumerable<EventoSeguranca> falhasDaMaquina)
    {
        var falhas = falhasDaMaquina.Where(e => e.Severidade is not null).DistinctBy(e => e.Cve ?? e.Id).ToList();
        if (falhas.Count == 0)
            return 0;
        var pior = falhas.Max(e => e.Severidade!.Value);
        return Math.Min(regras.TetoPorMaquina, regras.NotaPelaPiorFalha[pior] + (falhas.Count - 1) * regras.PontosPorFalhaAdicional);
    }

    private (double?, string) Vulnerabilidades(IReadOnlyList<EventoSeguranca> eventos, IReadOnlyDictionary<string, string?>? maquinasConhecidas)
    {
        var comFalha = eventos
            .Where(e => e.Tipo == TiposEvento.Vulnerabilidade && e.Ativo != ContextoConversao.NaoInformado)
            .GroupBy(e => e.Ativo, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (Pontos: PontosDaMaquina(g), Tipo: g.First().TipoAtivo), StringComparer.Ordinal);
        if (maquinasConhecidas is not null)
            foreach (var (ativo, tipo) in maquinasConhecidas)
                comFalha.TryAdd(ativo, (0, tipo)); // corrigida: continua na média, valendo 0

        var maquinas = comFalha.Values.Select(m => (m.Pontos, Peso: ativos.Tipo(m.Tipo).PesoNoScore)).ToList();
        if (maquinas.Count == 0)
            return (0, "Medido: o scanner não encontrou nenhuma falha.");

        var valor = maquinas.Sum(m => m.Pontos * m.Peso) / maquinas.Sum(m => m.Peso);
        var noTeto = maquinas.Count(m => m.Pontos >= regras.TetoPorMaquina);
        var n = regras.NotaPelaPiorFalha;
        return (valor,
            $"Cada máquina recebe a nota da sua pior falha (crítica {n[Severidade.Critica]:0}, alta {n[Severidade.Alta]:0}, " +
            $"média {n[Severidade.Media]:0}, baixa {n[Severidade.Baixa]:0}) e mais {regras.PontosPorFalhaAdicional:0} por falha a mais, até {regras.TetoPorMaquina:0}. " +
            $"O fator é a média de {Formatar.Numero(maquinas.Count)} máquinas (servidores pesam mais); " +
            $"{Formatar.Numero(noTeto)} delas chegaram ao máximo. Máquinas sem nenhuma falha não aparecem no arquivo do scanner.");
    }

    private (double?, string) Ameacas(IReadOnlyList<EventoSeguranca> eventos)
    {
        var alertas = eventos.Where(e => e.Tipo == TiposEvento.Alerta && e.Severidade is not null).ToList();
        if (alertas.Count == 0)
            return (0, "Medido: nenhum alerta registrado.");

        var valor = 100 * alertas.Sum(e => regras.PesoPorAlerta[e.Severidade!.Value]) / alertas.Count;
        var relevantes = alertas.Count(e => regras.PesoPorAlerta[e.Severidade!.Value] > 0);
        return (valor,
            $"{Formatar.Numero(relevantes)} de {Formatar.Numero(alertas.Count)} alertas merecem análise " +
            "(alertas médios contam metade; altos e críticos contam inteiro; baixos não contam).");
    }
}
