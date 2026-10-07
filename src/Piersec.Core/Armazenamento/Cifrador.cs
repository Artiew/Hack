namespace Piersec.Core.Armazenamento;

/// <summary>
/// Cifra e decifra o que é gravado em disco (RS3: dados protegidos em repouso). O núcleo não sabe COMO:
/// a plataforma web usa o Data Protection do ASP.NET Core com a chave protegida pelo Windows (DPAPI).
/// </summary>
public interface ICifrador
{
    byte[] Cifrar(byte[] aberto);

    /// <summary>Lança <see cref="System.Security.Cryptography.CryptographicException"/> se o conteúdo foi alterado ou a chave não confere.</summary>
    byte[] Decifrar(byte[] cifrado);
}

/// <summary>Sem cifra: só para testes que não tratam de cifragem. A plataforma nunca usa este.</summary>
public sealed class SemCifra : ICifrador
{
    public static SemCifra Instancia { get; } = new();
    public byte[] Cifrar(byte[] aberto) => aberto;
    public byte[] Decifrar(byte[] cifrado) => cifrado;
}
