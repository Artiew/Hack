using Piersec.Core.Modelo;

namespace Piersec.Core.Coleta;

/// <summary>
/// Fonte de vulnerabilidades (scanner), com colunas no padrão ECS:
/// agent.name, host.os.name, package.name, package.version, vulnerability.id,
/// vulnerability.description, vulnerability.severity, vulnerability.score.base, vulnerability.reference.
/// </summary>
public sealed class AdaptadorVulnerabilidades : IAdaptadorFonte
{
    private static readonly string[] Obrigatorias =
    [
        "agent.name", "package.name", "package.version", "vulnerability.id",
        "vulnerability.severity", "vulnerability.score.base",
    ];

    public string Nome => "Vulnerabilidades (scanner)";

    public IReadOnlyList<string> LimitacoesDaFonte =>
    [
        "a fonte não informa quando a falha foi detectada (sem data)",
        "a fonte não informa se a máquina está exposta à internet nem se a falha está sendo explorada (KEV/EPSS)",
    ];

    public bool Reconhece(IReadOnlyList<string> cabecalho) => Obrigatorias.All(cabecalho.Contains);

    public IReadOnlyList<EventoSeguranca> Converter(TabelaCsv tabela, ContextoConversao contexto)
    {
        var eventos = new List<EventoSeguranca>(tabela.Linhas.Count);
        foreach (var linha in tabela.Linhas)
        {
            var n = linha.Numero;
            var cve = tabela.Valor(linha, "vulnerability.id")?.ToUpperInvariant();
            if (cve is null)
                contexto.RegistrarLacuna(n, "vulnerability.id", "falha sem identificador (CVE)");
            var pacote = tabela.Valor(linha, "package.name");
            if (pacote is null)
                contexto.RegistrarLacuna(n, "package.name", "pacote não informado");
            var versao = tabela.Valor(linha, "package.version");

            eventos.Add(new EventoSeguranca
            {
                Id = contexto.NovoId(),
                IdOrigem = cve,
                Fonte = Nome,
                Tipo = TiposEvento.Vulnerabilidade,
                Ambiente = contexto.Ambiente,
                Ativo = contexto.ConverterAtivo(tabela.Valor(linha, "agent.name"), n),
                SeveridadeOrigem = tabela.Valor(linha, "vulnerability.severity"),
                Cvss = contexto.ConverterCvss(tabela.Valor(linha, "vulnerability.score.base"), n),
                Cve = cve,
                Pacote = pacote,
                VersaoPacote = versao,
                SistemaOperacional = tabela.Valor(linha, "host.os.name"),
                Descricao = $"{cve ?? "Falha sem identificador"} no pacote {pacote ?? ContextoConversao.NaoInformado} {versao}".TrimEnd(),
                Detalhe = tabela.Valor(linha, "vulnerability.description"),
                Referencia = tabela.Valor(linha, "vulnerability.reference"),
                Evidencia = contexto.Evidencia(n),
            });
        }
        return eventos;
    }
}
