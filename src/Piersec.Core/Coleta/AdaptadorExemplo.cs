using Piersec.Core.Modelo;

namespace Piersec.Core.Coleta;

/// <summary>
/// Fonte FICTÍCIA, criada por nós só para provar o esqueleto (Fase 1).
/// Será acompanhada pelos adaptadores das fontes reais do desafio quando tivermos os cabeçalhos.
/// </summary>
public sealed class AdaptadorExemplo : IAdaptadorFonte
{
    private static readonly string[] Colunas =
        ["id_evento", "data_hora", "ativo", "usuario", "severidade", "tipo", "descricao"];

    public string Nome => "Exemplo fictício";

    public bool Reconhece(IReadOnlyList<string> cabecalho) => Colunas.All(cabecalho.Contains);

    public IReadOnlyList<EventoSeguranca> Converter(TabelaCsv tabela, ContextoConversao contexto)
    {
        var eventos = new List<EventoSeguranca>(tabela.Linhas.Count);
        foreach (var linha in tabela.Linhas)
        {
            var n = linha.Numero;
            var severidadeOrigem = tabela.Valor(linha, "severidade");
            eventos.Add(new EventoSeguranca
            {
                Id = contexto.NovoId(),
                IdOrigem = tabela.Valor(linha, "id_evento"),
                Fonte = Nome,
                Tipo = tabela.Valor(linha, "tipo") ?? ContextoConversao.NaoInformado,
                Ativo = contexto.ConverterAtivo(tabela.Valor(linha, "ativo"), n),
                UsuarioAnonimo = contexto.ConverterUsuario(tabela.Valor(linha, "usuario"), n),
                SeveridadeOrigem = severidadeOrigem,
                Severidade = contexto.ConverterSeveridade(severidadeOrigem, n),
                DataUtc = contexto.ConverterData(tabela.Valor(linha, "data_hora"), n),
                Descricao = tabela.Valor(linha, "descricao") ?? "",
                Evidencia = contexto.Evidencia(n),
            });
        }
        return eventos;
    }
}
