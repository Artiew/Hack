using System.Text;
using Piersec.Core.Seguranca;

namespace Piersec.Tests;

public class DoisFatoresTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("piersec-2fa-").FullName;
    private readonly RelogioAjustavel _relogio = new(Fabrica.Agora);
    private readonly RepositorioUsuarios _repo;
    private readonly ServicoAutenticacao _servico;

    public DoisFatoresTests()
    {
        _repo = new RepositorioUsuarios(_pasta);
        _servico = new ServicoAutenticacao(_repo, _relogio, new CifradorDeTeste());
    }

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    // ---------- código do app (TOTP) ----------

    [Theory] // RFC 6238, apêndice B (SHA-1): os 6 últimos dígitos do código de 8
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(1111111111, "050471")]
    [InlineData(1234567890, "005924")]
    [InlineData(2000000000, "279037")]
    public void Codigo_bate_com_os_vetores_oficiais_da_RFC_6238(long segundos, string esperado)
    {
        var segredo = Encoding.ASCII.GetBytes("12345678901234567890");

        Assert.Equal(esperado, Totp.Codigo(segredo, Totp.Passo(DateTimeOffset.FromUnixTimeSeconds(segundos))));
    }

    [Fact]
    public void Chave_em_base32_bate_com_a_RFC_4648()
    {
        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", Base32.Codificar(Encoding.ASCII.GetBytes("12345678901234567890")));
        Assert.Equal("MZXW6YQ", Base32.Codificar("foob"u8));
        Assert.Equal("GEZD GNBV GY3T", Base32.EmGrupos("GEZDGNBVGY3T"));
    }

    [Fact]
    public void Aceita_um_passo_de_diferenca_no_relogio_mas_nao_dois_nem_o_mesmo_codigo_duas_vezes()
    {
        var segredo = Totp.GerarSegredo();
        var agora = Fabrica.Agora;
        var passo = Totp.Passo(agora);

        Assert.Equal(passo - 1, Totp.Verificar(segredo, Totp.Codigo(segredo, passo - 1), agora, null)); // celular atrasado
        Assert.Equal(passo + 1, Totp.Verificar(segredo, Totp.Codigo(segredo, passo + 1), agora, null)); // celular adiantado
        Assert.Null(Totp.Verificar(segredo, Totp.Codigo(segredo, passo + 2), agora, null));
        Assert.Null(Totp.Verificar(segredo, Totp.Codigo(segredo, passo), agora, ultimoPassoUsado: passo)); // já usado
        Assert.Null(Totp.Verificar(segredo, "12345", agora, null));
        Assert.Null(Totp.Verificar(segredo, "abcdef", agora, null));
    }

    [Fact]
    public void Endereco_do_QR_code_tem_emissor_conta_e_chave()
    {
        var endereco = Totp.Endereco("Piersec", "admin", Encoding.ASCII.GetBytes("12345678901234567890"));

        Assert.Equal("otpauth://totp/Piersec:admin?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&issuer=Piersec&digits=6&period=30", endereco);
    }

    // ---------- QR code ----------

    [Fact]
    public void Correcao_de_erro_bate_com_o_exemplo_da_norma_do_QR_code()
    {
        // ISO/IEC 18004, anexo I: "01234567", versão 1-M.
        byte[] dados = [32, 91, 11, 120, 209, 114, 220, 77, 67, 64, 236, 17, 236, 17, 236, 17];

        var correcao = ReedSolomon.Resto(dados, ReedSolomon.Divisor(10));

        Assert.Equal(new byte[] { 196, 35, 39, 119, 235, 215, 231, 226, 93, 23 }, correcao);
    }

    [Fact]
    public void Bits_de_formato_e_de_versao_batem_com_as_tabelas_da_norma()
    {
        Assert.Equal(0b101010000010010, QrCode.BitsFormato(0)); // nível M, máscara 0
        Assert.Equal(0b100000011001110, QrCode.BitsFormato(5)); // nível M, máscara 5
        Assert.Equal(0b100101010100000, QrCode.BitsFormato(7)); // nível M, máscara 7
        Assert.Equal(0x07C94, QrCode.BitsVersao(7));
        Assert.Equal(0x0A4D3, QrCode.BitsVersao(10));
    }

    [Fact]
    public void QR_code_do_endereco_do_2FA_tem_tamanho_certo_e_os_tres_quadrados_de_posicao()
    {
        var endereco = Totp.Endereco("Piersec", "administrador.longo", Totp.GerarSegredo());
        var qr = QrCode.Gerar(endereco);

        Assert.InRange(qr.Versao, 5, 7);
        Assert.Equal(qr.Versao * 4 + 17, qr.Tamanho);
        foreach (var (linha, coluna) in new[] { (0, 0), (0, qr.Tamanho - 7), (qr.Tamanho - 7, 0) })
        {
            Assert.True(qr.Escuro(linha, coluna));             // canto do quadrado: escuro
            Assert.False(qr.Escuro(linha + 1, coluna + 1));     // anel claro
            Assert.True(qr.Escuro(linha + 3, coluna + 3));     // centro: escuro
        }
        Assert.True(qr.Escuro(qr.Tamanho - 8, 8));               // módulo sempre escuro
        Assert.Contains("viewBox", qr.Svg("teste"));
        Assert.Throws<ArgumentException>(() => QrCode.Gerar(new string('x', 300)));
    }

    // ---------- login com 2FA ----------

    private string AdminCom2fa()
    {
        _servico.GarantirAdmin("Ab3$efgH");
        _servico.TrocarSenha("admin", "Ab3$efgH", "Nova-Senha-2026", "Nova-Senha-2026");
        var (chave, _) = _servico.Iniciar2fa("admin");
        Assert.Null(_servico.Confirmar2fa("admin", CodigoAgora(chave)).Erro);
        _relogio.Avancar(Totp.Periodo); // o código usado na ativação não vale de novo
        return chave;
    }

    private string CodigoAgora(string chave) => Totp.Codigo(DecodificarBase32(chave), Totp.Passo(_relogio.GetUtcNow()));

    [Fact]
    public void Admin_e_obrigado_a_configurar_e_os_outros_escolhem()
    {
        _servico.GarantirAdmin("Ab3$efgH");
        var (analista, _) = _servico.Criar("bia", Papeis.Analista);

        Assert.True(ServicoAutenticacao.PrecisaConfigurar2fa(_repo.Obter("admin")!));
        Assert.False(ServicoAutenticacao.PrecisaConfigurar2fa(analista));
    }

    [Fact]
    public void Com_2FA_a_senha_sozinha_nao_entra_e_o_codigo_certo_entra()
    {
        var chave = AdminCom2fa();

        Assert.Equal(ResultadoEntrada.PrecisaSegundoFator, _servico.Entrar("admin", "Nova-Senha-2026").Resultado);
        var codigo = CodigoAgora(chave);
        Assert.True(_servico.ConfirmarSegundoFator("admin", codigo).Ok);
        Assert.False(_servico.ConfirmarSegundoFator("admin", codigo).Ok); // o mesmo código não vale duas vezes
    }

    [Fact]
    public void Codigos_errados_bloqueiam_e_a_senha_certa_nao_zera_as_tentativas()
    {
        var chave = AdminCom2fa();

        for (var i = 0; i < ServicoAutenticacao.MaximoTentativas - 1; i++)
        {
            Assert.Equal(ResultadoEntrada.PrecisaSegundoFator, _servico.Entrar("admin", "Nova-Senha-2026").Resultado);
            Assert.False(_servico.ConfirmarSegundoFator("admin", "000000").Bloqueado);
        }
        Assert.True(_servico.ConfirmarSegundoFator("admin", "000000").Bloqueado);
        Assert.False(_servico.ConfirmarSegundoFator("admin", CodigoAgora(chave)).Ok); // bloqueado: nem o certo
        Assert.Equal(ResultadoEntrada.Falhou, _servico.Entrar("admin", "Nova-Senha-2026").Resultado);
    }

    [Fact]
    public void Codigo_de_recuperacao_entra_uma_vez_so()
    {
        _servico.GarantirAdmin("Ab3$efgH");
        var (chave, _) = _servico.Iniciar2fa("admin");
        var (_, codigos) = _servico.Confirmar2fa("admin", Totp.Codigo(DecodificarBase32(chave), Totp.Passo(_relogio.GetUtcNow())));

        Assert.Equal(ServicoAutenticacao.QuantidadeCodigosRecuperacao, codigos.Count);
        var usado = _servico.ConfirmarSegundoFator("admin", codigos[0].ToUpperInvariant().Replace("-", " ")); // sem diferença de maiúsculas/hífen
        Assert.True(usado.Ok);
        Assert.True(usado.UsouRecuperacao);
        Assert.Equal(7, usado.RecuperacaoRestantes);
        Assert.False(_servico.ConfirmarSegundoFator("admin", codigos[0]).Ok);
    }

    [Fact]
    public void Chave_e_codigos_nunca_ficam_abertos_no_arquivo_de_usuarios()
    {
        _servico.GarantirAdmin("Ab3$efgH");
        var (chave, _) = _servico.Iniciar2fa("admin");
        Assert.Equal(chave, _servico.Iniciar2fa("admin").Chave); // recarregar a página não troca o QR code
        var (_, codigos) = _servico.Confirmar2fa("admin", Totp.Codigo(DecodificarBase32(chave), Totp.Passo(_relogio.GetUtcNow())));

        var arquivo = File.ReadAllText(Path.Combine(_pasta, "usuarios.json"));
        Assert.DoesNotContain(chave, arquivo);
        Assert.DoesNotContain(Convert.ToBase64String(DecodificarBase32(chave)), arquivo);
        Assert.All(codigos, c => Assert.DoesNotContain(c, arquivo));
        Assert.DoesNotContain(codigos[0].Replace("-", ""), arquivo);
    }

    [Fact]
    public void Ativacao_exige_um_codigo_da_chave_nova()
    {
        _servico.GarantirAdmin("Ab3$efgH");
        _servico.Iniciar2fa("admin");

        Assert.Contains("não confere", _servico.Confirmar2fa("admin", "000000").Erro);
        Assert.False(_repo.Obter("admin")!.Tem2fa);
    }

    [Fact]
    public void Admin_nao_desliga_o_2FA_e_analista_desliga_com_o_codigo()
    {
        var chaveAdmin = AdminCom2fa();
        _servico.Criar("bia", Papeis.Analista);
        var (chaveBia, _) = _servico.Iniciar2fa("bia");
        _servico.Confirmar2fa("bia", CodigoAgora(chaveBia));
        _relogio.Avancar(Totp.Periodo);

        Assert.Contains("obrigatória", _servico.Desativar2fa("admin", CodigoAgora(chaveAdmin)));
        Assert.Contains("não confere", _servico.Desativar2fa("bia", "000000"));
        Assert.Null(_servico.Desativar2fa("bia", CodigoAgora(chaveBia)));
        Assert.False(_repo.Obter("bia")!.Tem2fa);
    }

    [Fact]
    public void Codigos_de_recuperacao_novos_invalidam_os_antigos()
    {
        _servico.GarantirAdmin("Ab3$efgH");
        var (chave, _) = _servico.Iniciar2fa("admin");
        var (_, antigos) = _servico.Confirmar2fa("admin", CodigoAgora(chave));
        _relogio.Avancar(Totp.Periodo);

        var (erro, novos) = _servico.NovosCodigosRecuperacao("admin", CodigoAgora(chave));

        Assert.Null(erro);
        Assert.Empty(antigos.Intersect(novos));
        Assert.False(_servico.ConfirmarSegundoFator("admin", antigos[0]).Ok);
        Assert.True(_servico.ConfirmarSegundoFator("admin", novos[0]).Ok);
    }

    [Fact]
    public void Esqueci_a_senha_do_admin_tambem_desliga_o_2FA_e_obriga_cadastrar_de_novo()
    {
        AdminCom2fa();

        var temporaria = _servico.RedefinirSenha("admin");

        Assert.Equal(ResultadoEntrada.PrecisaTrocarSenha, _servico.Entrar("admin", temporaria).Resultado);
        Assert.True(ServicoAutenticacao.PrecisaConfigurar2fa(_repo.Obter("admin")!));
    }

    private static byte[] DecodificarBase32(string texto)
    {
        const string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in texto.Replace(" ", ""))
        {
            buffer = (buffer << 5) | alfabeto.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        return [.. bytes];
    }
}
