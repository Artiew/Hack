using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Piersec.Core.Modelo;

namespace Piersec.Core.Armazenamento;

/// <summary>
/// Guarda cada lote em var/lotes/{id}/: lote, resumo e relatórios (HTML e PDF), todos CIFRADOS (*.cifrado, RS3).
/// O id é sempre validado como Guid antes de virar caminho, para ninguém pedir "..\..\outra-pasta".
/// Arquivo que não decifra (alterado no disco ou chave diferente) é tratado como inexistente, nunca lido "pela metade".
/// </summary>
public sealed class RepositorioLotes
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _pastaLotes;
    private readonly string _pastaTemporaria;
    private readonly ICifrador _cifrador;

    public RepositorioLotes(string pastaDados, ICifrador? cifrador = null)
    {
        _pastaLotes = Path.Combine(pastaDados, "lotes");
        _pastaTemporaria = Path.Combine(pastaDados, "tmp");
        _cifrador = cifrador ?? SemCifra.Instancia;
        Directory.CreateDirectory(_pastaLotes);
    }

    /// <summary>
    /// Pasta temporária dentro de var/ para quando um arquivo precisa existir aberto por instantes (ex.: o navegador
    /// gerar o PDF). Quem pede é responsável por apagá-la (ver <see cref="Pipeline.CicloAnalise"/>).
    /// </summary>
    public string CriarPastaTemporaria() =>
        Directory.CreateDirectory(Path.Combine(_pastaTemporaria, Guid.NewGuid().ToString("N"))).FullName;

    public static bool IdValido(string? id) => id is { Length: 32 } && Guid.TryParseExact(id, "N", out _);

    public static bool ExtensaoValida(string extensao) => extensao is "html" or "pdf";

    public string Pasta(string id) =>
        IdValido(id) ? Path.Combine(_pastaLotes, id) : throw new ArgumentException("Id de lote inválido.", nameof(id));

    public void Salvar(Lote lote)
    {
        Directory.CreateDirectory(Pasta(lote.Id));
        Gravar(lote.Id, "lote.json", JsonSerializer.SerializeToUtf8Bytes(lote, Json));
        Gravar(lote.Id, "resumo.json", JsonSerializer.SerializeToUtf8Bytes(ResumoLote.De(lote), Json));
    }

    public Lote? Carregar(string id) =>
        IdValido(id) && Ler(id, "lote.json") is { } bytes ? JsonSerializer.Deserialize<Lote>(bytes, Json) : null;

    public void SalvarRelatorio(string id, string extensao, byte[] conteudo)
    {
        if (!ExtensaoValida(extensao))
            throw new ArgumentException("Extensão de relatório inválida.", nameof(extensao));
        Directory.CreateDirectory(Pasta(id));
        Gravar(id, "relatorio." + extensao, conteudo);
    }

    /// <summary>Relatório decifrado, ou null se não existir (ou não decifrar).</summary>
    public byte[]? LerRelatorio(string id, string extensao) =>
        IdValido(id) && ExtensaoValida(extensao) ? Ler(id, "relatorio." + extensao) : null;

    public bool ExisteRelatorio(string id, string extensao) =>
        IdValido(id) && ExtensaoValida(extensao) && File.Exists(Caminho(id, "relatorio." + extensao));

    /// <summary>Resumo de todos os lotes, do mais recente para o mais antigo (sem carregar os eventos).</summary>
    public IReadOnlyList<ResumoLote> Listar() =>
        Directory.EnumerateDirectories(_pastaLotes)
            .Select(Path.GetFileName)
            .Where(IdValido)
            .Select(id => Ler(id!, "resumo.json") is { } bytes ? JsonSerializer.Deserialize<ResumoLote>(bytes, Json) : null)
            .OfType<ResumoLote>()
            .OrderByDescending(l => l.CriadoEmUtc)
            .ToList();

    private string Caminho(string id, string nome) => Path.Combine(Pasta(id), nome + ".cifrado");

    private void Gravar(string id, string nome, byte[] aberto)
    {
        var destino = Caminho(id, nome);
        var temporario = destino + ".tmp";
        File.WriteAllBytes(temporario, _cifrador.Cifrar(aberto));
        File.Move(temporario, destino, overwrite: true); // grava inteiro ou não grava
    }

    private byte[]? Ler(string id, string nome)
    {
        var arquivo = Caminho(id, nome);
        if (!File.Exists(arquivo))
            return null;
        try
        {
            return _cifrador.Decifrar(File.ReadAllBytes(arquivo));
        }
        catch (CryptographicException)
        {
            return null; // alterado no disco ou cifrado com outra chave: não confiar
        }
    }
}
