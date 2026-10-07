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
}
