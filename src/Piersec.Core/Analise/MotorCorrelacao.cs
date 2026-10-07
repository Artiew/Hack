using Piersec.Core.Coleta;
using Piersec.Core.Modelo;
using Piersec.Core.Relatorio;

namespace Piersec.Core.Analise;

/// <summary>
/// Aplica as regras de regras/correlacao.json aos eventos do lote (RF3).
/// Cada "tipo" de regra é um algoritmo pequeno aqui; limites e textos ficam no JSON.
/// Determinístico: mesma entrada → mesmas correlações, na mesma ordem, com os mesmos ids.
/// Regras sem resultado também são registradas, com o motivo.
/// </summary>
public sealed class MotorCorrelacao
{
    /// <summary>Tipos suportados e os campos que cada um oferece para título e justificativa.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> CamposPorTipo = new Dictionary<string, IReadOnlySet<string>>
    {
        ["mesma_vulnerabilidade_em_varios_ativos"] = new HashSet<string> { "cve", "cvss", "ativos" },
        ["pacote_concentra_vulnerabilidades"] = new HashSet<string> { "pacote", "cves", "ativos", "cvss" },
        ["ativo_acumula_vulnerabilidades"] = new HashSet<string> { "ativo", "quantidade" },
        ["rajada_de_alertas"] = new HashSet<string> { "ativo", "regra", "descricao", "quantidade", "janela" },
        ["alerta_e_vulnerabilidade_no_mesmo_ativo"] = new HashSet<string> { "ativo", "quantidade", "alertas" },
    };

    private sealed record Achado(string Chave, IReadOnlyDictionary<string, string> Valores, IReadOnlyList<EventoSeguranca> Eventos);

    public static void ValidarParametros(RegraCorrelacao regra)
    {
        switch (regra.Tipo)
        {
            case "mesma_vulnerabilidade_em_varios_ativos": regra.Numero("minimoAtivos"); regra.SeveridadeParametro("severidadeMinima"); break;
            case "pacote_concentra_vulnerabilidades": regra.Numero("minimoCves"); regra.SeveridadeParametro("severidadeMinima"); break;
            case "ativo_acumula_vulnerabilidades": regra.Numero("minimoFalhas"); regra.SeveridadeParametro("severidadeMinima"); break;
            case "rajada_de_alertas": regra.Numero("minimoAlertas"); regra.Numero("janelaMinutos"); break;
            case "alerta_e_vulnerabilidade_no_mesmo_ativo": regra.SeveridadeParametro("severidadeMinima"); break;
        }
    }

    public (IReadOnlyList<Correlacao> Correlacoes, IReadOnlyList<RegraAvaliada> Avaliadas) Correlacionar(
        IReadOnlyList<EventoSeguranca> eventos, IReadOnlyList<RegraCorrelacao> regras)
    {
        var correlacoes = new List<Correlacao>();
        var avaliadas = new List<RegraAvaliada>();

        foreach (var regra in regras)
        {
            var (achados, motivoVazio) = regra.Tipo switch
            {
                "mesma_vulnerabilidade_em_varios_ativos" => VulnerabilidadeEspalhada(eventos, regra),
                "pacote_concentra_vulnerabilidades" => PacoteConcentra(eventos, regra),
                "ativo_acumula_vulnerabilidades" => AtivoAcumula(eventos, regra),
                "rajada_de_alertas" => Rajada(eventos, regra),
                "alerta_e_vulnerabilidade_no_mesmo_ativo" => AlertaEFalhaNoMesmoAtivo(eventos, regra),
                _ => throw new InvalidOperationException($"Tipo de regra desconhecido: {regra.Tipo}"),
            };

            foreach (var achado in achados)
            {
                correlacoes.Add(new Correlacao
                {
                    Id = $"COR-{correlacoes.Count + 1:00000}",
                    RegraId = regra.Id,
                    Chave = achado.Chave,
                    Titulo = Preencher(regra.Titulo, achado.Valores),
                    Justificativa = Preencher(regra.Justificativa, achado.Valores),
                    Severidade = regra.Severidade == RegraCorrelacao.SeveridadeMaiorDosEventos
                        ? achado.Eventos.Max(e => e.Severidade) ?? Severidade.Baixa
                        : Enum.Parse<Severidade>(regra.Severidade),
                    Ativos = achado.Eventos.Select(e => e.Ativo).Distinct().Order(StringComparer.Ordinal).ToList(),
                    EventoIds = achado.Eventos.Select(e => e.Id).Order(StringComparer.Ordinal).ToList(),
                    Frameworks = regra.Frameworks,
                });
            }
            avaliadas.Add(new RegraAvaliada(regra.Id, regra.Nome, achados.Count, achados.Count == 0 ? motivoVazio : null));
        }
        return (correlacoes, avaliadas);
    }

    // ---------- tipos de regra ----------

