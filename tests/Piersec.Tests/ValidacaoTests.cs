using Piersec.Core.Modelo;
using Piersec.Core.Validacao;

namespace Piersec.Tests;

public class ValidacaoTests
{
    private static Lote LoteExemplos() =>
        Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")]);

    private static Verificacao Item(ResultadoValidacao r, string id) => r.Verificacoes.Single(v => v.Id == id);

    [Fact]
    public void Lote_correto_passa_em_todas_as_verificacoes()
    {
        var resultado = Fabrica.Validador().Validar(LoteExemplos());

        Assert.True(resultado.TudoCerto, string.Join("; ", resultado.Verificacoes.Where(v => !v.Aprovada).Select(v => $"{v.Id} {v.Detalhe}")));
        Assert.Equal(11, resultado.Total);
    }

    [Fact]
    public void Arquivo_corrompido_de_proposito_reprova_a_verificacao_de_datas()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("alertas-corrompido-ficticio.csv")]);

        var resultado = Fabrica.Validador().Validar(lote);

        Assert.False(resultado.TudoCerto);
        var datas = Item(resultado, "V04");
        Assert.False(datas.Aprovada);
        Assert.Contains("1 data(s) no futuro", datas.Detalhe);
    }

    [Fact]
    public void Score_adulterado_e_pego_pelo_segundo_caminho()
    {
        var lote = LoteExemplos();
        var adulterado = lote with { Avaliacao = lote.Avaliacao! with { Score = lote.Avaliacao.Score with { Valor = lote.Avaliacao.Score.Valor + 10 } } };

        var resultado = Fabrica.Validador().Validar(adulterado);

        Assert.False(Item(resultado, "V08").Aprovada);
        Assert.False(Item(resultado, "V07").Aprovada);
    }

    [Fact]
    public void Evento_sumido_e_pego_pela_conferencia_de_lidos_por_arquivo()
    {
        var lote = LoteExemplos();
        var semUm = lote with { Eventos = lote.Eventos.Skip(1).ToList() };

        Assert.False(Item(Fabrica.Validador().Validar(semUm), "V02").Aprovada);
    }

    [Fact]
    public void Correlacao_apontando_para_evento_inexistente_reprova()
    {
        var lote = LoteExemplos();
        var quebrada = lote with { Correlacoes = [lote.Correlacoes[0] with { EventoIds = ["EV-99999"] }, .. lote.Correlacoes.Skip(1)] };

        Assert.False(Item(Fabrica.Validador().Validar(quebrada), "V06").Aprovada);
    }

    [Fact]
    public void Fonte_reconhecida_mas_vazia_vira_lacuna_e_nao_zero_problemas()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("vazio.csv",
            "agent.name,host.os.name,package.name,package.version,vulnerability.id,vulnerability.description,vulnerability.severity,vulnerability.score.base,vulnerability.reference")]);

        Assert.Contains(lote.Lacunas, l => l.Arquivo == "vazio.csv" && l.Motivo.Contains("NÃO significa ausência de problemas"));
        Assert.True(Item(Fabrica.Validador().Validar(lote), "V05").Aprovada);
    }

    // ---------- credenciais (S1) ----------

    [Theory]
    [InlineData("acesso com senha=Ficticia123 vazou")]
    [InlineData("password: hunter2")]
    [InlineData("api_key='abc123xyz'")]
    [InlineData("chave AKIAABCDEFGHIJKLMNOP exposta")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----\nMIIBOgIBAAJBAKj\n-----END RSA PRIVATE KEY-----")]
    public void Filtro_encontra_e_mascara_credenciais(string texto)
    {
        Assert.Equal(1, FiltroCredenciais.Contar(texto));
        var mascarado = FiltroCredenciais.Mascarar(texto);
        Assert.Contains(FiltroCredenciais.Mascara, mascarado);
        Assert.Equal(0, FiltroCredenciais.Contar(mascarado));
    }

    [Theory]
    [InlineData("Falha conhecida é uma porta de entrada, sem precisar de senha.")]
    [InlineData("Fortigate: admin password change detected")]
    [InlineData("token de acesso expirado")]
    public void Texto_comum_sobre_senhas_nao_e_confundido_com_credencial(string texto)
    {
        Assert.Equal(0, FiltroCredenciais.Contar(texto));
    }

    [Fact]
    public void Credencial_nos_dados_nunca_chega_ao_relatorio()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("alertas-corrompido-ficticio.csv")]);
        var html = Fabrica.Gerador().Gerar(lote with { Validacao = Fabrica.Validador().Validar(lote) });

        Assert.DoesNotContain("Ficticia123", html);
        Assert.True(ValidadorLote.ConferirRelatorioFinal(html).Aprovada);
    }

    [Fact]
    public void Conferencia_final_reprova_relatorio_com_credencial_crua()
    {
        Assert.False(ValidadorLote.ConferirRelatorioFinal("<p>password=hunter2</p>").Aprovada);
    }

    // ---------- selo no relatório ----------

    [Fact]
    public void Relatorio_valido_mostra_o_selo_de_verificacoes()
    {
        var lote = LoteExemplos();
        var html = Fabrica.Gerador().Gerar(lote with { Validacao = Fabrica.Validador().Validar(lote) });

        Assert.Contains("✔ 11 verificações de consistência, 11 aprovadas", html);
        Assert.Contains("H. Verificações de consistência", html);
        Assert.DoesNotContain("NÃO VALIDADO", html);
    }

    [Fact]
    public void Relatorio_reprovado_sai_marcado_e_nunca_como_se_estivesse_certo()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Exemplo("alertas-corrompido-ficticio.csv")]);
        var html = Fabrica.Gerador().Gerar(lote with { Validacao = Fabrica.Validador().Validar(lote) });

        Assert.Contains("RELATÓRIO NÃO VALIDADO", html);
        Assert.Contains("NÃO devem ser usados para decisão", html);
        Assert.DoesNotContain("verificações de consistência, 11 aprovadas", html);
    }

    [Fact]
    public void Relatorio_retido_nao_mostra_nenhum_dado()
    {
        var lote = LoteExemplos();
        var html = Fabrica.Gerador().GerarRetido(lote);

        Assert.Contains("Relatório retido", html);
        Assert.DoesNotContain("FICT-SRV", html);
        Assert.DoesNotContain("CVE-", html);
    }
}
