using System.Globalization;
using System.Text.RegularExpressions;

namespace Piersec.Core.Padronizacao;

/// <summary>
/// Converte datas escritas de jeitos diferentes para um único formato: UTC.
/// Data sem fuso é tratada como horário do <see cref="FusoPadrao"/> (premissa declarada no relatório).
/// </summary>
public sealed partial class NormalizadorDatas(TimeZoneInfo fusoPadrao)
{
    public static TimeZoneInfo FusoBrasilia { get; } = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    // Só formatos explícitos: "01/02/2026" é sempre dia/mês (padrão brasileiro), nunca adivinhado.
    private static readonly string[] FormatosSemFuso =
    [
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm",
        "yyyy-MM-dd HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd",
        "dd/MM/yyyy HH:mm:ss",
        "dd/MM/yyyy HH:mm",
        "dd/MM/yyyy",
    ];

    public TimeZoneInfo FusoPadrao { get; } = fusoPadrao;

    public bool TentarNormalizar(string? texto, out DateTimeOffset utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(texto))
            return false;
        texto = texto.Trim();

        // Com fuso explícito (termina em Z ou +03:00 / -0300): respeita o fuso informado.
        if (TerminaComFuso().IsMatch(texto))
        {
            if (!DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var comFuso))
                return false;
            utc = comFuso.ToUniversalTime();
            return true;
        }

        if (!DateTime.TryParseExact(texto, FormatosSemFuso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return false;

        try
        {
            utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), FusoPadrao));
            return true;
        }
        catch (ArgumentException)
        {
            return false; // horário que não existe naquele fuso (ex.: pulado no horário de verão)
        }
    }

    [GeneratedRegex(@"(Z|[+-]\d{2}:?\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex TerminaComFuso();
}
