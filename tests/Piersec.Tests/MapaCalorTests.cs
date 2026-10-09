using Piersec.Core.Analise;
using Piersec.Core.Modelo;

namespace Piersec.Tests;

/// <summary>Mapa de calor serviço × gravidade do painel (dados fictícios).</summary>
public class MapaCalorTests
{
    private static Lote LoteExemplo() => Fabrica.ProcessadorReal().Processar([
        Fabrica.Exemplo("vulnerabilidades-ficticio.csv"), Fabrica.Exemplo("alertas-ficticio.csv")]);

    [Fact]
    public void Cada_quadrado_conta_os_eventos_daquele_servico_naquela_gravidade()
    {
        var lote = LoteExemplo();

        var mapa = MapaCalor.Montar(lote);

        Assert.Equal(lote.Servicos.Count, mapa.Count);
        Assert.All(mapa, linha => Assert.Equal(MapaCalor.Colunas, linha.Celulas.Select(c => c.Severidade)));
        foreach (var linha in mapa)
            foreach (var celula in linha.Celulas)
                Assert.Equal(lote.Eventos.Count(e => e.Servico == linha.ServicoId && e.Severidade == celula.Severidade), celula.Hoje);
        // Sem simulação, nada sobra a menos.
        Assert.All(mapa.SelectMany(l => l.Celulas), c => Assert.Equal(c.Hoje, c.Restantes));
        Assert.Equal(lote.Eventos.Count(e => e.Servico is not null && e.Severidade is not null), mapa.Sum(l => l.Celulas.Sum(c => c.Hoje)));
    }

    [Fact]
    public void Servico_de_maior_risco_vem_primeiro()
    {
        var lote = LoteExemplo();

        var mapa = MapaCalor.Montar(lote);

        var notas = mapa.Select(l => lote.Servicos.Single(s => s.Id == l.ServicoId).Nota ?? -1).ToList();
        Assert.Equal(notas.OrderByDescending(n => n), notas);
    }

    [Fact]
    public void Simular_correcoes_esfria_o_mapa_com_os_mesmos_eventos_que_saem_do_score()
    {
        var lote = LoteExemplo();
        var correcoes = lote.Avaliacao!.Acoes.Where(SimuladorRemediacao.PodeSimular).Select(a => a.Ordem).ToList();
        var simulacao = new SimuladorRemediacao(Fabrica.Regras).Simular(lote, correcoes);

        var mapa = MapaCalor.Montar(lote, simulacao.EventosResolvidos);

        var celulas = mapa.SelectMany(l => l.Celulas).ToList();
        Assert.True(celulas.Sum(c => c.Restantes) < celulas.Sum(c => c.Hoje), "corrigir tem de esfriar o mapa");
        Assert.All(celulas, c => Assert.InRange(c.Restantes, 0, c.Hoje));
        var resolvidosNoMapa = lote.Eventos.Count(e => simulacao.EventosResolvidos.Contains(e.Id) && e.Servico is not null && e.Severidade is not null);
        Assert.Equal(celulas.Sum(c => c.Hoje) - celulas.Sum(c => c.Restantes), resolvidosNoMapa);
        Assert.Equal(simulacao.Falhas, simulacao.EventosResolvidos.Count);
    }

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(100, 100, 1)]
    [InlineData(5, 0, 0)]
    public void Intensidade_vai_de_0_a_1(int valor, int maximo, double esperado) =>
        Assert.Equal(esperado, MapaCalor.Intensidade(valor, maximo), 3);

    [Fact]
    public void Intensidade_logaritmica_separa_dez_de_dez_mil()
    {
        var pouco = MapaCalor.Intensidade(10, 10_000);
        Assert.InRange(pouco, 0.2, 0.3);   // numa escala linear seria 0,001: o quadrado ficaria invisível
        Assert.True(MapaCalor.Intensidade(1_000, 10_000) > pouco);
    }
}
