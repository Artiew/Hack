using Piersec.Core.Analise;
using Piersec.Core.Modelo;

namespace Piersec.Tests;

/// <summary>
/// MITRE ATT&amp;CK (técnica deduzida, nunca inventada) e custo de correção (faixa com premissa declarada).
/// Os dois respondem a perguntas do manual, e os dois têm de deixar claro o que NÃO sabem.
/// </summary>
public class MitreECustosTests
{
    private const string CabecalhoVuln =
        "agent.name,host.os.name,package.name,package.version,vulnerability.id,vulnerability.description,vulnerability.severity,vulnerability.score.base,vulnerability.reference";
    private const string CabecalhoAlertas = "\"timestamp,agent.name,rule.description,data.devname,rule.level,rule.id\"";

    private static MapaMitre Mapa() => new(Fabrica.Regras.Mitre);

    // ---------------- MITRE ----------------

    [Fact]
    public void Catalogo_tem_as_tecnicas_usadas_pelas_regras_e_cada_uma_se_explica()
    {
        var mitre = Fabrica.Regras.Mitre;
        Assert.NotEmpty(mitre.Catalogo);
        foreach (var (codigo, t) in mitre.Catalogo)
        {
            Assert.StartsWith("T", codigo);
            Assert.False(string.IsNullOrWhiteSpace(t.Nome));
            Assert.False(string.IsNullOrWhiteSpace(t.Tatica));
            Assert.False(string.IsNullOrWhiteSpace(t.Explicacao));
            foreach (var m in t.Mitigacoes)
                Assert.True(mitre.Mitigacoes.ContainsKey(m), $"{codigo} cita a mitigação {m}, que não existe");
        }
    }

    [Fact]
    public void Toda_tecnica_citada_nas_regras_existe_no_catalogo()
    {
        var mapa = Mapa();
        foreach (var regra in Fabrica.Regras.Correlacoes)
            foreach (var t in regra.Frameworks.Mitre)
                Assert.True(mapa.Conhece(t), $"a regra {regra.Id} cita {t}, fora do catálogo");

        foreach (var (tipo, f) in Fabrica.Regras.FrameworksPorTipo)
            foreach (var t in f.Mitre)
                Assert.True(mapa.Conhece(t), $"o tipo {tipo} cita {t}, fora do catálogo");
    }

    [Fact]
    public void Alerta_recebe_a_tecnica_pelo_texto_da_regra_do_firewall()
    {
        var lote = Fabrica.ProcessadorReal().Processar([
            Fabrica.Csv("alertas.csv", CabecalhoAlertas,
                "\"Oct 7,2026 @ 08:00:00.000,FW-01,Port scan detected from external host,fw,7,101\"",
                "\"Oct 7,2026 @ 08:01:00.000,FW-01,SSH brute force attempt,fw,9,102\"")]);

        var scan = lote.Eventos.First(e => e.Descricao.Contains("scan", StringComparison.OrdinalIgnoreCase));
        var forca = lote.Eventos.First(e => e.Descricao.Contains("brute", StringComparison.OrdinalIgnoreCase));

        Assert.Contains("T1046", scan.Mitre);   // descoberta de serviços de rede
        Assert.Contains("T1110", forca.Mitre);  // tentativa de adivinhar senha
    }

    [Fact]
    public void Alerta_que_o_texto_nao_explica_fica_sem_tecnica_em_vez_de_receber_um_palpite()
    {
        var lote = Fabrica.ProcessadorReal().Processar([
            Fabrica.Csv("alertas.csv", CabecalhoAlertas,
                "\"Oct 7,2026 @ 08:00:00.000,FW-01,Evento generico sem pista nenhuma,fw,7,999\"")]);

        Assert.Empty(lote.Eventos.Single().Mitre);
    }

    [Fact]
    public void Vulnerabilidade_recebe_tecnicas_possiveis_com_a_premissa_declarada()
    {
        var lote = Fabrica.ProcessadorReal().Processar([
            Fabrica.Csv("vuln.csv", CabecalhoVuln,
                "SRV-01,Windows Server 2019,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref")]);

        var evento = lote.Eventos.Single();
        Assert.NotEmpty(evento.Mitre);

        var apontadas = Mapa().Apontar(evento);
        Assert.All(apontadas, a => Assert.Equal(ConfiancaMitre.Possivel, a.Confianca));
        Assert.False(string.IsNullOrWhiteSpace(Mapa().Premissa(TiposEvento.Vulnerabilidade)));
    }

    [Fact]
    public void Tecnica_de_alerta_e_provavel_e_diz_por_que_foi_apontada()
    {
        var evento = Fabrica.Alerta("EV-1", "FW-01", "101", Fabrica.Agora) with
        {
            Descricao = "Port scan detected",
        };

        var apontada = Assert.Single(Mapa().Apontar(evento));
        Assert.Equal("T1046", apontada.Tecnica);
        Assert.Equal(ConfiancaMitre.Provavel, apontada.Confianca);
        Assert.Contains("texto do alerta", apontada.Motivo);
    }

