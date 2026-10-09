using Piersec.Core.Modelo;

namespace Piersec.Core.Coleta;

/// <summary>Inventário exportado: não faz varredura, consulta externa ou conexão com os ativos.</summary>
public sealed class AdaptadorExposicaoInternet : IAdaptadorFonte
{
    public string Nome => "Inventário de exposição à internet";
    public string TipoDeEvento => TiposEvento.Exposicao;
    public IReadOnlyList<string> LimitacoesDaFonte =>
    [
        "o inventário informa exposição declarada; não comprova exploração, necessidade do acesso nem cobertura de toda a empresa",
        "registros de datas diferentes não comprovam simultaneidade; confirme o período e o ambiente das fontes",
    ];
    public bool Reconhece(IReadOnlyList<string> cabecalho) =>
        new[] { "asset.name", "internet.exposed", "observed_at" }.All(cabecalho.Contains);

    public IReadOnlyList<EventoSeguranca> Converter(TabelaCsv tabela, ContextoConversao contexto)
    {
        var eventos = new List<EventoSeguranca>();
        foreach (var linha in tabela.Linhas)
        {
            var valor = tabela.Valor(linha, "internet.exposed")?.Trim().ToLowerInvariant();
            bool? exposto = valor switch
            {
                "true" or "1" or "sim" => true,
                "false" or "0" or "nao" or "não" => false,
                _ => null,
            };
            if (exposto is null)
                contexto.RegistrarLacuna(linha.Numero, "internet.exposed", "exposição ausente ou inválida: use true/false, sim/não ou 1/0; não foi tratada como acesso fechado");
            eventos.Add(new EventoSeguranca
            {
                Id = contexto.NovoId(), Fonte = Nome, Tipo = TiposEvento.Exposicao,
                Ambiente = contexto.Ambiente,
                AmbienteDeclarado = contexto.ConverterIdentificadorDeclarado(tabela.Valor(linha, "environment.id"), linha.Numero, "environment.id"),
                Ativo = contexto.ConverterAtivo(tabela.Valor(linha, "asset.name"), linha.Numero),
                SistemaOperacional = tabela.Valor(linha, "asset.os"),
                ExpostoInternet = exposto,
                DataUtc = contexto.ConverterData(tabela.Valor(linha, "observed_at"), linha.Numero),
                Descricao = exposto switch
                {
                    true => "Inventário informa máquina acessível pela internet",
                    false => "Inventário informa máquina sem acesso direto pela internet",
                    _ => "Inventário sem informação válida sobre exposição à internet",
                },
                Evidencia = contexto.Evidencia(linha.Numero),
            });
        }
        foreach (var g in eventos.GroupBy(e => e.Ativo).Where(g => g.Select(e => e.ExpostoInternet).Distinct().Count() > 1))
            contexto.RegistrarLacuna(g.First().Evidencia.Linha, "internet.exposed", "há estados de exposição divergentes para o mesmo ativo; o fator não será calculado até a conferência");
        return eventos;
    }
}
