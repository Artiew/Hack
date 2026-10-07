using Piersec.Core.Analise;
using Piersec.Core.Modelo;

namespace Piersec.Core.Operacao;

public enum TipoDecisao { Sugerir, PedirAprovacao, AgirSozinho }

/// <summary>A decisão e o motivo dela, em português (vai para o log de decisão).</summary>
public sealed record Decisao(TipoDecisao Tipo, string Motivo);

/// <summary>
/// Decide se uma ação é feita sozinha, espera aprovação ou vira sugestão. As travas são checadas NESTA ordem e
/// valem em qualquer nível: só sugestão → nível 0 → lote não validado → alto impacto → irreversível →
/// playbook que exige aprovação → ativo crítico → nível insuficiente → limite do dia. Só passa quem não caiu em nenhuma.
/// </summary>
public static class MotorDecisao
{
    public static Decisao Decidir(RegrasAutomacao config, RegrasAtivos ativos, Playbook p, Ocorrencia o, bool loteValidado, int automaticasHoje)
    {
        var nivel = config.NivelAutonomia;
        if (p.SomenteSugestao || p.Acao == Acoes.Sugerir)
            return new(TipoDecisao.Sugerir, "Este playbook só sugere: quem decide é uma pessoa.");
        if (nivel == 0)
            return new(TipoDecisao.Sugerir, $"Nível de autonomia 0: {config.DescricaoNivel(0)}.");
        if (!loteValidado)
            return new(TipoDecisao.PedirAprovacao, "O lote não passou em todas as verificações de consistência: nada é feito sozinho.");
        if (p.Impacto == Nivel.Alto)
            return new(TipoDecisao.PedirAprovacao, "Ação de alto impacto: sempre exige aprovação humana, em qualquer nível.");
        if (!p.Reversivel)
            return new(TipoDecisao.PedirAprovacao, "Ação que não dá para desfazer: sempre exige aprovação humana.");
        if (p.ExigeAprovacao)
            return new(TipoDecisao.PedirAprovacao, "Este playbook sempre exige aprovação humana.");
        var criticos = o.TiposAtivo.Where(config.TiposCriticos.Contains).Order(StringComparer.Ordinal).ToList();
        if (p.AlteraAtivo && criticos.Count > 0)
            return new(TipoDecisao.PedirAprovacao,
                $"A ação mexe em {string.Join(" e ", criticos.Select(t => ativos.Tipo(t).Singular))} (ativo crítico): sempre com uma pessoa no circuito.");
        if (nivel < p.NivelParaAgirSozinho)
            return new(TipoDecisao.PedirAprovacao,
                $"Nível de autonomia {nivel}: {config.DescricaoNivel(nivel)}. Esta ação só seria feita sozinha a partir do nível {p.NivelParaAgirSozinho}.");
        if (automaticasHoje >= config.LimiteAcoesPorDia)
            return new(TipoDecisao.PedirAprovacao,
                $"Limite de {config.LimiteAcoesPorDia} ações automáticas por dia atingido: o resto espera uma pessoa.");

        var onde = p.AlteraAtivo ? "fora dos ativos críticos" : "sem mexer em nenhuma máquina";
        return new(TipoDecisao.AgirSozinho,
            $"Nível de autonomia {nivel} permite: ação reversível, de impacto {NomeImpacto(p.Impacto)}, {onde}. Pode ser desfeita.");
    }

    public static string NomeImpacto(Nivel n) => n switch
    {
        Nivel.Baixo => "baixo",
        Nivel.Medio => "médio",
        _ => "alto",
    };

    /// <summary>
    /// Confere, por um caminho independente, que nenhuma ação feita sozinha furou as travas (verificação V13).
    /// Usa só o resultado gravado e as regras, sem chamar <see cref="Decidir"/>.
    /// </summary>
    public static Verificacao Conferir(ResultadoOperacao? resultado, RegrasAutomacao config)
    {
        const string Id = "V13";
        const string Descricao = "A operação automática respeitou as travas (aprovação humana, ativos críticos, nível e limite do dia)";
        if (resultado is null)
            return new(Id, Descricao, true, "operação automática não rodou neste lote");

        var playbooks = config.Playbooks.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var problemas = new List<string>();
        foreach (var item in resultado.Itens.Where(i => i.AgiuSozinho))
        {
            if (!playbooks.TryGetValue(item.PlaybookId, out var p))
            {
                problemas.Add($"{item.PlaybookId}: playbook desconhecido");
                continue;
            }
            if (p.Impacto == Nivel.Alto || !p.Reversivel || p.ExigeAprovacao || p.SomenteSugestao || p.Acao == Acoes.Sugerir)
                problemas.Add($"{item.Numero ?? p.Id}: ação que exige pessoa foi feita sozinha");
            if (p.AlteraAtivo && item.TiposAtivo.Any(config.TiposCriticos.Contains))
                problemas.Add($"{item.Numero ?? p.Id}: mexeu em ativo crítico sem aprovação");
            if (resultado.Nivel < p.NivelParaAgirSozinho)
                problemas.Add($"{item.Numero ?? p.Id}: nível {resultado.Nivel} não permite agir sozinho");
        }
        if (resultado.Sozinho > 0 && !resultado.LoteValidado)
            problemas.Add("agiu sozinha com o lote não validado");
        if (resultado.AutomaticasHoje > resultado.LimitePorDia)
            problemas.Add($"passou do limite do dia ({resultado.AutomaticasHoje} de {resultado.LimitePorDia})");

        return problemas.Count == 0
            ? new(Id, Descricao, true, $"{resultado.Sozinho} ação(ões) feita(s) sozinha(s), todas dentro das travas; {resultado.AguardandoAprovacao} aguardando aprovação")
            : new(Id, Descricao, false, string.Join("; ", problemas.Take(5)));
    }
}
