using Piersec.Core.Modelo;
using Piersec.Core.Padronizacao;
using Piersec.Core.Relatorio;

namespace Piersec.Core.Consulta;

/// <summary>
/// Filtros da visão técnica. Tudo opcional: filtro vazio = todos os eventos do lote.
/// É assim que um número da tela vira a lista dos eventos que o formam (RG2: número → achados → linha do CSV).
/// </summary>
public sealed record FiltroEventos
{
    public string? Fonte { get; init; }

    /// <summary>"Critica", "Alta", "Media", "Baixa" ou "nenhuma" (sem severidade).</summary>
    public string? Severidade { get; init; }

    public string? Tipo { get; init; }

    /// <summary>Pedaço do nome da máquina (sem diferença de maiúsculas e acentos).</summary>
    public string? Ativo { get; init; }

    public string? Servico { get; init; }
    public string? Ambiente { get; init; }
    public string? Arquivo { get; init; }

    /// <summary>Regra de origem do alerta (ex.: o id da regra do SIEM).</summary>
    public string? Regra { get; init; }

    /// <summary>Uma ou mais correlações separadas por vírgula (ex.: "COR-00001,COR-00004").</summary>
    public string? Correlacao { get; init; }

    /// <summary>Número da ação do plano: os eventos que ela elimina.</summary>
    public int? Acao { get; init; }

    /// <summary>Texto livre: procura na descrição, CVE, programa e nome da máquina.</summary>
    public string? Texto { get; init; }

    public const int TamanhoMaximoTexto = 100;
    public const int MaximoCorrelacoes = 50;

    public bool Vazio => this == new FiltroEventos();

    /// <summary>Corta textos longos demais (filtro vem da URL: não confiar no tamanho).</summary>
    public FiltroEventos Limpo() => this with
    {
        Fonte = Cortar(Fonte), Severidade = Cortar(Severidade), Tipo = Cortar(Tipo), Ativo = Cortar(Ativo), Servico = Cortar(Servico),
        Ambiente = Cortar(Ambiente), Arquivo = Cortar(Arquivo), Regra = Cortar(Regra), Texto = Cortar(Texto),
        Correlacao = string.IsNullOrWhiteSpace(Correlacao) ? null : Correlacao.Length > MaximoCorrelacoes * 12 ? Correlacao[..(MaximoCorrelacoes * 12)] : Correlacao.Trim(),
    };

    private static string? Cortar(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim() is var t && t.Length > TamanhoMaximoTexto ? t[..TamanhoMaximoTexto] : s.Trim();
}

public static class ConsultaEventos
{
    public const string SemSeveridade = "nenhuma";

    /// <summary>Eventos do lote que passam no filtro, dos mais graves para os menos graves.</summary>
    public static IReadOnlyList<EventoSeguranca> Filtrar(Lote lote, FiltroEventos filtro)
    {
        var f = filtro.Limpo();
        IEnumerable<EventoSeguranca> q = lote.Eventos;

        if (f.Correlacao is not null)
        {
            var ids = f.Correlacao.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Take(FiltroEventos.MaximoCorrelacoes)
                .ToHashSet(StringComparer.Ordinal);
            var eventos = lote.Correlacoes.Where(c => ids.Contains(c.Id)).SelectMany(c => c.EventoIds).ToHashSet(StringComparer.Ordinal);
            q = q.Where(e => eventos.Contains(e.Id));
        }
        if (f.Acao is { } ordem)
        {
            var eventos = (lote.Avaliacao?.Acoes.FirstOrDefault(a => a.Ordem == ordem)?.EventoIds ?? []).ToHashSet(StringComparer.Ordinal);
            q = q.Where(e => eventos.Contains(e.Id));
        }
        if (f.Fonte is not null) q = q.Where(e => e.Fonte == f.Fonte);
        if (f.Tipo is not null) q = q.Where(e => e.Tipo == f.Tipo);
        if (f.Servico is not null) q = q.Where(e => e.Servico == f.Servico);
        if (f.Ambiente is not null) q = q.Where(e => e.Ambiente == f.Ambiente);
        if (f.Arquivo is not null) q = q.Where(e => e.Evidencia.Arquivo == f.Arquivo);
        if (f.Regra is not null) q = q.Where(e => e.RegraOrigem == f.Regra);
        if (f.Severidade is not null)
        {
            if (f.Severidade == SemSeveridade)
                q = q.Where(e => e.Severidade is null);
            else if (Enum.TryParse<Severidade>(f.Severidade, out var s) && Enum.IsDefined(s))
                q = q.Where(e => e.Severidade == s);
            else
                q = [];                                              // severidade inválida na URL: nada, em vez de "tudo"
        }
        if (f.Ativo is not null)
        {
            var parte = Texto.NormalizarChave(f.Ativo);
            q = q.Where(e => Texto.NormalizarChave(e.Ativo).Contains(parte, StringComparison.Ordinal));
        }
        if (f.Texto is not null)
            q = q.Where(e => Contem(e.Descricao, f.Texto) || Contem(e.Cve, f.Texto) || Contem(e.Pacote, f.Texto) || Contem(e.Ativo, f.Texto));

        return Agrupamentos.MaisGraves(q).ToList();
    }

    /// <summary>O filtro em palavras, para o título da lista ("severidade Crítica · serviço Faturamento").</summary>
    public static string Descrever(Lote lote, FiltroEventos filtro)
    {
        var f = filtro.Limpo();
        var partes = new List<string>();
        if (f.Acao is { } a) partes.Add($"eliminados pela ação {a} do plano");
        if (f.Correlacao is not null) partes.Add($"da correlação {f.Correlacao.Replace(",", ", ")}");
        if (f.Severidade is not null)
            partes.Add(f.Severidade == SemSeveridade ? "sem severidade"
                : Enum.TryParse<Severidade>(f.Severidade, out var s) && Enum.IsDefined(s) ? $"severidade {s.Nome()}" : "severidade inválida");
        if (f.Tipo is not null) partes.Add($"tipo {f.Tipo}");
        if (f.Servico is not null) partes.Add($"serviço {lote.Servicos.FirstOrDefault(s => s.Id == f.Servico)?.Nome ?? f.Servico}");
        if (f.Fonte is not null) partes.Add($"fonte {f.Fonte}");
        if (f.Ambiente is not null) partes.Add($"ambiente {f.Ambiente}");
        if (f.Arquivo is not null) partes.Add($"arquivo {f.Arquivo}");
        if (f.Regra is not null) partes.Add($"regra {f.Regra}");
        if (f.Ativo is not null) partes.Add($"máquina contém \"{f.Ativo}\"");
        if (f.Texto is not null) partes.Add($"texto \"{f.Texto}\"");
        return partes.Count == 0 ? "todos os eventos" : string.Join(" · ", partes);
    }

    /// <summary>Correlações em que o evento aparece (para a página de detalhe).</summary>
    public static IReadOnlyList<Correlacao> CorrelacoesDo(Lote lote, string eventoId) =>
        lote.Correlacoes.Where(c => c.EventoIds.Contains(eventoId)).ToList();

    private static bool Contem(string? campo, string busca) =>
        campo is not null && campo.Contains(busca, StringComparison.OrdinalIgnoreCase);
}
