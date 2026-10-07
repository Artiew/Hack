using Piersec.Core.Coleta;
using Piersec.Core.Modelo;

namespace Piersec.Tests;

/// <summary>
/// Fonte FICTÍCIA e simples, usada só nos testes de base (leitura, lacunas, relatório).
/// As fontes reais do desafio têm adaptadores próprios no Piersec.Core.
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
            eventos.Add(new EventoSeguranca
            {
                Id = contexto.NovoId(),
                IdOrigem = tabela.Valor(linha, "id_evento"),
                Fonte = Nome,
                Tipo = tabela.Valor(linha, "tipo") ?? ContextoConversao.NaoInformado,
                Ambiente = contexto.Ambiente,
                Ativo = contexto.ConverterAtivo(tabela.Valor(linha, "ativo"), n),
                UsuarioAnonimo = contexto.ConverterUsuario(tabela.Valor(linha, "usuario"), n),
                SeveridadeOrigem = tabela.Valor(linha, "severidade"),
                DataUtc = contexto.ConverterData(tabela.Valor(linha, "data_hora"), n),
                Descricao = tabela.Valor(linha, "descricao") ?? "",
                Evidencia = contexto.Evidencia(n),
            });
        }
        return eventos;
    }
}
