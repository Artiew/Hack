using System.Security.Cryptography;
using System.Text;
using Piersec.Core.Armazenamento;

namespace Piersec.Core.Seguranca;

public enum ResultadoEntrada { Falhou, Ok, PrecisaTrocarSenha, PrecisaSegundoFator }

/// <summary>Resultado de uma tentativa de login. <see cref="Bloqueado"/> serve só para a auditoria: a tela mostra sempre o mesmo erro.</summary>
public sealed record TentativaEntrada(ResultadoEntrada Resultado, Usuario? Usuario, bool Bloqueado);

/// <summary>Resultado do código do 2FA. <see cref="RecuperacaoRestantes"/> só vale quando um código de recuperação foi usado.</summary>
public sealed record TentativaSegundoFator(bool Ok, Usuario? Usuario, bool Bloqueado, bool UsouRecuperacao = false, int RecuperacaoRestantes = 0);

/// <summary>
/// Regras de acesso (RS1): senha inicial de exatamente 8 caracteres com troca obrigatória no primeiro acesso,
/// bloqueio após 5 erros seguidos, mensagem de erro igual para qualquer falha e regras mínimas para a nova senha.
/// Verificação em duas etapas (2FA, código do app de autenticação): obrigatória para admin, opcional para os outros.
/// </summary>
public sealed class ServicoAutenticacao(RepositorioUsuarios repositorio, TimeProvider relogio, ICifrador? cifrador = null)
{
    public const string Emissor2fa = "Piersec";
    public const int QuantidadeCodigosRecuperacao = 8;
    private const string CaracteresRecuperacao = "abcdefghjkmnpqrstuvwxyz23456789";
    private readonly ICifrador _cifrador = cifrador ?? SemCifra.Instancia;

    public const int TamanhoSenhaInicial = 8;
    /// <summary>Mínimo do guia NIST SP 800-63B para senha escolhida pela pessoa.</summary>
    public const int TamanhoMinimoNovaSenha = 8;
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

        // Com 2FA, a senha certa NÃO zera as tentativas: senão quem descobriu a senha poderia chutar códigos sem fim,
        // entrando de novo com a senha a cada 4 erros. Elas só zeram quando o código também confere.
        if (usuario.Tem2fa)
            return new(ResultadoEntrada.PrecisaSegundoFator, usuario, false);