    private static (List<Achado>, string) VulnerabilidadeEspalhada(IReadOnlyList<EventoSeguranca> eventos, RegraCorrelacao regra)
    {
        var minimo = (int)regra.Numero("minimoAtivos");
        var severidadeMinima = regra.SeveridadeParametro("severidadeMinima");
        var grupos = Vulnerabilidades(eventos, severidadeMinima).Where(e => e.Cve is not null)
            .GroupBy(e => e.Cve!, StringComparer.Ordinal)
            .Select(g => (Cve: g.Key, Eventos: g.ToList(), Ativos: g.Select(e => e.Ativo).Distinct().Count()))
            .ToList();

        var achados = grupos.Where(g => g.Ativos >= minimo)
            .OrderByDescending(g => g.Ativos).ThenBy(g => g.Cve, StringComparer.Ordinal)
            .Select(g => new Achado(g.Cve, new Dictionary<string, string>
            {
                ["cve"] = g.Cve,
                ["cvss"] = MaiorCvss(g.Eventos),
                ["ativos"] = Formatar.Numero(g.Ativos),
            }, g.Eventos)).ToList();

        var motivo = grupos.Count == 0
            ? $"nenhuma vulnerabilidade de severidade {severidadeMinima.Nome()} ou maior no lote"
            : $"nenhuma falha de severidade {severidadeMinima.Nome()} ou maior está em {minimo} máquinas ou mais (o máximo foi {grupos.Max(g => g.Ativos)})";
        return (achados, motivo);
    }

    private static (List<Achado>, string) PacoteConcentra(IReadOnlyList<EventoSeguranca> eventos, RegraCorrelacao regra)
    {
        var minimo = (int)regra.Numero("minimoCves");
        var severidadeMinima = regra.SeveridadeParametro("severidadeMinima");
        var grupos = Vulnerabilidades(eventos, severidadeMinima).Where(e => e.Pacote is not null && e.Cve is not null)
            .GroupBy(e => e.Pacote!.Trim().ToLowerInvariant(), StringComparer.Ordinal)
            .Select(g => (Chave: g.Key, Nome: g.First().Pacote!, Eventos: g.ToList(),
                          Cves: g.Select(e => e.Cve).Distinct().Count(), Ativos: g.Select(e => e.Ativo).Distinct().Count()))
            .ToList();

        var achados = grupos.Where(g => g.Cves >= minimo)
            .OrderByDescending(g => g.Cves).ThenByDescending(g => g.Ativos).ThenBy(g => g.Chave, StringComparer.Ordinal)
            .Select(g => new Achado(g.Chave, new Dictionary<string, string>
            {
                ["pacote"] = g.Nome,
                ["cves"] = Formatar.Numero(g.Cves),
                ["ativos"] = Formatar.Qtd(g.Ativos, "máquina", "máquinas"),
                ["cvss"] = MaiorCvss(g.Eventos),
            }, g.Eventos)).ToList();

        var motivo = grupos.Count == 0
            ? $"nenhuma vulnerabilidade de severidade {severidadeMinima.Nome()} ou maior com pacote informado"
            : $"nenhum software reúne {minimo} falhas diferentes ou mais (o máximo foi {grupos.Max(g => g.Cves)})";
        return (achados, motivo);
    }

    private static (List<Achado>, string) AtivoAcumula(IReadOnlyList<EventoSeguranca> eventos, RegraCorrelacao regra)
    {
        var minimo = (int)regra.Numero("minimoFalhas");
        var severidadeMinima = regra.SeveridadeParametro("severidadeMinima");
        var grupos = Vulnerabilidades(eventos, severidadeMinima)
            .GroupBy(e => e.Ativo, StringComparer.Ordinal)
            .Select(g => (Ativo: g.Key, Eventos: g.ToList(), Falhas: g.Select(e => e.Cve ?? e.Id).Distinct().Count()))
            .ToList();

        var achados = grupos.Where(g => g.Falhas >= minimo)
            .OrderByDescending(g => g.Falhas).ThenBy(g => g.Ativo, StringComparer.Ordinal)
            .Select(g => new Achado(g.Ativo, new Dictionary<string, string>
            {
                ["ativo"] = g.Ativo,
                ["quantidade"] = Formatar.Numero(g.Falhas),
            }, g.Eventos)).ToList();

        var motivo = grupos.Count == 0
            ? $"nenhuma vulnerabilidade de severidade {severidadeMinima.Nome()} ou maior no lote"
            : $"nenhuma máquina tem {minimo} falhas de severidade {severidadeMinima.Nome()} ou maior (o máximo foi {grupos.Max(g => g.Falhas)})";
        return (achados, motivo);
    }