    [Fact]
    public void Relatorio_mostra_as_tecnicas_e_declara_que_nao_sao_ataques_observados()
    {
        var lote = Fabrica.ProcessadorReal().Processar([
            Fabrica.Csv("alertas.csv", CabecalhoAlertas,
                "\"Oct 7,2026 @ 08:00:00.000,FW-01,Port scan detected from external host,fw,7,101\"")]);
        var html = Fabrica.Gerador().Gerar(lote with { Validacao = Fabrica.Validador().Validar(lote) });

        Assert.Contains("MITRE", html);
        Assert.Contains("T1046", html);
        Assert.Contains("não dizem qual técnica foi usada", html);
        // Anexo com letra, como os outros, e depois do H (antes ficava sem letra entre o E e o F).
        Assert.Contains("<h2>I. Técnicas de ataque (MITRE ATT&amp;CK)</h2>", html);
        Assert.True(html.IndexOf("H. Verificações", StringComparison.Ordinal) < html.IndexOf("I. Técnicas de ataque", StringComparison.Ordinal));
    }

    // ---------------- custo de correção ----------------

    [Fact]
    public void Custo_cresce_com_o_numero_de_maquinas_e_sempre_vem_em_faixa()
    {
        var custos = new CalculadoraCustos(Fabrica.Regras.Custos);

        var poucas = custos.Calcular(PlanejadorAvaliacao.AcaoAtualizarProgramas, 1);
        var muitas = custos.Calcular(PlanejadorAvaliacao.AcaoAtualizarProgramas, 200);

        Assert.NotNull(poucas);
        Assert.NotNull(muitas);
        Assert.True(poucas!.Minimo <= poucas.Maximo, "o mínimo não pode passar do máximo");
        Assert.True(muitas!.Maximo > poucas.Maximo, "mais máquinas tem de custar mais");
    }

    [Fact]
    public void Acao_pequena_nao_custa_zero_reais()
    {
        // Arredondar para baixo até zero diria "sai de graça" para um trabalho que existe. O piso é um passo.
        var custos = new CalculadoraCustos(Fabrica.Regras.Custos);
        var faixa = custos.Calcular(PlanejadorAvaliacao.AcaoAtualizarProgramas, 1);

        Assert.NotNull(faixa);
        Assert.True(faixa!.Minimo > 0, "trabalho que existe não pode custar R$ 0");
        Assert.True(faixa.Maximo >= faixa.Minimo);
    }

    [Fact]
    public void Tipo_de_acao_sem_premissa_de_custo_nao_inventa_valor()
    {
        var custos = new CalculadoraCustos(Fabrica.Regras.Custos);
        Assert.Null(custos.Calcular("acao_que_nao_existe", 10));
    }

    [Fact]
    public void Toda_premissa_de_custo_esta_declarada_nas_regras()
    {
        var custos = Fabrica.Regras.Custos;
        Assert.False(string.IsNullOrWhiteSpace(custos.HoraTecnica.Premissa));
        Assert.NotEmpty(custos.HorasPorAcao);
        foreach (var (tipo, h) in custos.HorasPorAcao)
            Assert.False(string.IsNullOrWhiteSpace(h.Premissa), $"{tipo} sem premissa declarada");
    }

    [Fact]
    public void Soma_de_faixas_ignora_acao_sem_premissa_e_e_nula_quando_nenhuma_tem()
    {
        var soma = CalculadoraCustos.Somar([new FaixaCusto(1000, 2000), null, new FaixaCusto(500, 900)]);
        Assert.Equal(1500, soma!.Minimo);
        Assert.Equal(2900, soma.Maximo);

        Assert.Null(CalculadoraCustos.Somar([null, null]));
    }

    // ---------------- quanto cada ação melhora o score ----------------

    [Fact]
    public void Cada_acao_do_plano_diz_quanto_derruba_do_score_e_quanto_custa()
    {
        var lote = Fabrica.ProcessadorReal().Processar([
            Fabrica.Csv("vuln.csv", CabecalhoVuln,
                "SRV-01,Windows Server 2019,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref",
                "SRV-02,Windows Server 2019,openssl,1.0,CVE-2099-0002,d,Critical,9.8,ref",
                "PC-01,Windows 10,openssl,1.0,CVE-2099-0003,d,Critical,9.8,ref",
                "PC-02,Windows 10,java,8.0,CVE-2099-0004,d,Critical,9.8,ref")]);

        var avaliacao = lote.Avaliacao!;
        var acao = avaliacao.Acoes.First(a => a.Tipo == PlanejadorAvaliacao.AcaoAtualizarProgramas);

        Assert.True(acao.QuedaScore is > 0, "atualizar o programa com mais falhas tem de derrubar o score");
        Assert.True(acao.PercentualScore is > 0 and <= 100);
        Assert.True(acao.CustoMinimo is > 0 && acao.CustoMaximo >= acao.CustoMinimo);
        Assert.True(avaliacao.CustoTotalMinimo is > 0, "o plano inteiro tem de ter custo total");
    }

    [Fact]
    public void Relatorio_mostra_a_melhora_no_score_o_custo_e_o_que_nao_esta_incluido()
    {
        var lote = Fabrica.ProcessadorReal().Processar([
            Fabrica.Csv("vuln.csv", CabecalhoVuln,
                "SRV-01,Windows Server 2019,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref",
                "PC-01,Windows 10,openssl,1.0,CVE-2099-0002,d,Critical,9.8,ref")]);
        var html = Fabrica.Gerador().Gerar(lote with { Validacao = Fabrica.Validador().Validar(lote) });

        Assert.Contains("Melhora no score", html);
        Assert.Contains("Custo estimado", html);
        Assert.Contains("Quanto custa corrigir", html);
        Assert.Contains("Não está incluído", html);
        Assert.Contains("Peso de cada problema no score", html);
    }
}