        var ok = usuario with { TentativasFalhas = 0, BloqueadoAteUtc = null };
        repositorio.Salvar(ok);
        return new(ok.TrocarSenha ? ResultadoEntrada.PrecisaTrocarSenha : ResultadoEntrada.Ok, ok, false);
    }

    // ================= 2FA =================

    /// <summary>Admin é obrigado a usar 2FA; analista e executivo escolhem.</summary>
    public static bool Exige2fa(Usuario usuario) => usuario.Papel == Papeis.Admin;

    public static bool PrecisaConfigurar2fa(Usuario usuario) => Exige2fa(usuario) && !usuario.Tem2fa;

    /// <summary>
    /// Segunda etapa do login: código do app (6 dígitos) ou um código de recuperação. Erros contam nas mesmas
    /// tentativas da senha e bloqueiam do mesmo jeito.
    /// </summary>
    public TentativaSegundoFator ConfirmarSegundoFator(string login, string? codigo)
    {
        var usuario = repositorio.Obter(login);
        if (usuario is not { Tem2fa: true })
            return new(false, null, false);
        var agora = relogio.GetUtcNow();
        if (usuario.BloqueadoAteUtc > agora)
            return new(false, usuario, true);

        if (Totp.Verificar(Decifrar(usuario.Segredo2fa!), codigo, agora, usuario.UltimoPasso2fa) is { } passo)
        {
            var ok = usuario with { UltimoPasso2fa = passo, TentativasFalhas = 0, BloqueadoAteUtc = null };
            repositorio.Salvar(ok);
            return new(true, ok, false);
        }

        var hash = HashRecuperacao(codigo);
        if (hash is not null && usuario.CodigosRecuperacao.Contains(hash))
        {
            var ok = usuario with { CodigosRecuperacao = [.. usuario.CodigosRecuperacao.Where(c => c != hash)], TentativasFalhas = 0, BloqueadoAteUtc = null };
            repositorio.Salvar(ok);
            return new(true, ok, false, UsouRecuperacao: true, RecuperacaoRestantes: ok.CodigosRecuperacao.Count);
        }

        var tentativas = usuario.TentativasFalhas + 1;
        var bloquear = tentativas >= MaximoTentativas;
        var atualizado = usuario with
        {
            TentativasFalhas = bloquear ? 0 : tentativas,
            BloqueadoAteUtc = bloquear ? agora + TempoBloqueio : usuario.BloqueadoAteUtc,
        };
        repositorio.Salvar(atualizado);
        return new(false, atualizado, bloquear);
    }

    /// <summary>
    /// Começa o cadastro do 2FA: devolve a chave e o endereço otpauth://.
    /// Preserva um cadastro pendente legível; se a proteção antiga não abre, gera outro ainda não ativado.
    /// Nunca substitui o 2FA já ativado.
    /// </summary>
    public (string Chave, string Endereco) Iniciar2fa(string login) => Iniciar2fa(login, out _);

    public (string Chave, string Endereco) Iniciar2fa(string login, out bool cadastroReiniciado)
    {
        var usuario = repositorio.Obter(login) ?? throw new ArgumentException($"Usuário '{login}' não existe.");
        if (usuario.Tem2fa)
            throw new InvalidOperationException("A verificação em duas etapas já está ativada.");

        var segredo = usuario.Segredo2faPendente is { } pendente ? DecifrarCadastroPendente(pendente) : null;
        cadastroReiniciado = usuario.Segredo2faPendente is not null && segredo is null;
        if (segredo is null)
        {
            segredo = Totp.GerarSegredo();
            repositorio.Salvar(usuario with { Segredo2faPendente = Cifrar(segredo) });
        }
        return (Base32.Codificar(segredo), Totp.Endereco(Emissor2fa, usuario.Login, segredo));
    }

    // A recuperação se limita ao cadastro AINDA NÃO ATIVADO. Falhas de escrita ou de geração de chaves
    // continuam sendo erros; uma chave ativa nunca passa por este tratamento.
    private byte[]? DecifrarCadastroPendente(string pendente)
    {
        try { return Decifrar(pendente); }
        catch (CryptographicException) { return null; }
        catch (FormatException) { return null; }
    }

    /// <summary>
    /// Termina o cadastro: o código do app precisa conferir com a chave nova (prova que o celular foi configurado).
    /// Devolve os códigos de recuperação, para mostrar UMA vez, ou o motivo da recusa.
    /// </summary>
    public (string? Erro, IReadOnlyList<string> CodigosRecuperacao) Confirmar2fa(string login, string? codigo)
    {
        var usuario = repositorio.Obter(login);
        if (usuario is { Tem2fa: true })
            return ("A verificação em duas etapas já está ativada.", []);
        if (usuario?.Segredo2faPendente is not { } pendente)
            return ("O cadastro não foi iniciado. Recarregue a página para gerar o QR code.", []);
        if (DecifrarCadastroPendente(pendente) is not { } segredo)
            return ("O cadastro anterior não pôde ser aberto. Use o novo QR code para configurar o app e confirme novamente.", []);
        if (Totp.Verificar(segredo, codigo, relogio.GetUtcNow(), null) is not { } passo)
            return ("O código não confere. Confira se o relógio do celular está certo e digite o código que aparece agora no app.", []);

        var codigos = GerarCodigosRecuperacao();
        repositorio.Salvar(usuario with
        {
            Segredo2fa = pendente,
            Segredo2faPendente = null,
            UltimoPasso2fa = passo,
            CodigosRecuperacao = [.. codigos.Select(c => HashRecuperacao(c)!)],
        });
        return (null, codigos);
    }

    /// <summary>Desliga o 2FA (só para quem não é obrigado a ter), conferindo um código do app.</summary>
    public string? Desativar2fa(string login, string? codigo)
    {
        var usuario = repositorio.Obter(login);
        if (usuario is not { Tem2fa: true })
            return "A verificação em duas etapas já está desligada.";
        if (Exige2fa(usuario))
            return "Para administradores a verificação em duas etapas é obrigatória.";
        if (Totp.Verificar(Decifrar(usuario.Segredo2fa!), codigo, relogio.GetUtcNow(), usuario.UltimoPasso2fa) is null)
            return "O código não confere.";
        repositorio.Salvar(Sem2fa(usuario));
        return null;
    }

    /// <summary>Troca os códigos de recuperação por 8 novos (os antigos deixam de valer), conferindo um código do app.</summary>
    public (string? Erro, IReadOnlyList<string> CodigosRecuperacao) NovosCodigosRecuperacao(string login, string? codigo)
    {
        var usuario = repositorio.Obter(login);
        if (usuario is not { Tem2fa: true })
            return ("A verificação em duas etapas está desligada.", []);
        if (Totp.Verificar(Decifrar(usuario.Segredo2fa!), codigo, relogio.GetUtcNow(), usuario.UltimoPasso2fa) is not { } passo)
            return ("O código não confere.", []);
        var codigos = GerarCodigosRecuperacao();
        repositorio.Salvar(usuario with { UltimoPasso2fa = passo, CodigosRecuperacao = [.. codigos.Select(c => HashRecuperacao(c)!)] });
        return (null, codigos);
    }

    private static Usuario Sem2fa(Usuario u) =>
        u with { Segredo2fa = null, Segredo2faPendente = null, UltimoPasso2fa = null, CodigosRecuperacao = [] };

    private static List<string> GerarCodigosRecuperacao() =>
        [.. Enumerable.Range(0, QuantidadeCodigosRecuperacao).Select(_ =>
        {
            var c = string.Create(10, 0, (destino, _) =>
            {
                for (var i = 0; i < destino.Length; i++)
                    destino[i] = CaracteresRecuperacao[RandomNumberGenerator.GetInt32(CaracteresRecuperacao.Length)];
            });
            return $"{c[..5]}-{c[5..]}";
        })];

    /// <summary>SHA-256 do código normalizado (sem hífen, espaço ou maiúsculas). O código tem ~49 bits aleatórios: não precisa de PBKDF2.</summary>
    private static string? HashRecuperacao(string? codigo)
    {
        var limpo = new string((codigo ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return limpo.Length == 10 ? Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(limpo))) : null;
    }

    private string Cifrar(byte[] segredo) => Convert.ToBase64String(_cifrador.Cifrar(segredo));

    private byte[] Decifrar(string cifrado) => _cifrador.Decifrar(Convert.FromBase64String(cifrado));

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

    /// <summary>
    /// Exclui um usuário (só o admin chama). Travas: ninguém exclui a si mesmo e o último admin não pode sumir,
    /// senão a plataforma ficaria sem ninguém para administrá-la. Devolve o motivo da recusa, ou null se excluiu.
    /// </summary>
    public string? Excluir(string login, string quemPede)
    {
        if (repositorio.Obter(login) is not { } alvo)
            return "Usuário não encontrado.";
        if (string.Equals(alvo.Login, quemPede, StringComparison.OrdinalIgnoreCase))
            return "Você não pode excluir o seu próprio usuário.";
        if (alvo.Papel == Papeis.Admin && repositorio.Listar().Count(u => u.Papel == Papeis.Admin) <= 1)
            return "Este é o último admin: crie outro admin antes de excluí-lo.";
        return repositorio.Remover(alvo.Login) ? null : "Usuário não encontrado.";
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

    /// <summary>
    /// "Esqueci a senha": gera uma senha temporária nova (mostrada UMA vez), desbloqueia e obriga a troca no próximo
    /// acesso. Só é chamado por quem tem acesso à máquina (rodar.ps1 -RedefinirSenhaAdmin ou o atalho "Esqueci a senha do admin.cmd"), nunca pelo site.
    /// Também desliga o 2FA (celular perdido): o admin é obrigado a cadastrar de novo logo depois de trocar a senha.
    /// </summary>
    public string RedefinirSenha(string login)
    {
        var usuario = repositorio.Obter(login) ?? throw new ArgumentException($"Usuário '{login}' não existe.");
        var senha = GerarSenhaInicial();
        repositorio.Salvar(Sem2fa(usuario) with { HashSenha = HashSenha.Gerar(senha), TrocarSenha = true, TentativasFalhas = 0, BloqueadoAteUtc = null });
        return senha;
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
