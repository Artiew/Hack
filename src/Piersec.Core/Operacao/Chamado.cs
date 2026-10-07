using Piersec.Core.Modelo;

namespace Piersec.Core.Operacao;

/// <summary>Em que pé está um chamado.</summary>
public enum EstadoChamado
{
    /// <summary>Só uma sugestão: nada foi feito.</summary>
    Sugerido,
    /// <summary>Preparado, esperando uma pessoa aprovar ou rejeitar.</summary>
    AguardandoAprovacao,
    /// <summary>Feito (de forma simulada): sozinho ou com aprovação.</summary>
    Aberto,
    /// <summary>Uma pessoa disse não. A plataforma não insiste sozinha.</summary>
    Rejeitado,
    /// <summary>O problema sumiu em um lote seguinte: fechou sozinho. Se voltar, reabre.</summary>
    Resolvido,
    /// <summary>Uma pessoa desfez a ação. A plataforma não refaz sozinha.</summary>
    Desfeito,
}

public static class EstadosChamado
{
    /// <summary>Estados em que o chamado ainda está "em andamento".</summary>
    public static bool Ativo(this EstadoChamado e) => e is EstadoChamado.Sugerido or EstadoChamado.AguardandoAprovacao or EstadoChamado.Aberto;

    public static string Nome(this EstadoChamado e) => e switch
    {
        EstadoChamado.Sugerido => "sugestão",
        EstadoChamado.AguardandoAprovacao => "aguardando aprovação",
        EstadoChamado.Aberto => "aberto",
        EstadoChamado.Rejeitado => "rejeitado",
        EstadoChamado.Resolvido => "resolvido (fechou sozinho)",
        EstadoChamado.Desfeito => "desfeito",
        _ => e.ToString(),
    };
}

/// <summary>Uma linha do histórico do chamado: o log de decisão (quem, o quê, por quê).</summary>
public sealed record RegistroChamado(DateTimeOffset QuandoUtc, string Quem, string Evento, string Detalhe, string? LoteId = null);

/// <summary>
/// Um chamado (ticket) SIMULADO. Existe no máximo um por <see cref="Chave"/>: o mesmo problema, em vários lotes,
/// continua sendo o mesmo chamado (idempotência).
/// </summary>
public sealed record Chamado
{
    /// <summary>Plataforma (não uma pessoa) nos registros de histórico.</summary>
    public const string Plataforma = "plataforma";

    public required string Numero { get; init; }

    /// <summary>Playbook + problema (ex.: "PB-ATUALIZAR-PROGRAMA|programa:openssl").</summary>
    public required string Chave { get; init; }

    public required string PlaybookId { get; init; }
    public required string Acao { get; init; }
    public required string Titulo { get; init; }
    public required string Motivo { get; init; }
    public required IReadOnlyList<string> Passos { get; init; }
    public required string ComoDesfazer { get; init; }
    public required IReadOnlyList<string> Ativos { get; init; }
    public required IReadOnlyList<string> TiposAtivo { get; init; }
    public required EstadoChamado Estado { get; init; }

    /// <summary>Por que a plataforma decidiu agir sozinha, pedir aprovação ou só sugerir.</summary>
    public required string MotivoDecisao { get; init; }

    /// <summary>A última vez que foi aberto, foi a plataforma sozinha (true) ou uma pessoa aprovou (false)?</summary>
    public bool AgiuSozinho { get; init; }

    public required string Confianca { get; init; }
    public required string PorQueConfianca { get; init; }

    public required int EvidenciasTotal { get; init; }

    /// <summary>Até <see cref="AmostraEvidencias"/> ids de eventos do último lote em que o problema apareceu.</summary>
    public required IReadOnlyList<string> EvidenciaIds { get; init; }
    public IReadOnlyList<string> CorrelacaoIds { get; init; } = [];

    public required string LoteCriacao { get; init; }
    public required string LoteUltimaVez { get; init; }
    public required DateTimeOffset CriadoEmUtc { get; init; }
    public required DateTimeOffset AtualizadoEmUtc { get; init; }
    public Frameworks Frameworks { get; init; } = Frameworks.Vazio;
    public required IReadOnlyList<RegistroChamado> Historico { get; init; }

    public const int AmostraEvidencias = 20;
}

/// <summary>Tudo o que a operação guarda entre um lote e outro (cifrado em var/operacao/).</summary>
public sealed record EstadoOperacao
{
    public IReadOnlyList<Chamado> Chamados { get; init; } = [];
    public int ProximoNumero { get; init; } = 1;
    public ResultadoOperacao? UltimoResultado { get; init; }

