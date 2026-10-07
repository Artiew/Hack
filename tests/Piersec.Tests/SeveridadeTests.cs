using Piersec.Core.Analise;
using Piersec.Core.Modelo;

namespace Piersec.Tests;

public class SeveridadeTests
{
    private static readonly ClassificadorSeveridade Classificador = new(Fabrica.Regras.Severidade);

    private static EventoSeguranca Evento(double? cvss = null, int? nivel = null, string? origem = null) => new()
    {
        Id = "EV-1",
        Fonte = "teste",
        Tipo = "x",
        Ambiente = "teste",
        Ativo = "A",
        Cvss = cvss,
        NivelOrigem = nivel,
        SeveridadeOrigem = origem,
        Descricao = "d",
        Evidencia = new Evidencia("t.csv", "sha", 7),
    };

    [Theory]
    [InlineData(10.0, Severidade.Critica)]
    [InlineData(9.0, Severidade.Critica)]
    [InlineData(8.9, Severidade.Alta)]
    [InlineData(7.0, Severidade.Alta)]
    [InlineData(6.9, Severidade.Media)]
    [InlineData(4.0, Severidade.Media)]
    [InlineData(3.9, Severidade.Baixa)]
    [InlineData(0.0, Severidade.Baixa)]
    public void Faixas_oficiais_do_CVSS(double nota, Severidade esperada)
    {
        Assert.Equal(esperada, Classificador.Classificar(Evento(cvss: nota)).Evento.Severidade);
    }

    [Fact]
    public void Fonte_que_marca_tudo_como_critico_e_corrigida_pela_nota_com_justificativa()
    {
        var (e, lacuna) = Classificador.Classificar(Evento(cvss: 7.5, origem: "Critical"));

        Assert.Equal(Severidade.Alta, e.Severidade);
        Assert.Null(lacuna);
        Assert.Contains("7,5", e.Justificativa);
        Assert.Contains("A fonte marcou \"Critical\"", e.Justificativa);
        Assert.True(e.Justificativa!.Length <= 200, "justificativa longa demais para ler numa tabela");
        Assert.Contains("Não é Crítica porque a nota está abaixo de 9,0", e.Justificativa);
    }

    [Fact]
    public void Critica_nao_traz_frase_de_por_que_nao_e_critica()
    {
        var e = Classificador.Classificar(Evento(cvss: 9.8, origem: "Critical")).Evento;

        Assert.Equal(Severidade.Critica, e.Severidade);
        Assert.DoesNotContain("Não é Crítica", e.Justificativa);
    }

    [Theory]
    [InlineData(15, Severidade.Critica)]
    [InlineData(12, Severidade.Alta)]
    [InlineData(11, Severidade.Media)]
    [InlineData(7, Severidade.Media)]
    [InlineData(6, Severidade.Baixa)]
    [InlineData(0, Severidade.Baixa)]
    public void Faixas_de_nivel_do_alerta(int nivel, Severidade esperada)
    {
        var e = Classificador.Classificar(Evento(nivel: nivel)).Evento;

        Assert.Equal(esperada, e.Severidade);
        Assert.Contains($"nível {nivel}", e.Justificativa);
    }

    [Fact]
    public void Sem_nota_usa_o_texto_da_fonte_e_diz_isso()
    {
        var e = Classificador.Classificar(Evento(origem: "High")).Evento;

        Assert.Equal(Severidade.Alta, e.Severidade);
        Assert.Contains("informada pela fonte", e.Justificativa);
    }

    [Fact]
    public void Sem_nenhuma_informacao_fica_sem_severidade_e_gera_lacuna_na_linha_certa()
    {
        var (e, lacuna) = Classificador.Classificar(Evento(origem: "urgentissimo"));

        Assert.Null(e.Severidade);
        Assert.NotNull(e.Justificativa);
        Assert.Equal(7, lacuna!.Linha);
        Assert.Contains("fora da tabela", lacuna.Motivo);
    }
}

public class RegrasTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("piersec-regras-").FullName;

    public RegrasTests()
    {
        foreach (var arquivo in Directory.GetFiles(Fabrica.PastaRegras))
            File.Copy(arquivo, Path.Combine(_pasta, Path.GetFileName(arquivo)));
    }

    [Fact]
    public void Regras_reais_da_plataforma_sao_validas()
    {
        var regras = ConjuntoRegras.Carregar(Fabrica.PastaRegras);

        Assert.NotEmpty(regras.Correlacoes);
        Assert.Contains(TiposEvento.Vulnerabilidade, regras.FrameworksPorTipo.Keys);
        Assert.Contains(TiposEvento.Alerta, regras.FrameworksPorTipo.Keys);
        // todo controle citado tem nome explicado
        var citados = regras.FrameworksPorTipo.Values.SelectMany(f => f.Nist.Concat(f.Cis).Concat(f.Iso))
            .Concat(regras.Correlacoes.SelectMany(r => r.Frameworks.Nist.Concat(r.Frameworks.Cis).Concat(r.Frameworks.Iso)));
        Assert.All(citados, c => Assert.True(regras.NomesControles.ContainsKey(c), $"controle {c} sem nome em frameworks.json"));
    }

    [Theory]
    [InlineData("\"tipo\": \"mesma_vulnerabilidade_em_varios_ativos\"", "\"tipo\": \"tipo_inventado\"", "não existe")]
    [InlineData("{cve} está em", "{cvee} está em", "{cvee}")]
    [InlineData("\"minimoAtivos\": 10", "\"minimoAtivo\": 10", "minimoAtivos")]
    [InlineData("\"severidade\": \"Critica\"", "\"severidade\": \"Gravissima\"", "severidade")]
    public void Regra_com_erro_impede_a_plataforma_de_subir_com_mensagem_clara(string original, string errado, string trechoDaMensagem)
    {
        var arquivo = Path.Combine(_pasta, "correlacao.json");
        var conteudo = File.ReadAllText(arquivo);
        Assert.Contains(original, conteudo); // garante que o teste mexe em algo que existe
        File.WriteAllText(arquivo, conteudo.Replace(original, errado));

        var erro = Assert.Throws<InvalidOperationException>(() => ConjuntoRegras.Carregar(_pasta));
        Assert.Contains(trechoDaMensagem, erro.Message);
    }

    [Fact]
    public void Faixa_de_CVSS_que_nao_comeca_em_zero_e_recusada()
    {
        var arquivo = Path.Combine(_pasta, "severidade.json");
        File.WriteAllText(arquivo, File.ReadAllText(arquivo).Replace("\"minimo\": 0.0", "\"minimo\": 1.0"));

        Assert.Throws<InvalidOperationException>(() => ConjuntoRegras.Carregar(_pasta));
    }

    public void Dispose() => Directory.Delete(_pasta, recursive: true);
}
