using Piersec.Core.Analise;
using Piersec.Core.Coleta;
using Piersec.Core.Modelo;
using Piersec.Core.Relatorio;

namespace Piersec.Core.Operacao;

/// <summary>
/// Um problema encontrado no lote que dispara um playbook. A <see cref="Chave"/> é estável entre lotes
/// (ex.: "programa:openssl"): é ela que garante que o mesmo problema nunca gera dois chamados.
/// </summary>
public sealed record Ocorrencia
{
    public required string Gatilho { get; init; }
    public required string Chave { get; init; }
    public required IReadOnlyDictionary<string, string> Valores { get; init; }
    public required IReadOnlyList<string> Ativos { get; init; }

    /// <summary>Tipos das máquinas envolvidas (ex.: "servidor"), para a trava de ativo crítico.</summary>
    public required IReadOnlySet<string> TiposAtivo { get; init; }

    public required IReadOnlyList<string> EventoIds { get; init; }
    public IReadOnlyList<string> CorrelacaoIds { get; init; } = [];

    /// <summary>"alta", "média" ou "baixa", com o porquê (vai para o log de decisão).</summary>
    public required string Confianca { get; init; }
    public required string PorQueConfianca { get; init; }
}

/// <summary>
/// Encontra, no lote já analisado, os problemas que disparam playbooks. Devolve TODAS as ocorrências de cada gatilho,
/// das mais graves para as menos graves: a lista completa serve para saber se um problema ainda existe
/// (fechar ou manter o chamado); o começo dela, para abrir chamados novos.
/// </summary>
public sealed class Gatilhos(ConjuntoRegras regras)
{
    public const string ProgramaComFalhasGraves = "programa_com_falhas_graves";
    public const string MaquinaComFalhasGraves = "maquina_com_falhas_graves";
    public const string MaquinaAcumulaFalhas = "maquina_acumula_falhas";
    public const string PicoDeAlertas = "pico_de_alertas";
    public const string FatorNaoMedido = "fator_nao_medido";

    /// <summary>Campos que cada gatilho preenche nos textos do playbook.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Campos = new Dictionary<string, IReadOnlySet<string>>
    {
        [ProgramaComFalhasGraves] = new HashSet<string> { "pacote", "falhas", "maquinas" },
        [MaquinaComFalhasGraves] = new HashSet<string> { "ativo", "tipo", "falhas", "programas" },
        [MaquinaAcumulaFalhas] = new HashSet<string> { "ativo", "tipo", "quantidade" },
        [PicoDeAlertas] = new HashSet<string> { "ativo", "regra", "quantidade", "janela" },
        [FatorNaoMedido] = new HashSet<string> { "fator", "pergunta" },
    };

    /// <summary>
    /// Que tipo de dado o lote precisa trazer para dizer que o problema SUMIU. Sem esse dado, o chamado fica como está:
    /// fonte ausente não é problema resolvido. Nulo = o gatilho sempre pode ser conferido.
    /// </summary>
    public static string? TipoNecessario(string gatilho) => gatilho switch
    {
        ProgramaComFalhasGraves or MaquinaComFalhasGraves or MaquinaAcumulaFalhas => TiposEvento.Vulnerabilidade,
        PicoDeAlertas => TiposEvento.Alerta,
        _ => null,
    };

    private const string ConfiancaScanner = "a falha foi informada pela ferramenta de vulnerabilidades, com nota oficial (CVSS)";
    private const string ConfiancaAlertas = "alerta pode ser falso positivo, e os dados não mostram se alguma tentativa deu certo";

    private readonly CalculadoraScore _calculadora = new(regras.Score, regras.Ativos);

