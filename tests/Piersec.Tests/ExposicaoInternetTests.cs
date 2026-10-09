using Piersec.Core.Analise;
using Piersec.Core.Modelo;

namespace Piersec.Tests;

public class ExposicaoInternetTests
{
    public const string Cabecalho = "asset.name,asset.os,internet.exposed,observed_at,environment.id";
    private static Lote Processar(params string[] linhas) => Fabrica.ProcessadorTresFontes().Processar([Fabrica.Csv("inventario.csv", [Cabecalho, .. linhas])]);
    private static FatorScore Fator(Lote lote) => lote.Avaliacao!.Score.Fatores.Single(f => f.Id == "exposicao");
    private const string Data = "2026-10-07T09:00:00-03:00";

    [Fact]
    public void Terceira_fonte_normaliza_ativo_data_booleano_e_mantem_origem()
    {
        var lote = Processar($" srv-demo-01 ,Windows Server 2022,sim,{Data},EMPRESA-FICTICIA");
        var e = Assert.Single(lote.Eventos);
        Assert.Equal(TiposEvento.Exposicao, e.Tipo);
        Assert.Equal("SRV-DEMO-01", e.Ativo);
        Assert.True(e.ExpostoInternet);
        Assert.Equal(Severidade.Media, e.Severidade);
        Assert.Contains("não comprova", e.Justificativa);
        Assert.Equal(2, e.Evidencia.Linha);
        Assert.Equal(64, e.Evidencia.Sha256Arquivo.Length);
        Assert.Equal(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero), e.DataUtc);
        Assert.Empty(e.Mitre); // exposição não comprova ataque
        Assert.True(Fabrica.Validador().Validar(lote).TudoCerto);
    }

    [Fact]
    public void Calcula_percentual_por_ativo_sem_inflar_por_repeticao()
    {
        var lote = Processar($"SRV-A,Windows Server,true,{Data},FICT", $"SRV-A,Windows Server,true,2026-10-07T09:01:00-03:00,FICT", $"PC-A,Windows 11,false,{Data},FICT");
        Assert.Equal(50, Fator(lote).Valor);
        Assert.Equal(20, lote.Avaliacao!.Score.PercentualMedido);
        Assert.True(Fabrica.Validador().Validar(lote).TudoCerto);
    }

    [Theory]
    [InlineData("")]
    [InlineData("talvez")]
    [InlineData("-1")]
    public void Valor_invalido_nao_vira_acesso_fechado_nem_zero(string valor)
    {
        var lote = Processar($"SRV-A,Windows Server,{valor},{Data},FICT");
        Assert.Null(Assert.Single(lote.Eventos).ExpostoInternet);
        Assert.Null(Fator(lote).Valor);
        Assert.Null(lote.Avaliacao!.Score.Valor);
        Assert.Contains(lote.Lacunas, l => l.Campo == "internet.exposed");
        Assert.True(Fabrica.Validador().Validar(lote).TudoCerto);
    }

    [Theory]
    [InlineData("", "2026-10-07T09:00:00-03:00")]
    [InlineData("SRV-A", "data-invalida")]
    public void Sem_ativo_ou_data_o_fator_nao_e_medido(string ativo, string data)
    {
        var lote = Processar($"{ativo},Windows Server,true,{data},FICT");
        Assert.Null(Fator(lote).Valor);
        Assert.Contains(lote.Lacunas, l => l.Linha == 2);
        Assert.True(Fabrica.Validador().Validar(lote).TudoCerto);
    }

    [Fact]
    public void Inventario_vazio_nao_prova_exposicao_zero()
    {
        var lote = Processar();
        Assert.Null(Fator(lote).Valor);
        Assert.Contains(lote.Lacunas, l => l.Campo == "arquivo");
        Assert.True(Fabrica.Validador().Validar(lote).TudoCerto);
    }

    [Fact]
    public void Estados_contraditorios_nao_sao_resolvidos_por_escolha_silenciosa()
    {
        var lote = Processar($"SRV-A,Windows Server,true,{Data},FICT", $"SRV-A,Windows Server,false,{Data},FICT");
        Assert.Null(Fator(lote).Valor);
        Assert.Contains(lote.Lacunas, l => l.Motivo.Contains("divergentes"));
        Assert.True(Fabrica.Validador().Validar(lote).TudoCerto);
    }

    [Fact]
    public void Estados_fechados_validos_dao_zero_somente_no_fator_medido()
    {
        var lote = Processar($"SRV-A,Windows Server,false,{Data},FICT");
        Assert.Equal(0, Fator(lote).Valor);
        Assert.Equal(20, lote.Avaliacao!.Score.PercentualMedido);
        Assert.DoesNotContain(lote.Avaliacao.Acoes, a => a.Tipo == PlanejadorAvaliacao.AcaoRevisarExposicao);
    }

    [Fact]
    public void Tres_fontes_aumentam_cobertura_sem_alterar_score_de_lotes_antigos()
    {
        var arquivos = new[] { Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv") };
        var antigo = Fabrica.ProcessadorReal().Processar(arquivos);
        var semInventario = Fabrica.ProcessadorTresFontes().Processar(arquivos);
        Assert.Equal(antigo.Avaliacao!.Score.Valor, semInventario.Avaliacao!.Score.Valor);
        Assert.Equal(55, semInventario.Avaliacao.Score.PercentualMedido);
        var tres = Fabrica.ProcessadorTresFontes().Processar([.. arquivos, Fabrica.Exemplo("exposicao-internet-ficticio.csv")]);
        Assert.Equal(3, tres.Arquivos.Select(x => x.Fonte).Distinct().Count());
        Assert.Equal(75, tres.Avaliacao!.Score.PercentualMedido);
        Assert.True(Fabrica.Validador().Validar(tres).TudoCerto);
        var acao = Assert.Single(tres.Avaliacao.Acoes, a => a.Tipo == PlanejadorAvaliacao.AcaoRevisarExposicao);
        Assert.Empty(acao.EventoIds); // não promete redução só por preparar um plano
        Assert.NotNull(acao.CustoMinimo);
        Assert.Null(acao.QuedaScore);
    }

    [Fact]
    public void Segundo_caminho_detecta_score_adulterado_do_inventario()
    {
        var lote = Processar($"SRV-A,Windows Server,true,{Data},FICT", $"PC-A,Windows 11,false,{Data},FICT");
        lote = lote with { Avaliacao = lote.Avaliacao! with { Score = lote.Avaliacao.Score with { Valor = 0 } } };
        Assert.False(Fabrica.Validador().Validar(lote).Verificacoes.Single(v => v.Id == "V08").Aprovada);
    }
}
