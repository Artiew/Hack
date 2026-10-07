using Piersec.Core.Modelo;

namespace Piersec.Core.Coleta;

/// <summary>
/// Contrato de toda fonte de dados. Cada fonte (antivírus, e-mail, scanner...) tem o seu adaptador;
/// trocar ou adicionar uma fonte é escrever um novo adaptador, sem mexer no resto da plataforma.
/// </summary>
public interface IAdaptadorFonte
{
    /// <summary>Nome da fonte exibido nas telas e no relatório.</summary>
    string Nome { get; }

    /// <summary>
    /// Tipo de evento que a fonte produz (ver <see cref="TiposEvento"/>). Serve para o score saber o que foi medido:
    /// arquivo reconhecido com zero linhas = "medido, nada encontrado", diferente de "sem dados".
    /// </summary>
    string? TipoDeEvento => null;

    /// <summary>
    /// O que esta fonte nunca informa (ex.: "não informa data"). Vira uma lacuna por arquivo,
    /// em vez de uma lacuna repetida em cada linha.
    /// </summary>
    IReadOnlyList<string> LimitacoesDaFonte => [];

    /// <summary>Texto que explica o reparo feito por <see cref="RepararLinha"/>, para o relatório.</summary>
    string? DescricaoReparo => null;

    /// <summary>Este adaptador sabe ler um arquivo com este cabeçalho (nomes já normalizados)?</summary>
    bool Reconhece(IReadOnlyList<string> cabecalho);

    /// <summary>
    /// Tenta consertar uma linha com número de colunas diferente do cabeçalho (defeito conhecido da fonte).
    /// Devolve null quando não sabe consertar: a linha vira lacuna, nunca é lida desalinhada.
    /// </summary>
    string[]? RepararLinha(IReadOnlyList<string> cabecalho, string[] campos) => null;

    /// <summary>Converte as linhas para o modelo comum. Problemas viram lacunas no contexto, nunca somem.</summary>
    IReadOnlyList<EventoSeguranca> Converter(TabelaCsv tabela, ContextoConversao contexto);
}
