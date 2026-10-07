using Piersec.Core.Coleta;
using Piersec.Core.Modelo;
using Piersec.Core.Padronizacao;

namespace Piersec.Tests;

public class PadronizacaoTests
{
    private static IReadOnlyList<EventoSeguranca> Converter(ContextoConversao contexto, params string[] linhas)
    {
        var tabela = LeitorCsv.Ler(string.Join("\n", [Fabrica.CabecalhoExemplo, .. linhas]));
        return new AdaptadorExemplo().Converter(tabela, contexto);
    }

    [Fact]
    public void Data_invalida_mantem_o_evento_e_registra_lacuna()
    {
        var contexto = Fabrica.Contexto();

        var eventos = Converter(contexto, "1,ontem de tarde,SRV-01,ana,Alta,firewall,teste");

        var evento = Assert.Single(eventos);
        Assert.Null(evento.DataUtc);
        Assert.Contains(contexto.Lacunas, l => l.Campo == "data" && l.Linha == 2);
    }

    [Fact]
    public void Ativo_ausente_vira_lacuna_e_o_evento_nao_some()
    {
        var contexto = Fabrica.Contexto();

        var eventos = Converter(contexto, "1,2026-09-01 10:00,,ana,Alta,firewall,teste");

        Assert.Equal(ContextoConversao.NaoInformado, Assert.Single(eventos).Ativo);
        Assert.Contains(contexto.Lacunas, l => l.Campo == "ativo");
    }

    [Fact]
    public void Severidade_desconhecida_fica_nula_e_vira_lacuna()
    {
        var contexto = Fabrica.Contexto();

        var eventos = Converter(contexto, "1,2026-09-01 10:00,SRV-01,ana,urgentissimo,firewall,teste");

        var evento = Assert.Single(eventos);
        Assert.Null(evento.Severidade);
        Assert.Equal("urgentissimo", evento.SeveridadeOrigem);
        Assert.Contains(contexto.Lacunas, l => l.Campo == "severidade");
    }

    [Theory]
    [InlineData("Crítica", Severidade.Critica)]
    [InlineData("CRITICAL", Severidade.Critica)]
    [InlineData("high", Severidade.Alta)]
    [InlineData("média", Severidade.Media)]
    [InlineData(" Low ", Severidade.Baixa)]
    public void Converte_textos_de_severidade_das_fontes(string texto, Severidade esperada)
    {
        Assert.Equal(esperada, ConversorSeveridade.Converter(texto));
    }

    [Fact]
    public void Usuario_e_anonimizado_de_forma_estavel_e_sem_o_nome_original()
    {
        var anonimizador = Fabrica.Anonimizador();

        var a = anonimizador.Anonimizar("usuario.ficticio01");
        var b = anonimizador.Anonimizar("  USUARIO.Ficticio01 ");
        var outro = anonimizador.Anonimizar("usuario.ficticio02");

        Assert.Equal(a, b);
        Assert.NotEqual(a, outro);
        Assert.StartsWith("USR-", a);
        Assert.DoesNotContain("ficticio", a, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Chave_curta_demais_e_recusada()
    {
        Assert.Throws<ArgumentException>(() => new Anonimizador(new byte[16]));
    }

    [Theory]
    [InlineData("2026-09-01 08:00:00")]
    [InlineData("01/09/2026 08:00")]
    [InlineData("2026-09-01T08:00:00")]
    [InlineData("2026-09-01T11:00:00Z")]
    [InlineData("2026-09-01T08:00:00-03:00")]
    public void Datas_em_formatos_diferentes_viram_o_mesmo_instante_UTC(string texto)
    {
        Assert.True(Fabrica.Datas().TentarNormalizar(texto, out var utc));
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 11, 0, 0, TimeSpan.Zero), utc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("31/02/2026")]
    [InlineData("amanhã")]
    public void Datas_invalidas_sao_recusadas(string texto)
    {
        Assert.False(Fabrica.Datas().TentarNormalizar(texto, out _));
    }

    [Fact]
    public void Nome_de_ativo_e_normalizado()
    {
        Assert.Equal("SRV-FICT-01", ContextoConversao.NormalizarAtivo("  srv-fict-01 "));
        Assert.Equal("PC FICT 07", ContextoConversao.NormalizarAtivo("pc   fict 07"));
    }
}
