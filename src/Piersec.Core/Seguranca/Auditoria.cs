using System.Text.Json;

namespace Piersec.Core.Seguranca;

/// <summary>Um registro de auditoria: quem fez o quê e quando. Nunca contém senha, conteúdo de arquivo ou dado do lote.</summary>
public sealed record RegistroAuditoria(DateTimeOffset QuandoUtc, string Usuario, string Acao, string Detalhe);

/// <summary>
/// Trilha de auditoria (RS4) em var/auditoria.jsonl: uma linha JSON por evento, só acrescentada (nunca editada).
/// Quem chama passa apenas identificadores e contagens; login inexistente é registrado como "(desconhecido)"
/// para não gravar, por engano, uma senha digitada no campo de usuário.
/// </summary>
public sealed class Auditoria(string pastaDados, TimeProvider relogio)
{
    public const string Desconhecido = "(desconhecido)";

    private readonly string _arquivo = Path.Combine(pastaDados, "auditoria.jsonl");
    private readonly Lock _trava = new();

    public void Registrar(string? usuario, string acao, string detalhe = "")
    {
        var registro = new RegistroAuditoria(relogio.GetUtcNow(), string.IsNullOrWhiteSpace(usuario) ? Desconhecido : usuario, acao, detalhe);
        var linha = JsonSerializer.Serialize(registro) + Environment.NewLine;
        lock (_trava)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
            File.AppendAllText(_arquivo, linha);
        }
    }

    /// <summary>Os registros mais recentes primeiro.</summary>
    public IReadOnlyList<RegistroAuditoria> Ultimos(int quantidade)
    {
        lock (_trava)
        {
            if (!File.Exists(_arquivo))
                return [];
            return File.ReadLines(_arquivo)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => JsonSerializer.Deserialize<RegistroAuditoria>(l)!)
                .Reverse()
                .Take(quantidade)
                .ToList();
        }
    }
}
