using System.Globalization;

namespace Piersec.Core.Relatorio;

/// <summary>Formatação brasileira usada igual na tela e no relatório.</summary>
public static class Formatar
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Bytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => (bytes / 1024d).ToString("0.#", PtBr) + " KB",
        _ => (bytes / (1024d * 1024)).ToString("0.#", PtBr) + " MB",
    };

    public static string DataHora(DateTimeOffset? utc, TimeZoneInfo fuso) =>
        utc is { } d ? TimeZoneInfo.ConvertTime(d, fuso).ToString("dd/MM/yyyy HH:mm", PtBr) : "—";

    public static string Numero(int n) => n.ToString("N0", PtBr);

    /// <summary>"1 chamado", "3 chamados": plural certo, sem o "(s)" que confunde o leitor.</summary>
    public static string Qtd(int n, string singular, string plural) => $"{Numero(n)} {(n == 1 ? singular : plural)}";

    /// <summary>Valor em reais arredondado para leitura: "R$ 800", "R$ 500 mil", "R$ 1,2 milhão", "R$ 12 milhões".</summary>
    public static string Dinheiro(decimal valor)
    {
        if (valor >= 1_000_000)
        {
            var milhoes = Math.Round(valor / 1_000_000, 1);
            return $"R$ {milhoes.ToString("0.#", PtBr)} {(milhoes < 2 ? "milhão" : "milhões")}";
        }
        if (valor >= 1_000)
            return $"R$ {Math.Round(valor / 1_000).ToString("0", PtBr)} mil";
        return $"R$ {valor.ToString("0", PtBr)}";
    }

    /// <summary>"entre R$ 500 mil e R$ 5 milhões".</summary>
    public static string FaixaDinheiro(decimal minimo, decimal maximo) => $"entre {Dinheiro(minimo)} e {Dinheiro(maximo)}";

    /// <summary>Faixa curta, para cartão de indicador: "R$ 500 mil a R$ 5 milhões".</summary>
    public static string FaixaCurta(decimal minimo, decimal maximo) => $"{Dinheiro(minimo)} a {Dinheiro(maximo)}";
}
