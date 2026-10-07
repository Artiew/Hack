using Piersec.Core.Seguranca;

namespace Piersec.Tests;

public class SegurancaTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("piersec-seg-").FullName;
    private readonly RelogioAjustavel _relogio = new(Fabrica.Agora);
    private readonly RepositorioUsuarios _repo;
    private readonly ServicoAutenticacao _servico;

    public SegurancaTests()
    {
        _repo = new RepositorioUsuarios(_pasta);
        _servico = new ServicoAutenticacao(_repo, _relogio);
    }

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    // ---------- hash ----------

    [Fact]
    public void Hash_confere_a_senha_certa_e_recusa_a_errada()
    {
        var hash = HashSenha.Gerar("Senha-Forte-123");

        Assert.True(HashSenha.Verificar("Senha-Forte-123", hash));
        Assert.False(HashSenha.Verificar("senha-forte-123", hash));
        Assert.DoesNotContain("Senha-Forte-123", hash);
        Assert.StartsWith("PBKDF2-SHA256$600000$", hash);
    }

    [Fact]
    public void Mesma_senha_gera_hashes_diferentes_por_causa_do_sal()
    {
        Assert.NotEqual(HashSenha.Gerar("igual-123456"), HashSenha.Gerar("igual-123456"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("lixo")]
    [InlineData("PBKDF2-SHA256$abc$xx$yy")]
    public void Hash_mal_formado_nunca_e_aceito(string gravado)
    {
        Assert.False(HashSenha.Verificar("qualquer", gravado));
    }

    // ---------- primeiro acesso ----------

    [Fact]
    public void Primeira_execucao_cria_admin_com_senha_do_ambiente_de_exatamente_8_caracteres()
    {
        var (criado, sorteada) = _servico.GarantirAdmin("Ab3$efgH");

        Assert.True(criado);
        Assert.Null(sorteada); // veio do ambiente: nada a mostrar
        Assert.Equal(ResultadoEntrada.PrecisaTrocarSenha, _servico.Entrar("admin", "Ab3$efgH").Resultado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("curta")]
    [InlineData("comprida-demais")]
    public void Sem_senha_valida_no_ambiente_sorteia_8_caracteres(string? doAmbiente)
    {
        var (_, sorteada) = _servico.GarantirAdmin(doAmbiente);

        Assert.NotNull(sorteada);
        Assert.Equal(ServicoAutenticacao.TamanhoSenhaInicial, sorteada.Length);
        Assert.Equal(ResultadoEntrada.PrecisaTrocarSenha, _servico.Entrar("admin", sorteada).Resultado);
    }

    [Fact]
    public void Admin_so_e_criado_quando_nao_ha_usuarios()
    {
        _servico.GarantirAdmin("Ab3$efgH");

        Assert.False(_servico.GarantirAdmin("Zz9#zzzz").Criado);
    }

    [Fact]
    public void Senha_nunca_e_gravada_no_arquivo_de_usuarios()
    {
        _servico.GarantirAdmin("Ab3$efgH");

        Assert.DoesNotContain("Ab3$efgH", File.ReadAllText(Path.Combine(_pasta, "usuarios.json")));
    }

    // ---------- login ----------

    [Fact]
    public void Erro_e_igual_para_usuario_inexistente_e_senha_errada()
    {
        _servico.GarantirAdmin("Ab3$efgH");

        var inexistente = _servico.Entrar("ninguem", "Ab3$efgH");
        var senhaErrada = _servico.Entrar("admin", "errada00");

        Assert.Equal(ResultadoEntrada.Falhou, inexistente.Resultado);
        Assert.Equal(ResultadoEntrada.Falhou, senhaErrada.Resultado);
    }

    [Fact]
    public void Cinco_erros_seguidos_bloqueiam_por_15_minutos_mesmo_com_a_senha_certa()
    {
        _servico.GarantirAdmin("Ab3$efgH");
        for (var i = 0; i < ServicoAutenticacao.MaximoTentativas - 1; i++)
            Assert.False(_servico.Entrar("admin", "errada00").Bloqueado);

        Assert.True(_servico.Entrar("admin", "errada00").Bloqueado);
        Assert.Equal(ResultadoEntrada.Falhou, _servico.Entrar("admin", "Ab3$efgH").Resultado);

        _relogio.Avancar(ServicoAutenticacao.TempoBloqueio + TimeSpan.FromSeconds(1));
        Assert.Equal(ResultadoEntrada.PrecisaTrocarSenha, _servico.Entrar("admin", "Ab3$efgH").Resultado);
    }

    [Fact]
    public void Login_ignora_maiusculas_e_espacos()
    {
        _servico.GarantirAdmin("Ab3$efgH");

        Assert.NotEqual(ResultadoEntrada.Falhou, _servico.Entrar("  ADMIN ", "Ab3$efgH").Resultado);
    }

    // ---------- troca de senha ----------

    [Fact]
    public void Depois_de_trocar_a_senha_o_acesso_e_normal()
    {
        _servico.GarantirAdmin("Ab3$efgH");

        Assert.Null(_servico.TrocarSenha("admin", "Ab3$efgH", "Nova-Senha-2026", "Nova-Senha-2026"));
        Assert.Equal(ResultadoEntrada.Ok, _servico.Entrar("admin", "Nova-Senha-2026").Resultado);
        Assert.Equal(ResultadoEntrada.Falhou, _servico.Entrar("admin", "Ab3$efgH").Resultado);
    }

    [Theory]
    [InlineData("curta1", "precisa ter pelo menos 8")]
    [InlineData("SomenteLetrasAqui", "letras e também números")]
    [InlineData("admin-2026-senha", "não pode conter o nome de usuário")]
    public void Nova_senha_fraca_e_recusada_com_motivo(string nova, string motivo)
    {
        _servico.GarantirAdmin("Ab3$efgH");

        Assert.Contains(motivo, _servico.TrocarSenha("admin", "Ab3$efgH", nova, nova));
    }

    [Fact]
    public void Troca_exige_a_senha_atual_e_a_confirmacao()
    {
        _servico.GarantirAdmin("Ab3$efgH");

        Assert.Contains("senha atual", _servico.TrocarSenha("admin", "errada00", "Nova-Senha-2026", "Nova-Senha-2026"));
        Assert.Contains("confirmação", _servico.TrocarSenha("admin", "Ab3$efgH", "Nova-Senha-2026", "Outra-Senha-2026"));
    }

    [Fact]
    public void Redefinir_senha_gera_temporaria_desbloqueia_e_obriga_troca()
    {
        _servico.GarantirAdmin("Ab3$efgH");
        _servico.TrocarSenha("admin", "Ab3$efgH", "Nova-Senha-2026", "Nova-Senha-2026");
        for (var i = 0; i < ServicoAutenticacao.MaximoTentativas; i++)
            _servico.Entrar("admin", "errada00"); // bloqueia

        var temporaria = _servico.RedefinirSenha("admin");

        Assert.Equal(ServicoAutenticacao.TamanhoSenhaInicial, temporaria.Length);
        Assert.Equal(ResultadoEntrada.PrecisaTrocarSenha, _servico.Entrar("admin", temporaria).Resultado);
        Assert.Equal(ResultadoEntrada.Falhou, _servico.Entrar("admin", "Nova-Senha-2026").Resultado);
    }

    // ---------- criação de usuários ----------

    [Fact]
    public void Novo_usuario_recebe_senha_inicial_de_8_caracteres_e_precisa_trocar()
    {
        var (usuario, senha) = _servico.Criar("Maria.Silva", Papeis.Analista);

        Assert.Equal("maria.silva", usuario.Login);
        Assert.Equal(8, senha.Length);
        Assert.Equal(ResultadoEntrada.PrecisaTrocarSenha, _servico.Entrar("maria.silva", senha).Resultado);
    }

    [Theory]
    [InlineData("ab", Papeis.Analista)]
    [InlineData("nome com espaço", Papeis.Analista)]
    [InlineData("valido", "chefe")]
    public void Login_ou_papel_invalido_e_recusado(string login, string papel)
    {
        Assert.Throws<ArgumentException>(() => _servico.Criar(login, papel));
    }

    // ---------- auditoria ----------

    [Fact]
    public void Auditoria_registra_em_ordem_e_marca_login_desconhecido()
    {
        var auditoria = new Auditoria(_pasta, _relogio);

        auditoria.Registrar("admin", "entrada");
        auditoria.Registrar(null, "entrada_falhou");

        var ultimos = auditoria.Ultimos(10);
        Assert.Equal("entrada_falhou", ultimos[0].Acao);
        Assert.Equal(Auditoria.Desconhecido, ultimos[0].Usuario);
        Assert.Equal("admin", ultimos[1].Usuario);
    }
}

internal sealed class RelogioAjustavel(DateTimeOffset inicio) : TimeProvider
{
    private DateTimeOffset _agora = inicio;
    public override DateTimeOffset GetUtcNow() => _agora;
    public void Avancar(TimeSpan quanto) => _agora += quanto;
}
