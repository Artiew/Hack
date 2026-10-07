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

    /// <summary>Este adaptador sabe ler um arquivo com este cabeçalho (nomes já normalizados)?</summary>
    bool Reconhece(IReadOnlyList<string> cabecalho);

    /// <summary>Converte as linhas para o modelo comum. Problemas viram lacunas no contexto, nunca somem.</summary>
    IReadOnlyList<EventoSeguranca> Converter(TabelaCsv tabela, ContextoConversao contexto);
}
