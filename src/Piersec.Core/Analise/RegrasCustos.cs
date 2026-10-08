namespace Piersec.Core.Analise;

/// <summary>Faixa de horas de uma ação: uma parte fixa e uma parte que cresce com o número de máquinas.</summary>
public sealed record HorasDaAcao
{
    public double FixoMinimo { get; init; }
    public double FixoMaximo { get; init; }
    public double PorMaquinaMinimo { get; init; }
    public double PorMaquinaMaximo { get; init; }
    public string? Premissa { get; init; }
}

/// <summary>Valor da hora técnica assumido pela equipe (premissa declarada; não veio dos dados).</summary>
public sealed record ValorHora
{
    public double Minimo { get; init; }
    public double Maximo { get; init; }
    public string? Premissa { get; init; }
}

/// <summary>Quanto custa corrigir (regras/custos.json). Sempre faixa, nunca valor exato.</summary>
public sealed record RegrasCustos
{
    public string? Descricao { get; init; }
    public string Moeda { get; init; } = "R$";
    public ValorHora HoraTecnica { get; init; } = new();
    public double ArredondarPara { get; init; } = 500;
    public IReadOnlyDictionary<string, HorasDaAcao> HorasPorAcao { get; init; } = new Dictionary<string, HorasDaAcao>();
    public IReadOnlyList<string> NaoIncluido { get; init; } = [];

    public static RegrasCustos Vazio { get; } = new();
}

/// <summary>Faixa de dinheiro já arredondada, pronta para mostrar. Nula quando não há premissa para o tipo de ação.</summary>
public sealed record FaixaCusto(double Minimo, double Maximo);

/// <summary>
/// Calcula a faixa de custo de uma ação do plano: horas (fixas + por máquina) × valor da hora, arredondado.
/// Nunca devolve valor exato — o objetivo é dar ordem de grandeza honesta, não precisão falsa.
/// </summary>
public sealed class CalculadoraCustos(RegrasCustos regras)
{
    public RegrasCustos Regras => regras;

    public FaixaCusto? Calcular(string tipoAcao, int maquinas)
    {
        if (!regras.HorasPorAcao.TryGetValue(tipoAcao, out var h))
            return null;

        var horasMin = h.FixoMinimo + h.PorMaquinaMinimo * Math.Max(maquinas, 0);
        var horasMax = h.FixoMaximo + h.PorMaquinaMaximo * Math.Max(maquinas, 0);
        var minimo = ArredondarParaBaixo(horasMin * regras.HoraTecnica.Minimo);
        var maximo = ArredondarParaCima(horasMax * regras.HoraTecnica.Maximo);
        if (maximo <= 0)
            return null;
        return new(Math.Min(minimo, maximo), maximo);
    }

    /// <summary>Soma de faixas: o mínimo com o mínimo, o máximo com o máximo. Nula se nenhuma ação tem premissa.</summary>
    public static FaixaCusto? Somar(IEnumerable<FaixaCusto?> faixas)
    {
        double min = 0, max = 0;
        var achou = false;
        foreach (var f in faixas)
        {
            if (f is null) continue;
            achou = true;
            min += f.Minimo;
            max += f.Maximo;
        }
        return achou ? new(min, max) : null;
    }

    private double Passo => regras.ArredondarPara > 0 ? regras.ArredondarPara : 1;

    /// <summary>
    /// Arredonda o piso da faixa para baixo, mas nunca até zero: trabalho que existe não pode custar "R$ 0".
    /// Custo pequeno fica valendo um passo de arredondamento (ex.: R$ 500), que é o mínimo que a faixa consegue dizer.
    /// </summary>
    private double ArredondarParaBaixo(double valor) =>
        valor <= 0 ? 0 : Math.Max(Math.Floor(valor / Passo) * Passo, Passo);

    private double ArredondarParaCima(double valor) =>
        valor <= 0 ? 0 : Math.Ceiling(valor / Passo) * Passo;
}
