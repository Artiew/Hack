using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Piersec.Core.Seguranca;

/// <summary>Papéis de acesso. admin: tudo + usuários; analista: envia CSV e vê tudo; executivo: só a visão executiva.</summary>
public static class Papeis
{
    public const string Admin = "admin";
    public const string Analista = "analista";
    public const string Executivo = "executivo";

    public static readonly IReadOnlyList<string> Todos = [Admin, Analista, Executivo];
}

public sealed record Usuario
{
    public required string Login { get; init; }
    public required string Papel { get; init; }
    public required string HashSenha { get; init; }

    /// <summary>Senha inicial ainda não trocada: o usuário é obrigado a trocar antes de usar o sistema.</summary>
    public bool TrocarSenha { get; init; }

    public int TentativasFalhas { get; init; }
    public DateTimeOffset? BloqueadoAteUtc { get; init; }
    public required DateTimeOffset CriadoEmUtc { get; init; }

    // ---- verificação em duas etapas (2FA) ----

    /// <summary>Chave do app de autenticação, CIFRADA (base64). Null = 2FA desligado.</summary>
    public string? Segredo2fa { get; init; }

    /// <summary>Chave gerada no cadastro, ainda não confirmada com um código (cifrada). Vira <see cref="Segredo2fa"/> ao confirmar.</summary>
    public string? Segredo2faPendente { get; init; }

    /// <summary>Último passo de 30 s aceito: o mesmo código não vale duas vezes.</summary>
    public long? UltimoPasso2fa { get; init; }

    /// <summary>SHA-256 dos códigos de recuperação ainda não usados (o código em si nunca é gravado).</summary>
    public IReadOnlyList<string> CodigosRecuperacao { get; init; } = [];

    [JsonIgnore] public bool Tem2fa => Segredo2fa is not null;
}

/// <summary>Cadastro de usuários em var/usuarios.json (só hashes; nenhuma senha é gravada).</summary>
public sealed partial class RepositorioUsuarios(string pastaDados)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _arquivo = Path.Combine(pastaDados, "usuarios.json");
    private readonly Lock _trava = new();

    public static bool LoginValido(string? login) => login is not null && FormatoLogin().IsMatch(login);

    public static string Normalizar(string login) => login.Trim().ToLowerInvariant();

    public IReadOnlyList<Usuario> Listar()
    {
        lock (_trava)
            return Ler();
    }

    public Usuario? Obter(string login)
    {
        var chave = Normalizar(login);
        lock (_trava)
            return Ler().FirstOrDefault(u => u.Login == chave);
    }

    /// <summary>Apaga o usuário. Devolve false se ele não existia.</summary>
    public bool Remover(string login)
    {
        var chave = Normalizar(login);
        lock (_trava)
        {
            var todos = Ler();
            if (todos.RemoveAll(u => u.Login == chave) == 0)
                return false;
            Gravar(todos);
            return true;
        }
    }

    public void Salvar(Usuario usuario)
    {
        lock (_trava)
        {
            Gravar(Ler().Where(u => u.Login != usuario.Login).Append(usuario).OrderBy(u => u.Login, StringComparer.Ordinal).ToList());
        }
    }

    private void Gravar(List<Usuario> todos)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
        var temporario = _arquivo + ".tmp";
        File.WriteAllText(temporario, JsonSerializer.Serialize(todos, Json));
        File.Move(temporario, _arquivo, overwrite: true); // grava inteiro ou não grava (nunca pela metade)
    }

    private List<Usuario> Ler() =>
        File.Exists(_arquivo) ? JsonSerializer.Deserialize<List<Usuario>>(File.ReadAllText(_arquivo), Json) ?? [] : [];

    [GeneratedRegex("^[a-z0-9._-]{3,32}$")]
    private static partial Regex FormatoLogin();
}
