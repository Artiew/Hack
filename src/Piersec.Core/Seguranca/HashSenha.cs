using System.Security.Cryptography;
using System.Text;

namespace Piersec.Core.Seguranca;

/// <summary>
/// Guarda senhas como "impressão digital" (hash) que não dá para desfazer: PBKDF2-HMAC-SHA256 com
/// 600.000 iterações e sal aleatório (recomendação da OWASP). A comparação leva sempre o mesmo tempo,
/// para não dar pistas a quem tenta adivinhar.
/// Formato gravado: PBKDF2-SHA256$iterações$sal(base64)$hash(base64)
/// </summary>
public static class HashSenha
{
    public const int Iteracoes = 600_000;
    private const int TamanhoSal = 16;
    private const int TamanhoHash = 32;
    private const string Prefixo = "PBKDF2-SHA256";

    public static string Gerar(string senha)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanhoSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(senha), sal, Iteracoes, HashAlgorithmName.SHA256, TamanhoHash);
        return $"{Prefixo}${Iteracoes}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string senha, string gravado)
    {
        var partes = gravado.Split('$');
        if (partes.Length != 4 || partes[0] != Prefixo || !int.TryParse(partes[1], out var iteracoes))
            return false;
        byte[] sal, esperado;
        try
        {
            sal = Convert.FromBase64String(partes[2]);
            esperado = Convert.FromBase64String(partes[3]);
        }
        catch (FormatException)
        {
            return false;
        }
        var calculado = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(senha), sal, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }
}