    public IReadOnlyDictionary<string, IReadOnlyList<Ocorrencia>> Detectar(Lote lote)
    {
        var graves = lote.Eventos
            .Where(e => e.Tipo == TiposEvento.Vulnerabilidade && e.Severidade >= Severidade.Alta && e.Ativo != ContextoConversao.NaoInformado)
            .ToList();
        var porId = lote.Eventos.ToDictionary(e => e.Id, StringComparer.Ordinal);

        return new Dictionary<string, IReadOnlyList<Ocorrencia>>
        {
            [ProgramaComFalhasGraves] = Programas(graves),
            [MaquinaComFalhasGraves] = Maquinas(graves),
            [MaquinaAcumulaFalhas] = Acumulam(lote, porId),
            [PicoDeAlertas] = Picos(lote, porId),
            [FatorNaoMedido] = Fatores(lote),
        };
    }

    private static List<Ocorrencia> Programas(List<EventoSeguranca> graves) =>
        graves.Where(e => !string.IsNullOrWhiteSpace(e.Pacote))
            .GroupBy(e => e.Pacote!.Trim().ToLowerInvariant(), StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal) // mesma ordem do plano de ação
            .Select(g =>
            {
                var ativos = g.Select(e => e.Ativo).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
                return new Ocorrencia
                {
                    Gatilho = ProgramaComFalhasGraves,
                    Chave = "programa:" + g.Key,
                    Valores = new Dictionary<string, string>
                    {
                        ["pacote"] = g.First().Pacote!.Trim(),
                        ["falhas"] = Formatar.Qtd(g.Count(), "falha grave", "falhas graves"),
                        ["maquinas"] = Formatar.Qtd(ativos.Count, "máquina", "máquinas"),
                    },
                    Ativos = ativos,
                    TiposAtivo = Tipos(g),
                    EventoIds = Ids(g),
                    Confianca = "alta",
                    PorQueConfianca = ConfiancaScanner,
                };
            }).ToList();

