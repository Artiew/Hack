using System.Security.Cryptography;
using System.Text;

namespace Piersec.Core.Seguranca;

/// <summary>
/// Código de 6 dígitos que muda a cada 30 segundos (TOTP, RFC 6238), o mesmo dos apps Google/Microsoft Authenticator.
/// O site e o celular calculam o código sozinhos a partir de uma chave secreta e do relógio: nada passa pela internet.
/// </summary>
public static class Totp
{
    public const int Digitos = 6;
    public static readonly TimeSpan Periodo = TimeSpan.FromSeconds(30);

    /// <summary>Aceita o código do passo anterior e do seguinte, para tolerar o relógio do celular adiantado ou atrasado.</summary>
    public const int Tolerancia = 1;

    public const int TamanhoSegredo = 20; // 160 bits, o recomendado para HMAC-SHA1

    public static byte[] GerarSegredo() => RandomNumberGenerator.GetBytes(TamanhoSegredo);

    /// <summary>Número do "passo" de 30 segundos desde 1970 (é o que o app também calcula).</summary>
    public static long Passo(DateTimeOffset quando) => quando.ToUnixTimeSeconds() / (long)Periodo.TotalSeconds;

    public static string Codigo(byte[] segredo, long passo)
    {
        Span<byte> contador = stackalloc byte[8];
        for (var i = 7; i >= 0; i--, passo >>= 8)
            contador[i] = (byte)(passo & 0xFF);
        var hash = HMACSHA1.HashData(segredo, contador);

        // "Truncamento dinâmico" da RFC 4226: os 4 últimos bits escolhem de onde tirar 31 bits.
        var deslocamento = hash[^1] & 0x0F;
        var numero = ((hash[deslocamento] & 0x7F) << 24) | (hash[deslocamento + 1] << 16) | (hash[deslocamento + 2] << 8) | hash[deslocamento + 3];
        return (numero % 1_000_000).ToString("D6");
    }

    /// <summary>
    /// Passo em que o código confere (dentro da tolerância) e que é mais novo que <paramref name="ultimoPassoUsado"/>,
    /// ou null. Guardar o passo devolvido impede usar o mesmo código duas vezes.
    /// </summary>
    public static long? Verificar(byte[] segredo, string? codigo, DateTimeOffset agora, long? ultimoPassoUsado)
    {
        codigo = codigo?.Replace(" ", "").Trim();
        if (codigo is not { Length: Digitos } || !codigo.All(char.IsAsciiDigit))
            return null;
        var atual = Passo(agora);
        for (var passo = atual - Tolerancia; passo <= atual + Tolerancia; passo++)
        {
            if (passo <= (ultimoPassoUsado ?? long.MinValue))
                continue;
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Codigo(segredo, passo)), Encoding.ASCII.GetBytes(codigo)))
                return passo;
        }
        return null;
    }

    /// <summary>Endereço que o QR code carrega; o app lê emissor, conta e chave dele.</summary>
    public static string Endereco(string emissor, string conta, byte[] segredo) =>
        $"otpauth://totp/{Uri.EscapeDataString(emissor)}:{Uri.EscapeDataString(conta)}" +
        $"?secret={Base32.Codificar(segredo)}&issuer={Uri.EscapeDataString(emissor)}&digits={Digitos}&period={(int)Periodo.TotalSeconds}";
}

/// <summary>Base32 (RFC 4648, sem "="): o formato em que os apps de autenticação recebem a chave.</summary>
public static class Base32
{
    private const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Codificar(ReadOnlySpan<byte> dados)
    {
        var sb = new StringBuilder((dados.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in dados)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Alfabeto[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0)
            sb.Append(Alfabeto[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    /// <summary>"ABCD EFGH ...": a chave em grupos de 4, para quem digita no app em vez de ler o QR code.</summary>
    public static string EmGrupos(string chave) =>
        string.Join(' ', Enumerable.Range(0, (chave.Length + 3) / 4).Select(i => chave.Substring(i * 4, Math.Min(4, chave.Length - i * 4))));
}
