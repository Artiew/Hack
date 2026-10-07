using System.Text.RegularExpressions;
using Piersec.Core.Modelo;

namespace Piersec.Core.Coleta;

/// <summary>
/// Alertas exportados do SIEM: timestamp, agent.name, rule.description, data.devname, rule.level, rule.id.
/// Defeitos conhecidos da exportação, tratados aqui:
/// 1) cada linha vem inteira entre aspas (o <see cref="LeitorCsv"/> desembrulha);
/// 2) a data tem vírgula sem aspas ("Oct 7,2026 @ 08:49:03.123"), o que cria uma coluna a mais.
/// </summary>
public sealed partial class AdaptadorAlertasSiem : IAdaptadorFonte
{
    public const int NivelMinimo = 0;
    public const int NivelMaximo = 15;

    private static readonly string[] Obrigatorias = ["timestamp", "agent.name", "rule.description", "rule.level", "rule.id"];

    public string Nome => "Alertas do SIEM";

    public string TipoDeEvento => TiposEvento.Alerta;

    public IReadOnlyList<string> LimitacoesDaFonte =>
    [
        "a fonte não informa usuário",
        "a fonte não informa qual máquina interna foi alvo de cada alerta (só o equipamento que gerou o alerta)",
    ];

    public string DescricaoReparo =>
        "a data vinha com uma vírgula sem aspas (ex.: \"Oct 7,2026 @ 08:49\"), o que criava uma coluna a mais; as duas partes da data foram juntadas";

    public bool Reconhece(IReadOnlyList<string> cabecalho) => Obrigatorias.All(cabecalho.Contains);

    /// <summary>Junta as duas partes da data quando ela foi partida pela vírgula. Qualquer outro desalinho não é consertado.</summary>
    public string[]? RepararLinha(IReadOnlyList<string> cabecalho, string[] campos)
    {
        var i = IndexOf(cabecalho, "timestamp");
        if (i < 0 || campos.Length != cabecalho.Count + 1 || i + 1 >= campos.Length)
            return null;
        if (!InicioData().IsMatch(campos[i]) || !FimData().IsMatch(campos[i + 1]))
            return null;

        return [.. campos[..i], campos[i] + "," + campos[i + 1], .. campos[(i + 2)..]];
    }

    public IReadOnlyList<EventoSeguranca> Converter(TabelaCsv tabela, ContextoConversao contexto)
    {
        var eventos = new List<EventoSeguranca>(tabela.Linhas.Count);
        foreach (var linha in tabela.Linhas)
        {
            var n = linha.Numero;
            var coletor = tabela.Valor(linha, "agent.name");
            var equipamento = tabela.Valor(linha, "data.devname");
            var regra = tabela.Valor(linha, "rule.id");
            if (regra is null)
                contexto.RegistrarLacuna(n, "rule.id", "alerta sem id de regra");
            var nivel = contexto.ConverterInteiro(tabela.Valor(linha, "rule.level"), n, "rule.level", NivelMinimo, NivelMaximo);

            eventos.Add(new EventoSeguranca
            {
                Id = contexto.NovoId(),
                IdOrigem = regra,
                Fonte = Nome,
                Tipo = TiposEvento.Alerta,
                Ambiente = contexto.Ambiente,
                // O ativo é o equipamento que gerou o alerta (ex.: o firewall); sem ele, o agente coletor.
                Ativo = contexto.ConverterAtivo(equipamento ?? coletor, n),
                SeveridadeOrigem = nivel is null ? null : $"nível {nivel}",
                NivelOrigem = nivel,
                RegraOrigem = regra,
                DataUtc = contexto.ConverterData(tabela.Valor(linha, "timestamp"), n),
                Descricao = tabela.Valor(linha, "rule.description") ?? "(alerta sem descrição)",
                Detalhe = coletor is null ? null : $"coletado pelo agente {coletor}",
                Evidencia = contexto.Evidencia(n),
            });
        }
        return eventos;
    }

    private static int IndexOf(IReadOnlyList<string> lista, string valor)
    {
        for (var i = 0; i < lista.Count; i++)
            if (lista[i] == valor)
                return i;
        return -1;
    }

    // "Oct 7" + "2026 @ 08:49:03.123"
    [GeneratedRegex(@"^[A-Za-z]{3} \d{1,2}$")]
    private static partial Regex InicioData();

    [GeneratedRegex(@"^\d{4} @ \d{2}:\d{2}(:\d{2}(\.\d+)?)?$")]
    private static partial Regex FimData();
}
