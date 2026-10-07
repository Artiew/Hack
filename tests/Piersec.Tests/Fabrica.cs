using System.Text;
using Piersec.Core.Coleta;
using Piersec.Core.Padronizacao;
using Piersec.Core.Pipeline;

namespace Piersec.Tests;

/// <summary>Monta as peças da plataforma para os testes, sempre com dados fictícios.</summary>
internal static class Fabrica
{
    public static readonly byte[] ChaveFixa = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    public static readonly DateTimeOffset Agora = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    public const string CabecalhoExemplo = "id_evento,data_hora,ativo,usuario,severidade,tipo,descricao";

    public static Anonimizador Anonimizador() => new(ChaveFixa);
    public static NormalizadorDatas Datas() => new(NormalizadorDatas.FusoBrasilia);

    public static ProcessadorLote Processador() =>
        new([new AdaptadorExemplo()], Anonimizador(), Datas(), new RelogioFixo(Agora));

    public static ArquivoEnviado Csv(string nome, params string[] linhas) =>
        ArquivoEnviado.De(nome, Encoding.UTF8.GetBytes(string.Join("\n", linhas)));

    public static ArquivoEnviado CsvExemplo(string nome, params string[] linhasDeDados) =>
        Csv(nome, [CabecalhoExemplo, .. linhasDeDados]);

    public static ContextoConversao Contexto()
    {
        var n = 0;
        return new ContextoConversao("teste.csv", new string('a', 64), Anonimizador(), Datas(), () => $"EV-{++n:00000}");
    }
}

internal sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => agora;
}
