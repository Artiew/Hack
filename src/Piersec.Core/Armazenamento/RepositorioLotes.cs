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
        File.WriteAllText(Path.Combine(pasta, "resumo.json"), JsonSerializer.Serialize(ResumoLote.De(lote), Json));
    }

    public Lote? Carregar(string id)
    {
        if (!IdValido(id))
            return null;
        var arquivo = Path.Combine(Pasta(id), "lote.json");
        return File.Exists(arquivo) ? JsonSerializer.Deserialize<Lote>(File.ReadAllText(arquivo), Json) : null;
    }

    /// <summary>Resumo de todos os lotes, do mais recente para o mais antigo (sem carregar os eventos).</summary>
    public IReadOnlyList<ResumoLote> Listar() =>
        Directory.EnumerateDirectories(_pastaLotes)
            .Select(Path.GetFileName)
            .Where(IdValido)
            .Select(id => CarregarResumo(id!))
            .OfType<ResumoLote>()
            .OrderByDescending(l => l.CriadoEmUtc)
            .ToList();

    private ResumoLote? CarregarResumo(string id)
    {
        var arquivo = Path.Combine(Pasta(id), "resumo.json");
        if (File.Exists(arquivo))
            return JsonSerializer.Deserialize<ResumoLote>(File.ReadAllText(arquivo), Json);
        return Carregar(id) is { } lote ? ResumoLote.De(lote) : null; // lotes antigos, sem resumo
    }
}
