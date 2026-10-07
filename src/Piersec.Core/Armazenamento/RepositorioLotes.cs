using System.Text.Json;
using System.Text.Json.Serialization;
using Piersec.Core.Modelo;

namespace Piersec.Core.Armazenamento;

/// <summary>
/// Guarda cada lote em var/lotes/{id}/ (lote.json + relatório). Fase 1: JSON simples.
/// O id é sempre validado como Guid antes de virar caminho, para ninguém pedir "..\..\outra-pasta".
/// </summary>
public sealed class RepositorioLotes
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _pastaLotes;

    public RepositorioLotes(string pastaDados)
    {
        _pastaLotes = Path.Combine(pastaDados, "lotes");
        Directory.CreateDirectory(_pastaLotes);
    }

    public static bool IdValido(string? id) => id is { Length: 32 } && Guid.TryParseExact(id, "N", out _);

    public string Pasta(string id) =>
        IdValido(id) ? Path.Combine(_pastaLotes, id) : throw new ArgumentException("Id de lote inválido.", nameof(id));

    public string CaminhoRelatorio(string id, string extensao) => extensao is "html" or "pdf"
        ? Path.Combine(Pasta(id), "relatorio." + extensao)
        : throw new ArgumentException("Extensão de relatório inválida.", nameof(extensao));

    public void Salvar(Lote lote)
    {
        var pasta = Pasta(lote.Id);
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Path.Combine(pasta, "lote.json"), JsonSerializer.Serialize(lote, Json));
    }

    public Lote? Carregar(string id)
    {
        if (!IdValido(id))
            return null;
        var arquivo = Path.Combine(Pasta(id), "lote.json");
        return File.Exists(arquivo) ? JsonSerializer.Deserialize<Lote>(File.ReadAllText(arquivo), Json) : null;
    }

    /// <summary>Todos os lotes, do mais recente para o mais antigo.</summary>
    public IReadOnlyList<Lote> Listar() =>
        Directory.EnumerateDirectories(_pastaLotes)
            .Select(Path.GetFileName)
            .Where(IdValido)
            .Select(id => Carregar(id!))
            .OfType<Lote>()
            .OrderByDescending(l => l.CriadoEmUtc)
            .ToList();
}
