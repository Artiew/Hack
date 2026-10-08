using Piersec.Core.Analise;
using Piersec.Core.Modelo;

namespace Piersec.Tests;

public class SimuladorRemediacaoTests
{
    private readonly SimuladorRemediacao _simulador = new(Fabrica.Regras);
    private static Lote LoteExemplo() => Fabrica.ProcessadorReal().Processar([
        Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")]);

    [Fact]
    public void Sem_selecao_preserva_score_e_nao_altera_o_lote()
    {
        var lote = LoteExemplo();
        var eventos = lote.Eventos.ToArray();
        var resultado = _simulador.Simular(lote, []);
        Assert.Equal(lote.Avaliacao!.Score, resultado.Score);
        Assert.Equal(0, resultado.Falhas);
        Assert.Equal(eventos, lote.Eventos);
        Assert.Null(lote.RevisaoDe);
    }

    [Fact]
    public void Todas_as_correcoes_batem_com_a_projecao_do_plano_original()
    {
        var lote = LoteExemplo();
        var correcoes = lote.Avaliacao!.Acoes.Where(SimuladorRemediacao.PodeSimular).ToList();
        Assert.NotEmpty(correcoes);
        var resultado = _simulador.Simular(lote, correcoes.Select(a => a.Ordem));
        Assert.Equal(lote.Avaliacao.ScorePrevisto, resultado.Score.Valor);
        Assert.Equal(lote.Avaliacao.Score.PercentualMedido, resultado.Score.PercentualMedido);
        Assert.Null(resultado.Score.Fatores.Single(f => f.Id == "erroHumano").Valor);
        Assert.True(resultado.Falhas > 0);
    }

    [Fact]
    public void Acoes_sobrepostas_nao_contam_a_mesma_falha_duas_vezes()
    {
        var lote = LoteExemplo();
        var acao = lote.Avaliacao!.Acoes.First(SimuladorRemediacao.PodeSimular);
        lote = lote with { Avaliacao = lote.Avaliacao with { Acoes = [acao with { Ordem = 1 }, acao with { Ordem = 2 }] } };
        var uma = _simulador.Simular(lote, [1]);
        var duas = _simulador.Simular(lote, [1,2,2]);
        Assert.Equal(uma.Falhas, duas.Falhas);
        Assert.Equal(uma.Maquinas, duas.Maquinas);
        Assert.Equal(uma.Score.Valor, duas.Score.Valor);
        Assert.Equal(2, duas.Acoes);
    }

    [Fact]
    public void Investigar_ou_medir_nao_promete_corrigir_falhas()
    {
        var lote = LoteExemplo();
        var medir = lote.Avaliacao!.Acoes.First(a => a.Tipo == PlanejadorAvaliacao.AcaoMedirFatores);
        Assert.False(SimuladorRemediacao.PodeSimular(medir));
        Assert.Throws<ArgumentException>(() => _simulador.Simular(lote, [medir.Ordem]));
        Assert.Throws<ArgumentException>(() => _simulador.Simular(lote, [999]));
    }

    [Fact]
    public void Sem_dados_o_score_permanece_nao_calculado()
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("desconhecido.csv", "coluna", "x")]);
        var resultado = _simulador.Simular(lote, []);
        Assert.Null(resultado.Score.Valor);
        Assert.Equal(0, resultado.Score.PercentualMedido);
    }
}