    private List<Ocorrencia> Maquinas(List<EventoSeguranca> graves) =>
        graves.GroupBy(e => e.Ativo, StringComparer.Ordinal)
            .Select(g => (Grupo: g, Pontos: _calculadora.PontosDaMaquina(g) * regras.Ativos.Tipo(g.First().TipoAtivo).PesoNoScore))
            .OrderByDescending(m => m.Pontos).ThenByDescending(m => m.Grupo.Count()).ThenBy(m => m.Grupo.Key, StringComparer.Ordinal)
            .Select(m =>
            {
                var g = m.Grupo;
                var programas = g.Where(e => !string.IsNullOrWhiteSpace(e.Pacote))
                    .GroupBy(e => e.Pacote!.Trim().ToLowerInvariant(), StringComparer.Ordinal)
                    .OrderByDescending(p => p.Max(e => e.Severidade)).ThenByDescending(p => p.Count()).ThenBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => p.First().Pacote!.Trim()).ToList();
                var textoProgramas = programas.Count == 0 ? "(programas não informados)"
                    : programas.Count <= 5 ? Lista(programas)
                    : Lista([.. programas.Take(5), $"mais {programas.Count - 5}"]);
                return new Ocorrencia
                {
                    Gatilho = MaquinaComFalhasGraves,
                    Chave = "maquina:" + g.Key,
                    Valores = new Dictionary<string, string>
                    {
                        ["ativo"] = g.Key,
                        ["tipo"] = regras.Ativos.Tipo(g.First().TipoAtivo).Singular,
                        ["falhas"] = Formatar.Qtd(g.Count(), "falha grave", "falhas graves"),
                        ["programas"] = textoProgramas,
                    },
                    Ativos = [g.Key],
                    TiposAtivo = Tipos(g),
                    EventoIds = Ids(g),
                    Confianca = "alta",
                    PorQueConfianca = ConfiancaScanner,
                };
            }).ToList();

    private List<Ocorrencia> Acumulam(Lote lote, Dictionary<string, EventoSeguranca> porId) =>
        CorrelacoesDoTipo(lote, "ativo_acumula_vulnerabilidades")
            .Where(c => c.Ativos.Count > 0)
            .OrderByDescending(c => c.EventoIds.Count).ThenBy(c => c.Ativos[0], StringComparer.Ordinal)
            .Select(c =>
            {
                var eventos = c.EventoIds.Where(porId.ContainsKey).Select(id => porId[id]).ToList();
                return new Ocorrencia
                {
                    Gatilho = MaquinaAcumulaFalhas,
                    Chave = "maquina:" + c.Ativos[0],
                    Valores = new Dictionary<string, string>
                    {
                        ["ativo"] = c.Ativos[0],
                        ["tipo"] = regras.Ativos.Tipo(eventos.FirstOrDefault()?.TipoAtivo).Singular,
                        ["quantidade"] = Formatar.Numero(c.EventoIds.Count),
                    },
                    Ativos = [c.Ativos[0]],
                    TiposAtivo = Tipos(eventos),
                    EventoIds = Ids(eventos),
                    CorrelacaoIds = [c.Id],
                    Confianca = "alta",
                    PorQueConfianca = ConfiancaScanner,
                };
            }).ToList();

    private List<Ocorrencia> Picos(Lote lote, Dictionary<string, EventoSeguranca> porId) =>
        CorrelacoesDoTipo(lote, "rajada_de_alertas")
            .Where(c => c.Ativos.Count > 0)
            .Select(c => (Correlacao: c, Eventos: c.EventoIds.Where(porId.ContainsKey).Select(id => porId[id]).ToList()))
            .Where(p => p.Eventos.Count > 0)
            .OrderByDescending(p => p.Eventos.Count).ThenBy(p => p.Correlacao.Chave, StringComparer.Ordinal)
            .Select(p =>
            {
                var regraCorrelacao = regras.Correlacoes.First(r => r.Id == p.Correlacao.RegraId);
                var regraOrigem = p.Eventos[0].RegraOrigem ?? "?";
                return new Ocorrencia
                {
                    Gatilho = PicoDeAlertas,
                    Chave = $"pico:{p.Correlacao.Ativos[0]}|{regraOrigem}",
                    Valores = new Dictionary<string, string>
                    {
                        ["ativo"] = p.Correlacao.Ativos[0],
                        ["regra"] = regraOrigem,
                        ["quantidade"] = Formatar.Numero(p.Eventos.Count),
                        ["janela"] = regraCorrelacao.Numero("janelaMinutos").ToString("0", Formatar.PtBr),
                    },
                    Ativos = [p.Correlacao.Ativos[0]],
                    TiposAtivo = Tipos(p.Eventos),
                    EventoIds = Ids(p.Eventos),
                    CorrelacaoIds = [p.Correlacao.Id],
                    Confianca = "média",
                    PorQueConfianca = ConfiancaAlertas,
                };
            }).ToList();

    private static List<Ocorrencia> Fatores(Lote lote) =>
        (lote.Avaliacao?.Score.Fatores ?? [])
            .Where(f => f.Valor is null)
            .OrderByDescending(f => f.Peso).ThenBy(f => f.Id, StringComparer.Ordinal)
            .Select(f => new Ocorrencia
            {
                Gatilho = FatorNaoMedido,
                Chave = "fator:" + f.Id,
                Valores = new Dictionary<string, string> { ["fator"] = f.Nome, ["pergunta"] = f.Pergunta },
                Ativos = [],
                TiposAtivo = new HashSet<string>(),
                EventoIds = [],
                Confianca = "alta",
                PorQueConfianca = "nenhum dado deste fator chegou no lote",
            }).ToList();

    private IEnumerable<Correlacao> CorrelacoesDoTipo(Lote lote, string tipo)
    {
        var ids = regras.Correlacoes.Where(r => r.Tipo == tipo).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        return lote.Correlacoes.Where(c => ids.Contains(c.RegraId));
    }

    private static HashSet<string> Tipos(IEnumerable<EventoSeguranca> eventos) =>
        eventos.Select(e => e.TipoAtivo ?? RegrasAtivos.Desconhecido).ToHashSet(StringComparer.Ordinal);

    private static List<string> Ids(IEnumerable<EventoSeguranca> eventos) =>
        eventos.Select(e => e.Id).Order(StringComparer.Ordinal).ToList();

    private static string Lista(IReadOnlyList<string> itens) =>
        itens.Count == 1 ? itens[0] : string.Join(", ", itens.Take(itens.Count - 1)) + " e " + itens[^1];
}
