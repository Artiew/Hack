using System.Globalization;
using System.Text;

namespace Piersec.Core.Padronizacao;

public static class Texto
{
    /// <summary>
    /// Transforma um texto em "chave" para comparação: minúsculas, sem acento, espaços e hífens viram "_".
    /// Ex.: " Data Hora " → "data_hora", "Crítica" → "critica".
    /// </summary>
    public static string NormalizarChave(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";

        var decomposto = texto.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            // Na forma decomposta, "é" vira "e" + acento; o acento é um NonSpacingMark e é descartado.
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.IsWhiteSpace(c) || c == '-' ? '_' : char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
