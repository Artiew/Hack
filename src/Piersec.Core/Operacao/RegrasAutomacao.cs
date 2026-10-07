using Piersec.Core.Analise;
using Piersec.Core.Modelo;

namespace Piersec.Core.Operacao;

/// <summary>
/// Um playbook: "quando o gatilho X aparecer, faça a ação Y". Lido de regras/automacao.json.
/// Textos entre chaves, como {ativo}, são preenchidos com os dados do gatilho.
/// </summary>
public sealed record Playbook
{
    public required string Id { get; init; }
    public required string Nome { get; init; }
    public required string Gatilho { get; init; }
    public required string Acao { get; init; }
    public required Nivel Impacto { get; init; }
    public required bool Reversivel { get; init; }

    /// <summary>Quando true, nunca age sozinho, em nenhum nível.</summary>
    public bool ExigeAprovacao { get; init; }

    /// <summary>Quando true, só vira sugestão (a decisão é sempre de uma pessoa).</summary>
    public bool SomenteSugestao { get; init; }

    /// <summary>Menor nível de autonomia em que este playbook pode agir sem aprovação.</summary>
    public required int NivelParaAgirSozinho { get; init; }

    /// <summary>Quantos chamados NOVOS este playbook pode abrir por lote (os mais graves primeiro).</summary>
    public required int MaximoPorLote { get; init; }

    public required string Titulo { get; init; }
    public required string Motivo { get; init; }
    public required IReadOnlyList<string> Passos { get; init; }
    public required string ComoDesfazer { get; init; }
    public Frameworks Frameworks { get; init; } = Frameworks.Vazio;

    /// <summary>A ação mexe na máquina (e não só registra um chamado)?</summary>
    public bool AlteraAtivo => Acoes.QueAlteramAtivo.Contains(Acao);
}

/// <summary>Ações conhecidas. Todas são SIMULADAS: viram registro e arquivo, nunca comando em outro sistema.</summary>
public static class Acoes
{
    public const string AbrirChamado = "abrir_chamado";
    public const string AplicarCorrecao = "aplicar_correcao";
    public const string IsolarMaquina = "isolar_maquina";
    public const string Sugerir = "sugerir";

    public static readonly IReadOnlySet<string> Todas = new HashSet<string> { AbrirChamado, AplicarCorrecao, IsolarMaquina, Sugerir };
    public static readonly IReadOnlySet<string> QueAlteramAtivo = new HashSet<string> { AplicarCorrecao, IsolarMaquina };

    public static string Nome(string acao) => acao switch
    {
        AbrirChamado => "abrir chamado",
        AplicarCorrecao => "plano de correção (simulado)",
        IsolarMaquina => "isolar máquina (simulado)",
        Sugerir => "sugestão",
        _ => acao,
    };
}

/// <summary>regras/automacao.json: nível de autonomia, modo de ensaio, limite diário e playbooks.</summary>
public sealed record RegrasAutomacao
{
    public string Descricao { get; init; } = "";

    /// <summary>0 = só sugere · 1 = tudo com aprovação · 2 = baixo impacto sozinho · 3 = também impacto médio sozinho.</summary>
    public required int NivelAutonomia { get; init; }

    public required IReadOnlyDictionary<string, string> Niveis { get; init; }

    /// <summary>Ensaio (dry-run): mostra o que faria, mas não grava nenhum chamado.</summary>
    public bool ModoEnsaio { get; init; }

    public required int LimiteAcoesPorDia { get; init; }

    /// <summary>Tipos de máquina em que nenhuma ação que altera o ativo acontece sem uma pessoa.</summary>
    public required IReadOnlyList<string> TiposCriticos { get; init; }

    public required IReadOnlyList<Playbook> Playbooks { get; init; }

    public const int NivelMaximo = 3;

    public string DescricaoNivel(int nivel) => Niveis.TryGetValue(nivel.ToString(), out var d) ? d : "";

    /// <summary>Tudo o que está errado no arquivo (lista vazia = válido).</summary>
    public IEnumerable<string> Problemas(RegrasAtivos ativos)
    {
        if (NivelAutonomia is < 0 or > NivelMaximo)
            yield return $"'nivelAutonomia' precisa estar entre 0 e {NivelMaximo} (está {NivelAutonomia})";
        for (var n = 0; n <= NivelMaximo; n++)
            if (string.IsNullOrWhiteSpace(DescricaoNivel(n)))
                yield return $"'niveis' precisa descrever o nível {n}";
        if (LimiteAcoesPorDia < 1)
            yield return "'limiteAcoesPorDia' precisa ser pelo menos 1";
        foreach (var tipo in TiposCriticos.Where(t => !ativos.Tipos.ContainsKey(t)))
            yield return $"'tiposCriticos' usa o tipo '{tipo}', que não existe em ativos.json";
        if (Playbooks.Count == 0)
            yield return "precisa de pelo menos um playbook";
        foreach (var id in Playbooks.GroupBy(p => p.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            yield return $"playbook com id repetido: {id}";

        foreach (var p in Playbooks)
        {
            if (!Gatilhos.Campos.TryGetValue(p.Gatilho, out var campos))
            {
                yield return $"playbook {p.Id}: gatilho '{p.Gatilho}' não existe";
                continue;
            }
            if (!Acoes.Todas.Contains(p.Acao))
                yield return $"playbook {p.Id}: ação '{p.Acao}' não existe";
            if (p.NivelParaAgirSozinho is < 1 or > NivelMaximo)
                yield return $"playbook {p.Id}: 'nivelParaAgirSozinho' precisa estar entre 1 e {NivelMaximo}";
            if (p.MaximoPorLote < 1)
                yield return $"playbook {p.Id}: 'maximoPorLote' precisa ser pelo menos 1";
            if (p.Passos.Count == 0 || string.IsNullOrWhiteSpace(p.ComoDesfazer))
                yield return $"playbook {p.Id}: precisa de 'passos' e de 'comoDesfazer'";
            foreach (var texto in p.Passos.Append(p.Titulo).Append(p.Motivo).Append(p.ComoDesfazer))
                foreach (var campo in Textos.Marcadores(texto).Where(c => !campos.Contains(c)))
                    yield return $"playbook {p.Id}: o texto usa {{{campo}}}, que não existe para o gatilho '{p.Gatilho}'";
        }
    }
}

/// <summary>Preenche os {campos} dos textos dos playbooks.</summary>
public static class Textos
{
    public static IEnumerable<string> Marcadores(string texto) =>
        ConjuntoRegras.Marcador().Matches(texto).Select(m => m.Groups[1].Value);

    public static string Preencher(string texto, IReadOnlyDictionary<string, string> valores) =>
        ConjuntoRegras.Marcador().Replace(texto, m => valores.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
}