    public static EstadoOperacao Vazio { get; } = new();
}

/// <summary>O que aconteceu com um chamado (ou com um candidato a chamado) ao processar um lote.</summary>
public enum ResultadoItem
{
    /// <summary>Feito sozinho (simulado), dentro das travas.</summary>
    AgiuSozinho,
    AguardaAprovacao,
    Sugerido,
    /// <summary>O chamado já existia para este problema: não foi duplicado.</summary>
    JaExistia,
    /// <summary>O problema sumiu: o chamado fechou sozinho.</summary>
    FechouSozinho,
    /// <summary>O problema voltou: o chamado reabriu.</summary>
    Reaberto,
    /// <summary>O lote não traz o tipo de dado necessário para conferir: o chamado ficou como estava.</summary>
    MantidoSemConferir,
    /// <summary>O problema continua, mas uma pessoa rejeitou ou desfez: a plataforma não insiste.</summary>
    DecisaoHumanaMantida,
}

public static class ResultadosItem
{
    public static string Nome(this ResultadoItem r, bool ensaio) => (r, ensaio) switch
    {
        (ResultadoItem.AgiuSozinho, false) => "feito sozinho",
        (ResultadoItem.AgiuSozinho, true) => "faria sozinho",
        (ResultadoItem.AguardaAprovacao, false) => "aguardando aprovação",
        (ResultadoItem.AguardaAprovacao, true) => "pediria aprovação",
        (ResultadoItem.Sugerido, false) => "sugestão",
        (ResultadoItem.Sugerido, true) => "sugeriria",
        (ResultadoItem.JaExistia, _) => "já existia (não duplicou)",
        (ResultadoItem.FechouSozinho, false) => "fechou sozinho",
        (ResultadoItem.FechouSozinho, true) => "fecharia sozinho",
        (ResultadoItem.Reaberto, false) => "reaberto",
        (ResultadoItem.Reaberto, true) => "reabriria",
        (ResultadoItem.MantidoSemConferir, _) => "mantido (sem dados para conferir)",
        (ResultadoItem.DecisaoHumanaMantida, _) => "decisão humana mantida",
        _ => r.ToString(),
    };
}

/// <summary>Uma linha do resultado da operação em um lote (vai para a tela, o relatório e a verificação V13).</summary>
public sealed record ItemOperacao
{
    /// <summary>Número do chamado; nulo no ensaio quando o chamado seria novo.</summary>
    public string? Numero { get; init; }
    public required string PlaybookId { get; init; }
    public required string Acao { get; init; }
    public required string Titulo { get; init; }
    public required ResultadoItem Resultado { get; init; }
    public required EstadoChamado Estado { get; init; }

    /// <summary>A plataforma agiu (ou agiria, no ensaio) sem aprovação humana neste lote.</summary>
    public bool AgiuSozinho { get; init; }

    public required string Motivo { get; init; }
    public required string Confianca { get; init; }
    public required IReadOnlyList<string> TiposAtivo { get; init; }
    public int Maquinas { get; init; }
    public int Evidencias { get; init; }
}

/// <summary>Quantas ações cairiam em cada caminho se a autonomia fosse este nível (ensaio sobre o mesmo lote).</summary>
public sealed record SimulacaoNivel(int Nivel, int Sozinho, int Aprovacao, int Sugestao);

/// <summary>Resultado da operação automática para um lote.</summary>
public sealed record ResultadoOperacao
{
    public required string LoteId { get; init; }
    public required DateTimeOffset QuandoUtc { get; init; }
    public required int Nivel { get; init; }
    public required string DescricaoNivel { get; init; }
    public required bool Ensaio { get; init; }
    public required bool LoteValidado { get; init; }
    public required int LimitePorDia { get; init; }

    /// <summary>Ações feitas sozinhas hoje, já contando este lote.</summary>
    public required int AutomaticasHoje { get; init; }

    public required IReadOnlyList<ItemOperacao> Itens { get; init; }
    public IReadOnlyList<SimulacaoNivel> SimulacaoPorNivel { get; init; } = [];

    public int Contar(ResultadoItem r) => Itens.Count(i => i.Resultado == r);
    public int Sozinho => Itens.Count(i => i.AgiuSozinho);
    public int AguardandoAprovacao => Itens.Count(i => i.Estado == EstadoChamado.AguardandoAprovacao);
}
