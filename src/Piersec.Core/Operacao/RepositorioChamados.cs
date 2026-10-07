using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Piersec.Core.Armazenamento;

namespace Piersec.Core.Operacao;

/// <summary>
/// Guarda os chamados e o último resultado da operação em var/operacao/estado.json.cifrado (RS3).
/// Grava inteiro ou não grava (arquivo temporário + troca). Se o arquivo existir mas não decifrar,
/// a plataforma PARA com erro em vez de começar do zero: perder o histórico em silêncio duplicaria chamados.
/// </summary>
public sealed class RepositorioChamados
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    private readonly string _arquivo;
    private readonly ICifrador _cifrador;

    public RepositorioChamados(string pastaDados, ICifrador? cifrador = null)
    {
        var pasta = Path.Combine(pastaDados, "operacao");
        Directory.CreateDirectory(pasta);
        _arquivo = Path.Combine(pasta, "estado.json.cifrado");
        _cifrador = cifrador ?? SemCifra.Instancia;
    }

    public EstadoOperacao Carregar()
    {
        if (!File.Exists(_arquivo))
            return EstadoOperacao.Vazio;
        try
        {
            return JsonSerializer.Deserialize<EstadoOperacao>(_cifrador.Decifrar(File.ReadAllBytes(_arquivo)), Json) ?? EstadoOperacao.Vazio;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            throw new InvalidOperationException(
                $"Não foi possível decifrar {_arquivo} (alterado no disco ou chave diferente). Os chamados não foram tocados.", ex);
        }
    }

    public void Salvar(EstadoOperacao estado)
    {
        var temporario = _arquivo + ".tmp";
        File.WriteAllBytes(temporario, _cifrador.Cifrar(JsonSerializer.SerializeToUtf8Bytes(estado, Json)));
        File.Move(temporario, _arquivo, overwrite: true);
    }
}
