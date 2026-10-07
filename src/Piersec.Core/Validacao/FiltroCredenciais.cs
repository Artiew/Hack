using System.Text.RegularExpressions;

namespace Piersec.Core.Validacao;

/// <summary>
/// Encontra e mascara credenciais em texto vindo dos dados (S1: nunca no relatório, nem parcialmente).
/// Procura "senha=...", "password: ...", "token=...", "api_key=...", chaves privadas e chaves de acesso
/// no formato da AWS. Texto comum como "sem precisar de senha" não é afetado (precisa de ":" ou "=").
/// </summary>
public static partial class FiltroCredenciais
{
    public const string Mascara = "[credencial removida]";

    public static int Contar(string? texto) => string.IsNullOrEmpty(texto) ? 0 : Padrao().Count(texto);

    public static string Mascarar(string texto) => Padrao().Replace(texto, Mascara);

    [GeneratedRegex(
        @"(?i)\b(senha|password|passwd|pwd|secret|segredo|token|api[_-]?key|access[_-]?key)\s*[:=]\s*[""']?[^\s""',;]+" +
        @"|-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?(-----END [A-Z ]*PRIVATE KEY-----|$)" +
        @"|\bAKIA[0-9A-Z]{16}\b")]
    private static partial Regex Padrao();
}
