using System.Security.Cryptography;

namespace Piersec.Core.Seguranca;

public enum ResultadoEntrada { Falhou, Ok, PrecisaTrocarSenha }

/// <summary>Resultado de uma tentativa de login. <see cref="Bloqueado"/> serve só para a auditoria: a tela mostra sempre o mesmo erro.</summary>
public sealed record TentativaEntrada(ResultadoEntrada Resultado, Usuario? Usuario, bool Bloqueado);

/// <summary>
/// Regras de acesso (RS1): senha inicial de exatamente 8 caracteres com troca obrigatória no primeiro acesso,
/// bloqueio após 5 erros seguidos, mensagem de erro igual para qualquer falha e regras mínimas para a nova senha.
/// </summary>
public sealed class ServicoAutenticacao(RepositorioUsuarios repositorio, TimeProvider relogio)
{
    public const int TamanhoSenhaInicial = 8;
    public const int TamanhoMinimoNovaSenha = 12;
    public const int MaximoTentativas = 5;
    public static readonly TimeSpan TempoBloqueio = TimeSpan.FromMinutes(15);
    public const string VariavelSenhaInicial = "ADMIN_SENHA_INICIAL";

    // Sem caracteres que se confundem (0/O, 1/l/I) para quem vai digitar a senha inicial.
    private const string CaracteresSenhaInicial = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    // Hash de uma senha qualquer: usado quando o login não existe, para a resposta levar o mesmo tempo.
    private static readonly string HashFantasma = HashSenha.Gerar(Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)));

    public TentativaEntrada Entrar(string? login, string? senha)
    {
        var usuario = string.IsNullOrWhiteSpace(login) ? null : repositorio.Obter(login);
        if (usuario is null)
        {
            HashSenha.Verificar(senha ?? "", HashFantasma);
            return new(ResultadoEntrada.Falhou, null, false);
        }

        var agora = relogio.GetUtcNow();
        if (usuario.BloqueadoAteUtc > agora)
            return new(ResultadoEntrada.Falhou, usuario, true);

        if (!HashSenha.Verificar(senha ?? "", usuario.HashSenha))
        {
            var tentativas = usuario.TentativasFalhas + 1;
            var bloquear = tentativas >= MaximoTentativas;
            var atualizado = usuario with
            {
                TentativasFalhas = bloquear ? 0 : tentativas,
                BloqueadoAteUtc = bloquear ? agora + TempoBloqueio : usuario.BloqueadoAteUtc,
            };
            repositorio.Salvar(atualizado);
            return new(ResultadoEntrada.Falhou, atualizado, bloquear);
        }

        var ok = usuario with { TentativasFalhas = 0, BloqueadoAteUtc = null };
        repositorio.Salvar(ok);
        return new(ok.TrocarSenha ? ResultadoEntrada.PrecisaTrocarSenha : ResultadoEntrada.Ok, ok, false);
    }

    /// <summary>Troca a senha. Devolve null se deu certo, ou o motivo (em português) se não deu.</summary>
    public string? TrocarSenha(string login, string? senhaAtual, string? novaSenha, string? confirmacao)
    {
        var usuario = repositorio.Obter(login);
        if (usuario is null || !HashSenha.Verificar(senhaAtual ?? "", usuario.HashSenha))
            return "A senha atual não confere.";
        if (novaSenha != confirmacao)
            return "A confirmação é diferente da nova senha.";
        if (ProblemaNaNovaSenha(usuario.Login, senhaAtual!, novaSenha) is { } problema)
            return problema;

        repositorio.Salvar(usuario with { HashSenha = HashSenha.Gerar(novaSenha!), TrocarSenha = false });
        return null;
    }

    public static string? ProblemaNaNovaSenha(string login, string senhaAtual, string? nova)
    {
        if (string.IsNullOrEmpty(nova) || nova.Length < TamanhoMinimoNovaSenha)
            return $"A nova senha precisa ter pelo menos {TamanhoMinimoNovaSenha} caracteres.";
        if (nova == senhaAtual)
            return "A nova senha precisa ser diferente da atual.";
        if (nova.Contains(login, StringComparison.OrdinalIgnoreCase))
            return "A nova senha não pode conter o nome de usuário.";
        if (!nova.Any(char.IsLetter) || !nova.Any(c => !char.IsLetter(c)))
            return "A nova senha precisa ter letras e também números ou símbolos.";
        return null;
    }

    /// <summary>Cria um usuário com senha inicial aleatória (devolvida UMA vez para ser entregue à pessoa).</summary>
    public (Usuario Usuario, string SenhaInicial) Criar(string login, string papel)
    {
        login = RepositorioUsuarios.Normalizar(login);
        if (!RepositorioUsuarios.LoginValido(login))
            throw new ArgumentException("Login deve ter de 3 a 32 caracteres: letras minúsculas, números, ponto, hífen ou sublinhado.");
        if (!Papeis.Todos.Contains(papel))
            throw new ArgumentException("Papel inválido.");
        if (repositorio.Obter(login) is not null)
            throw new ArgumentException("Já existe um usuário com esse login.");

        var senha = GerarSenhaInicial();
        var usuario = Novo(login, papel, senha);
        repositorio.Salvar(usuario);
        return (usuario, senha);
    }

    /// <summary>
    /// Na primeira execução (sem usuários), cria o "admin". A senha inicial vem de ADMIN_SENHA_INICIAL se tiver
    /// exatamente 8 caracteres; senão é sorteada e devolvida para ser mostrada UMA vez no terminal.
    /// </summary>
    public (bool Criado, string? SenhaSorteada) GarantirAdmin(string? senhaDoAmbiente)
    {
        if (repositorio.Listar().Count > 0)
            return (false, null);
        var usarAmbiente = senhaDoAmbiente is { Length: TamanhoSenhaInicial };
        var senha = usarAmbiente ? senhaDoAmbiente! : GerarSenhaInicial();
        repositorio.Salvar(Novo(Papeis.Admin, Papeis.Admin, senha));
        return (true, usarAmbiente ? null : senha);
    }

    public static string GerarSenhaInicial() =>
        string.Create(TamanhoSenhaInicial, 0, (destino, _) =>
        {
            for (var i = 0; i < destino.Length; i++)
                destino[i] = CaracteresSenhaInicial[RandomNumberGenerator.GetInt32(CaracteresSenhaInicial.Length)];
        });

    private Usuario Novo(string login, string papel, string senha) => new()
    {
        Login = login,
        Papel = papel,
        HashSenha = HashSenha.Gerar(senha),
        TrocarSenha = true,
        CriadoEmUtc = relogio.GetUtcNow(),
    };
}
