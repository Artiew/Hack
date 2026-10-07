using Piersec.Core.Consulta;
using Piersec.Core.Modelo;

namespace Piersec.Tests;

/// <summary>Visão técnica: filtros que transformam um número da tela nos eventos que o formam (RG2).</summary>
public class ConsultaTests
{
    private const string CabecalhoVuln =
        "agent.name,host.os.name,package.name,package.version,vulnerability.id,vulnerability.description,vulnerability.severity,vulnerability.score.base,vulnerability.reference";
    private const string CabecalhoAlertas = "\"timestamp,agent.name,rule.description,data.devname,rule.level,rule.id\"";

    private static readonly Lote L = Fabrica.ProcessadorReal().Processar([
        Fabrica.Csv("vuln.csv", CabecalhoVuln,
            "DB-01,Windows Server 2019,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref",          // linha 2
            "DB-01,Windows Server 2019,java,8.0,CVE-2099-0002,d,High,7.5,ref",                 // linha 3
            "PC-Ação,Windows 10,zoom,5.0,CVE-2099-0003,d,Low,2.0,ref"),                        // linha 4
        Fabrica.Csv("alertas.csv", CabecalhoAlertas,
            "\"Oct 7,2026 @ 08:00:00.000,coletor,desc,FW-01,12,5710\"",
            "\"Oct 7,2026 @ 08:00:05.000,coletor,desc,FW-01,3,5402\"")]);

    private static IReadOnlyList<EventoSeguranca> F(FiltroEventos f) => ConsultaEventos.Filtrar(L, f);

    [Fact]
    public void Linha_de_origem_conta_o_cabecalho_como_linha_1()
    {
        var vulns = L.Eventos.Where(e => e.Tipo == TiposEvento.Vulnerabilidade).OrderBy(e => e.Evidencia.Linha).ToList();
        Assert.Equal([2L, 3L, 4L], vulns.Select(e => e.Evidencia.Linha));
        Assert.Equal("openssl", vulns[0].Pacote);
    }

    [Fact]
    public void Filtro_vazio_traz_tudo_do_mais_grave_para_o_menos_grave()
    {
        var todos = F(new FiltroEventos());

        Assert.Equal(L.Eventos.Count, todos.Count);
        Assert.Equal(Severidade.Critica, todos[0].Severidade);
        Assert.Equal("todos os eventos", ConsultaEventos.Descrever(L, new FiltroEventos()));
    }

    [Fact]
    public void Filtra_por_severidade_servico_tipo_regra_e_arquivo()
    {
        Assert.All(F(new FiltroEventos { Severidade = "Critica" }), e => Assert.Equal(Severidade.Critica, e.Severidade));
        Assert.Equal(2, F(new FiltroEventos { Servico = "dados-clientes" }).Count);
        Assert.Equal(2, F(new FiltroEventos { Tipo = TiposEvento.Alerta }).Count);
        Assert.Single(F(new FiltroEventos { Regra = "5710" }));
        Assert.Equal(3, F(new FiltroEventos { Arquivo = "vuln.csv" }).Count);
        Assert.Equal(2, F(new FiltroEventos { Ambiente = "alertas" }).Count);
    }

    [Fact]
    public void Maquina_e_texto_ignoram_maiusculas_e_acentos()
    {
        Assert.Single(F(new FiltroEventos { Ativo = "pc-acao" }));
        Assert.Single(F(new FiltroEventos { Texto = "OPENSSL" }));
        Assert.Single(F(new FiltroEventos { Texto = "cve-2099-0003" }));
    }

    [Fact]
    public void Correlacao_e_acao_do_plano_levam_aos_eventos_que_as_formam()
    {
        var acao = L.Avaliacao!.Acoes.First(a => a.EventoIds.Count > 0);
        Assert.Equal(acao.EventoIds.Order(StringComparer.Ordinal), F(new FiltroEventos { Acao = acao.Ordem }).Select(e => e.Id).Order(StringComparer.Ordinal));

        var correlacao = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("vuln.csv", [CabecalhoVuln,
            .. Enumerable.Range(1, 6).Select(i => $"DB-01,Windows Server 2019,openssl,1.0,CVE-2099-000{i},d,Critical,9.8,ref")])]);
        var c = correlacao.Correlacoes.First();
        Assert.Equal(c.EventoIds.Count, ConsultaEventos.Filtrar(correlacao, new FiltroEventos { Correlacao = c.Id }).Count);
        Assert.Contains(c, ConsultaEventos.CorrelacoesDo(correlacao, c.EventoIds[0]));
    }

    [Fact]
    public void Valor_invalido_na_URL_nao_vira_tudo()
    {
        Assert.Empty(F(new FiltroEventos { Severidade = "Gravissima" }));       // não "ignora o filtro" e mostra tudo
        Assert.Empty(F(new FiltroEventos { Correlacao = "COR-99999" }));
        Assert.Empty(F(new FiltroEventos { Acao = 99 }));
        var longo = new FiltroEventos { Ativo = new string('x', 5000) }.Limpo();
        Assert.Equal(FiltroEventos.TamanhoMaximoTexto, longo.Ativo!.Length);
    }

    [Fact]
    public void Filtro_em_palavras_para_o_titulo()
    {
        var texto = ConsultaEventos.Descrever(L, new FiltroEventos { Severidade = "Critica", Servico = "dados-clientes" });

        Assert.Equal("severidade Crítica · serviço Dados de clientes", texto);
    }
}