    private static (List<Achado>, string) Rajada(IReadOnlyList<EventoSeguranca> eventos, RegraCorrelacao regra)
    {
        var minimo = (int)regra.Numero("minimoAlertas");
        var janelaMinutos = regra.Numero("janelaMinutos");
        var janela = TimeSpan.FromMinutes(janelaMinutos);

        var grupos = eventos
            .Where(e => e.Tipo == TiposEvento.Alerta && e.DataUtc is not null && e.RegraOrigem is not null && e.Ativo != ContextoConversao.NaoInformado)
            .GroupBy(e => (e.Ativo, Regra: e.RegraOrigem!))
            .Select(g => (g.Key.Ativo, g.Key.Regra, Janela: MaiorJanela(g.OrderBy(e => e.DataUtc).ThenBy(e => e.Id, StringComparer.Ordinal).ToList(), janela)))
            .ToList();

        var achados = grupos.Where(g => g.Janela.Count >= minimo)
            .OrderByDescending(g => g.Janela.Count).ThenBy(g => g.Ativo, StringComparer.Ordinal).ThenBy(g => g.Regra, StringComparer.Ordinal)
            .Select(g => new Achado($"{g.Ativo}|{g.Regra}", new Dictionary<string, string>
            {
                ["ativo"] = g.Ativo,
                ["regra"] = g.Regra,
                ["descricao"] = g.Janela[0].Descricao,
                ["quantidade"] = Formatar.Numero(g.Janela.Count),
                ["janela"] = janelaMinutos.ToString("0", Formatar.PtBr),
            }, g.Janela)).ToList();

        var motivo = grupos.Count == 0
            ? "nenhum alerta com data e regra no lote"
            : $"nenhuma regra passou de {minimo} alertas em {janelaMinutos:0} minutos no mesmo equipamento (o máximo foi {grupos.Max(g => g.Janela.Count)})";
        return (achados, motivo);
    }

    private static (List<Achado>, string) AlertaEFalhaNoMesmoAtivo(IReadOnlyList<EventoSeguranca> eventos, RegraCorrelacao regra)
    {
        var severidadeMinima = regra.SeveridadeParametro("severidadeMinima");
        var alertasPorAtivo = eventos.Where(e => e.Tipo == TiposEvento.Alerta && e.Ativo != ContextoConversao.NaoInformado)
            .GroupBy(e => e.Ativo, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var ativosComFalha = eventos.Where(e => e.Tipo == TiposEvento.Vulnerabilidade && e.Ativo != ContextoConversao.NaoInformado)
            .Select(e => e.Ativo).ToHashSet(StringComparer.Ordinal);

        var achados = Vulnerabilidades(eventos, severidadeMinima)
            .Where(e => alertasPorAtivo.ContainsKey(e.Ativo))
            .GroupBy(e => e.Ativo, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var alertas = alertasPorAtivo[g.Key];
                return new Achado(g.Key, new Dictionary<string, string>
                {
                    ["ativo"] = g.Key,
                    ["quantidade"] = Formatar.Qtd(g.Select(e => e.Cve ?? e.Id).Distinct().Count(), "falha grave", "falhas graves"),
                    ["alertas"] = Formatar.Qtd(alertas.Count, "alerta", "alertas"),
                }, [.. g, .. alertas]);
            }).ToList();

        var emComum = alertasPorAtivo.Keys.Count(ativosComFalha.Contains);
        var motivo = alertasPorAtivo.Count == 0 || ativosComFalha.Count == 0
            ? $"o lote não tem as duas fontes ({alertasPorAtivo.Count} equipamento(s) com alertas, {ativosComFalha.Count} máquina(s) com vulnerabilidades)"
            : emComum == 0
                ? $"nenhuma máquina aparece nas duas fontes: {alertasPorAtivo.Count} equipamento(s) nos alertas e {ativosComFalha.Count} máquina(s) nas vulnerabilidades, 0 em comum. Os dados não têm uma chave que ligue o alerta à máquina vulnerável"
                : $"as {emComum} máquina(s) em comum não têm falhas de severidade {severidadeMinima.Nome()} ou maior";
        return (achados, motivo);
    }

    // ---------- apoio ----------

    private static IEnumerable<EventoSeguranca> Vulnerabilidades(IReadOnlyList<EventoSeguranca> eventos, Severidade minima) =>
        eventos.Where(e => e.Tipo == TiposEvento.Vulnerabilidade && e.Severidade >= minima && e.Ativo != ContextoConversao.NaoInformado);

    /// <summary>Maior grupo de alertas cabendo numa janela de tempo (janela deslizante).</summary>
    private static List<EventoSeguranca> MaiorJanela(List<EventoSeguranca> ordenados, TimeSpan janela)
    {
        int melhorInicio = 0, melhorTamanho = 0, inicio = 0;
        for (var fim = 0; fim < ordenados.Count; fim++)
        {
            while (ordenados[fim].DataUtc!.Value - ordenados[inicio].DataUtc!.Value > janela)
                inicio++;
            if (fim - inicio + 1 > melhorTamanho)
                (melhorInicio, melhorTamanho) = (inicio, fim - inicio + 1);
        }
        return ordenados.GetRange(melhorInicio, melhorTamanho);
    }

    private static string MaiorCvss(IEnumerable<EventoSeguranca> eventos) =>
        eventos.Max(e => e.Cvss) is { } nota ? nota.ToString("0.0", Formatar.PtBr) : "não informada";

    private static string Preencher(string modelo, IReadOnlyDictionary<string, string> valores) =>
        ConjuntoRegras.Marcador().Replace(modelo, m => valores.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
}
