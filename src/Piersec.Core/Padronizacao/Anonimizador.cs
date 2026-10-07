using System.Security.Cryptography;
using System.Text;

namespace Piersec.Core.Padronizacao;

/// <summary>
/// Troca o nome do usuário por um apelido fixo (ex.: USR-3FA9C01B7E) logo na entrada (RS5 / LGPD).
/// Usa HMAC-SHA256 com uma chave secreta: o mesmo usuário sempre vira o mesmo apelido
/// (dá para correlacionar), mas sem a chave não dá para voltar ao nome.
/// </summary>
public sealed class Anonimizador
{
    public const int TamanhoMinimoChave = 32;
    private readonly byte[] _chave;

    public Anonimizador(byte[] chave)
    {
        if (chave.Length < TamanhoMinimoChave)
            throw new ArgumentException($"A chave de anonimização precisa ter ao menos {TamanhoMinimoChave} bytes.", nameof(chave));
        _chave = chave.ToArray();
    }

    public string Anonimizar(string identificador)
    {
        var normalizado = identificador.Trim().ToLowerInvariant();
        var hash = HMACSHA256.HashData(_chave, Encoding.UTF8.GetBytes(normalizado));
        return "USR-" + Convert.ToHexString(hash, 0, 5);
    }
}

/// <summary>Obtém a chave de anonimização sem nunca colocá-la no código.</summary>
public static class ChaveAnonimizacao
{
    public const string VariavelAmbiente = "PIERSEC_CHAVE_ANONIMIZACAO";

    /// <summary>
    /// 1º: variável de ambiente (base64). 2º: arquivo em var/segredos (criado na primeira execução).
    /// </summary>
    public static byte[] Obter(string pastaDados)
    {
        var doAmbiente = Environment.GetEnvironmentVariable(VariavelAmbiente);
        if (!string.IsNullOrWhiteSpace(doAmbiente))
        {
            byte[] chave;
            try { chave = Convert.FromBase64String(doAmbiente); }
            catch (FormatException) { throw new InvalidOperationException($"{VariavelAmbiente} não está em base64."); }
            if (chave.Length < Anonimizador.TamanhoMinimoChave)
                throw new InvalidOperationException($"{VariavelAmbiente} precisa ter ao menos {Anonimizador.TamanhoMinimoChave} bytes.");
            return chave;
        }

        var arquivo = Path.Combine(pastaDados, "segredos", "chave-anonimizacao.bin");
        if (File.Exists(arquivo))
            return File.ReadAllBytes(arquivo);

        Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
        var nova = RandomNumberGenerator.GetBytes(Anonimizador.TamanhoMinimoChave);
        File.WriteAllBytes(arquivo, nova);
        return nova;
    }
}
