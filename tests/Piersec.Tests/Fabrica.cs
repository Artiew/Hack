using System.Text;
using System.Text.Json;
using Piersec.Core.Analise;
using Piersec.Core.Coleta;
using Piersec.Core.Modelo;
using Piersec.Core.Padronizacao;
using Piersec.Core.Pipeline;
using Piersec.Core.Relatorio;

namespace Piersec.Tests;

/// <summary>Monta as peças da plataforma para os testes, sempre com dados fictícios.</summary>
internal static class Fabrica
{
    public static readonly byte[] ChaveFixa = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    public static readonly DateTimeOffset Agora = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    public const string CabecalhoExemplo = "id_evento,data_hora,ativo,usuario,severidade,tipo,descricao";

    public static string PastaRegras => Path.Combine(AppContext.BaseDirectory, "regras");
    public static string PastaExemplos => Path.Combine(AppContext.BaseDirectory, "exemplos");

    /// <summary>As regras reais da plataforma (regras/*.json).</summary>
    public static ConjuntoRegras Regras { get; } = ConjuntoRegras.Carregar(PastaRegras);

    public static Anonimizador Anonimizador() => new(ChaveFixa);
    public static NormalizadorDatas Datas() => new(NormalizadorDatas.FusoBrasilia);

    /// <summary>Processador com a fonte fictícia simples (usada nos testes de base).</summary>
    public static ProcessadorLote Processador() =>
        new([new AdaptadorExemplo()], Anonimizador(), Datas(), Regras, new RelogioFixo(Agora));

    /// <summary>Processador com os adaptadores das fontes reais do desafio.</summary>
    public static ProcessadorLote ProcessadorReal() =>
        new([new AdaptadorVulnerabilidades(), new AdaptadorAlertasSiem()], Anonimizador(), Datas(), Regras, new RelogioFixo(Agora));

    public static ProcessadorLote ProcessadorTresFontes() =>
        new([new AdaptadorVulnerabilidades(), new AdaptadorAlertasSiem(), new AdaptadorExposicaoInternet()], Anonimizador(), Datas(), Regras, new RelogioFixo(Agora));

    public static GeradorRelatorioHtml Gerador() => new(NormalizadorDatas.FusoBrasilia, Regras);

    public static Core.Validacao.ValidadorLote Validador() => new(Regras, new RelogioFixo(Agora));

    public static ArquivoEnviado Csv(string nome, params string[] linhas) =>
        ArquivoEnviado.De(nome, Encoding.UTF8.GetBytes(string.Join("\n", linhas)));

    public static ArquivoEnviado CsvExemplo(string nome, params string[] linhasDeDados) =>
        Csv(nome, [CabecalhoExemplo, .. linhasDeDados]);

    public static ArquivoEnviado Exemplo(string nome) =>
        ArquivoEnviado.De(nome, File.ReadAllBytes(Path.Combine(PastaExemplos, nome)));

    public static ContextoConversao Contexto()
    {
        var n = 0;
        return new ContextoConversao("teste.csv", new string('a', 64), "teste", Anonimizador(), Datas(), () => $"EV-{++n:00000}");
    }

    // ---------- eventos e regras montados à mão (testes de correlação) ----------

    public static EventoSeguranca Vulnerabilidade(string id, string ativo, string cve, string pacote = "pacote-x",
        Severidade severidade = Severidade.Critica, double cvss = 9.8) => new()
    {
        Id = id,
        Fonte = "teste",
        Tipo = TiposEvento.Vulnerabilidade,
        Ambiente = "teste",
        Ativo = ativo,
        Cve = cve,
        Pacote = pacote,
        Cvss = cvss,
        Severidade = severidade,
        Descricao = $"{cve} em {pacote}",
        Evidencia = new Evidencia("teste.csv", "sha", 1),
    };

    public static EventoSeguranca Alerta(string id, string ativo, string regra, DateTimeOffset quando, Severidade severidade = Severidade.Media) => new()
    {
        Id = id,
        Fonte = "teste",
        Tipo = TiposEvento.Alerta,
        Ambiente = "teste",
        Ativo = ativo,
        RegraOrigem = regra,
        DataUtc = quando,
        Severidade = severidade,
        Descricao = $"alerta da regra {regra}",
        Evidencia = new Evidencia("teste.csv", "sha", 1),
    };

    /// <summary>Regra de correlação com parâmetros escolhidos no teste. Os títulos repetem os campos para conferência.</summary>
    public static RegraCorrelacao Regra(string tipo, object parametros, string severidade = RegraCorrelacao.SeveridadeMaiorDosEventos) => new()
    {
        Id = "TESTE-" + tipo,
        Nome = tipo,
        Tipo = tipo,
        Titulo = "t",
        Justificativa = "j",
        Severidade = severidade,
        Parametros = JsonSerializer.SerializeToElement(parametros).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()),
    };
}

internal sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => agora;
}
