using Piersec.Core.Coleta;
using Piersec.Core.Modelo;
using Piersec.Core.Relatorio;

namespace Piersec.Core.Analise;

/// <summary>
/// Monta o que o executivo lê: a situação numa frase, os 3 riscos principais (o que é, por que importa,
/// como resolver) e o plano de ação com impacto CALCULADO (quantas falhas graves cada ação elimina).
/// </summary>
public sealed class PlanejadorAvaliacao(ConjuntoRegras regras, CalculadoraScore calculadora)
{
    public const string AcaoAtualizarProgramas = "atualizar_programas";
    public const string AcaoCorrigirMaquinas = "corrigir_maquinas";
    public const string AcaoInvestigarPicos = "investigar_picos";
    public const string AcaoMedirFatores = "medir_fatores";
    public const string AcaoRevisarExposicao = "revisar_exposicao";

    private RegrasPlano Plano => regras.Plano;

    public Avaliacao Avaliar(IReadOnlyList<EventoSeguranca> eventos, IReadOnlyList<Correlacao> correlacoes, IReadOnlySet<string> tiposMedidos)
    {
        var score = calculadora.Calcular(eventos, tiposMedidos);
        var graves = eventos
            .Where(e => e.Tipo == TiposEvento.Vulnerabilidade && e.Severidade >= Severidade.Alta && e.Ativo != ContextoConversao.NaoInformado)
            .ToList();

        var acoes = MontarAcoes(eventos, graves, correlacoes, score, out var removidas);
        var riscos = MontarRiscos(eventos, graves, correlacoes, score, acoes);

        // Máquinas que continuam na média (valendo 0) quando as falhas delas são "resolvidas" numa simulação.
        var populacao = eventos.Where(e => e.Tipo == TiposEvento.Vulnerabilidade && e.Ativo != ContextoConversao.NaoInformado)
            .GroupBy(e => e.Ativo, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().TipoAtivo, StringComparer.Ordinal);

        double? previsto = null;
        if (removidas.Count > 0)
        {
            var restantes = eventos.Where(e => !removidas.Contains(e.Id)).ToList();
            previsto = calculadora.Calcular(restantes, tiposMedidos, populacao).Valor;
        }

        var (achados, percentualExplicado, scoreSemAchados) = AchadosQueMaisPesam(eventos, tiposMedidos, score, populacao);

        // Quanto CADA ação melhora o score e quanto custa fazer. A queda é recalculada pela mesma calculadora,
        // tirando os eventos que a ação resolve: nada é estimado "no olho".
        acoes = ComEfeitoECusto(acoes, eventos, tiposMedidos, populacao, score);
        var custoTotal = CalculadoraCustos.Somar(acoes.Select(a => a.CustoMinimo is { } min && a.CustoMaximo is { } max ? new FaixaCusto(min, max) : null));

        return new Avaliacao
        {
            Score = score,
            FraseSituacao = FraseSituacao(eventos, graves, score),
            Riscos = riscos,
            Acoes = acoes,
            FraseUmaCoisa = FraseUmaCoisa(acoes),
            ScorePrevisto = previsto,
            AchadosPrincipais = achados,
            PercentualExplicado = percentualExplicado,
            ScoreSemAchados = scoreSemAchados,
            CustoTotalMinimo = custoTotal?.Minimo,
            CustoTotalMaximo = custoTotal?.Maximo,
            MaquinasPorTipo = eventos.Where(e => e.Ativo != ContextoConversao.NaoInformado)
                .GroupBy(e => e.TipoAtivo ?? RegrasAtivos.Desconhecido)
                .ToDictionary(g => regras.Ativos.Tipo(g.Key).Plural, g => g.Select(e => e.Ativo).Distinct().Count()),
        };
    }

    /// <summary>
    /// Completa cada ação com duas respostas que o executivo pede: "quanto isso melhora o score?" e "quanto custa?".
    /// A melhora vem de recalcular o score (mesma calculadora do score oficial) NA ORDEM DO PLANO: cada ação conta as
    /// anteriores como já feitas. Assim as melhoras somadas dão exatamente a queda do plano inteiro (o "de 50 para 6").
    /// Calculada isolada, uma ação que só termina o que a anterior começou pareceria inútil: a máquina ainda teria as
    /// falhas que a ação anterior resolve, e o score quase não mudaria.
    /// O custo vem de regras/custos.json e é sempre uma FAIXA, com a premissa declarada no relatório.
    /// </summary>
    private List<AcaoRecomendada> ComEfeitoECusto(List<AcaoRecomendada> acoes, IReadOnlyList<EventoSeguranca> eventos,
        IReadOnlySet<string> tiposMedidos, IReadOnlyDictionary<string, string?> populacao, ScoreRisco score)
    {
        var custos = new CalculadoraCustos(regras.Custos);
        var total = score.Valor;
        var feitas = new HashSet<string>(StringComparer.Ordinal);   // eventos resolvidos pelas ações anteriores do plano
        var antes = total;                                           // score depois das ações anteriores
        var completas = new List<AcaoRecomendada>(acoes.Count);
        foreach (var acao in acoes.OrderBy(x => x.Ordem))
        {
            double? queda = null, percentual = null;
            if (total is { } atual && atual > 0 && antes is { } scoreAntes && acao.EventoIds.Count > 0)
            {
                feitas.UnionWith(acao.EventoIds);
                var depois = calculadora.Calcular(eventos.Where(e => !feitas.Contains(e.Id)).ToList(), tiposMedidos, populacao).Valor ?? 0;
                queda = Math.Round(Math.Max(scoreAntes - depois, 0), 1);
                percentual = Math.Round(100 * queda.Value / atual, 1);   // sempre sobre o score de HOJE, para as partes somarem
                antes = depois;
            }
            var custo = custos.Calcular(acao.Tipo, acao.Maquinas);
            completas.Add(acao with
            {
                QuedaScore = queda,
                PercentualScore = percentual,
                CustoMinimo = custo?.Minimo,
                CustoMaximo = custo?.Maximo,
            });
        }
        return completas;
    }

    // ---------------- "3 achados explicam X% do risco" ----------------

    public const int QuantidadeAchadosPrincipais = 3;

    /// <summary>
    /// Para cada candidato (programa, máquina ou grupo de alertas), recalcula o score SEM ele e mede a queda.
    /// Os 3 que mais derrubam o score são os achados principais; o X% vem de recalcular sem os 3 juntos
    /// (não é a soma das quedas, porque um mesmo evento pode estar em dois achados). Mesma calculadora do score:
    /// nada é estimado. Para não pesar, só os candidatos mais fortes de cada tipo entram na conta.
    /// </summary>
    private (List<AchadoPrincipal>, double?, double?) AchadosQueMaisPesam(IReadOnlyList<EventoSeguranca> eventos,
        IReadOnlySet<string> tiposMedidos, ScoreRisco score, IReadOnlyDictionary<string, string?> populacao)
    {
        if (score.Valor is not { } total || total <= 0)
            return ([], null, null);

        // Cada candidato tem duas versões do texto: a técnica (anexos) e a ação em linguagem simples (executivo).
        var candidatos = new List<(string Descricao, string Acao, HashSet<string> Ids)>();
        HashSet<string> Ids(IEnumerable<EventoSeguranca> g) => g.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var vulnerabilidades = eventos.Where(e => e.Tipo == TiposEvento.Vulnerabilidade && e.Ativo != ContextoConversao.NaoInformado).ToList();

        foreach (var g in vulnerabilidades.Where(e => !string.IsNullOrWhiteSpace(e.Pacote))
                     .GroupBy(e => e.Pacote!.Trim().ToLowerInvariant(), StringComparer.Ordinal)
                     .OrderByDescending(g => g.Count(e => e.Severidade >= Severidade.Alta)).ThenByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                     .Take(10))
        {
            var programa = g.First().Pacote!.Trim();
            var maquinas = Qtd(g.Select(e => e.Ativo).Distinct().Count(), "máquina", "máquinas");
            candidatos.Add(($"falhas no programa {programa} ({maquinas}, {Qtd(g.Count(), "falha", "falhas")})",
                            $"Atualizar o programa {programa}, que está em {maquinas}", Ids(g)));
        }

        foreach (var g in vulnerabilidades.GroupBy(e => e.Ativo, StringComparer.Ordinal)
                     .OrderByDescending(g => calculadora.PontosDaMaquina(g) * regras.Ativos.Tipo(g.First().TipoAtivo).PesoNoScore)
                     .ThenByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                     .Take(5))
        {
            var tipo = g.First().TipoAtivo;
            var acao = tipo == "servidor" ? $"Corrigir o servidor {g.Key}" : $"Corrigir a máquina {g.Key} ({regras.Ativos.Tipo(tipo).Singular})";
            candidatos.Add(($"a máquina {g.Key} ({regras.Ativos.Tipo(tipo).Singular}, {Qtd(g.Count(), "falha", "falhas")})", acao, Ids(g)));
        }

        foreach (var g in eventos.Where(e => e.Tipo == TiposEvento.Alerta && e.Severidade is { } s && regras.Score.PesoPorAlerta[s] > 0)
                     .GroupBy(e => (e.Ativo, Regra: e.RegraOrigem ?? "?"))
                     .OrderByDescending(g => g.Sum(e => regras.Score.PesoPorAlerta[e.Severidade!.Value]))
                     .ThenBy(g => g.Key.Ativo, StringComparer.Ordinal).ThenBy(g => g.Key.Regra, StringComparer.Ordinal)
                     .Take(5))
            candidatos.Add(($"{Qtd(g.Count(), "alerta", "alertas")} da regra {g.Key.Regra} em {g.Key.Ativo}",
                            $"Verificar os sinais de ataque repetidos registrados pelo firewall {g.Key.Ativo}", Ids(g)));

        double ScoreSem(HashSet<string> ids) =>
            calculadora.Calcular(eventos.Where(e => !ids.Contains(e.Id)).ToList(), tiposMedidos, populacao).Valor ?? 0;

        // Empate na queda: vence quem entrou antes na lista (programa, depois máquina, depois alertas), porque
        // "atualizar o programa X" é a ação mais simples de pedir e de fazer.
        var principais = new List<(string Descricao, string Acao, HashSet<string> Ids, double Queda)>();
        foreach (var c in candidatos
                     .Select((c, ordem) => (c.Descricao, c.Acao, c.Ids, Queda: Math.Round(total - ScoreSem(c.Ids), 1), Ordem: ordem))
                     .Where(c => c.Queda >= 0.1)
                     .OrderByDescending(c => c.Queda).ThenBy(c => c.Ordem))
        {
            // "o programa X" e "a máquina Y" podem ser exatamente as mesmas falhas: conta uma vez só.
            if (principais.Any(p => p.Ids.SetEquals(c.Ids)))
                continue;
            principais.Add((c.Descricao, c.Acao, c.Ids, c.Queda));
            if (principais.Count == QuantidadeAchadosPrincipais)
                break;
        }
        if (principais.Count == 0)
            return ([], null, null);

        var semTodos = Math.Round(ScoreSem(principais.SelectMany(c => c.Ids).ToHashSet(StringComparer.Ordinal)), 1);
        var percentual = Math.Round(100 * (total - semTodos) / total, 1);
        return (principais.Select(c => new AchadoPrincipal(c.Descricao, Math.Round(c.Queda, 1), c.Ids.Count, c.Acao)).ToList(), percentual, semTodos);
    }

    // ---------------- plano de ação ----------------

    private List<AcaoRecomendada> MontarAcoes(IReadOnlyList<EventoSeguranca> eventos, List<EventoSeguranca> graves,
        IReadOnlyList<Correlacao> correlacoes, ScoreRisco score, out HashSet<string> removidas)
    {
        var acoes = new List<AcaoRecomendada>();
        var jaEliminadas = new HashSet<string>(StringComparer.Ordinal);
        removidas = jaEliminadas;
        var total = graves.Count;

        // 1) Atualizar os programas que concentram mais falhas graves
        var programas = graves.Where(e => e.Pacote is not null)
            .GroupBy(e => e.Pacote!.Trim().ToLowerInvariant(), StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(Plano.ProgramasNaPrimeiraAcao)
            .ToList();
        if (programas.Count > 0)
        {
            var eliminadas = programas.SelectMany(g => g).ToList();
            var pct = Percentual(eliminadas.Count, total);
            var atualizacoes = eliminadas.Select(e => (e.Ativo, e.Pacote!.ToLowerInvariant())).Distinct().Count();
            var maquinas = eliminadas.Select(e => e.Ativo).Distinct().Count();
            var nomes = Lista(programas.Select(g => g.First().Pacote!));
            acoes.Add(Nova(
                AcaoAtualizarProgramas,
                $"Atualizar {(programas.Count == 1 ? "o programa" : $"os {programas.Count} programas")} com mais falhas: {nomes}",
                $"atualizar {nomes}",
                $"Instalar a versão mais nova {(programas.Count == 1 ? "desse programa" : "desses programas")} em {Qtd(maquinas, "máquina", "máquinas")} " +
                $"({Qtd(atualizacoes, "atualização", "atualizações")}). Uma ferramenta de atualização automática faz isso de uma vez.",
                $"Elimina {Pct(pct)} das falhas graves ({Formatar.Numero(eliminadas.Count)} de {Formatar.Numero(total)}).",
                pct, Plano.ImpactoDe(pct), Plano.EsforcoDe(atualizacoes), PrazoDe(eliminadas), maquinas, eliminadas));
            removidas.UnionWith(eliminadas.Select(e => e.Id));
        }

        // 2) Corrigir por completo as máquinas que mais acumulam o que sobrou
        var porMaquina = graves.Where(e => !jaEliminadas.Contains(e.Id))
            .GroupBy(e => e.Ativo, StringComparer.Ordinal)
            .Select(g => (Ativo: g.Key, Eventos: g.ToList(), Tipo: g.First().TipoAtivo,
                          Pontos: calculadora.PontosDaMaquina(g) * regras.Ativos.Tipo(g.First().TipoAtivo).PesoNoScore,
                          Falhas: g.Count()))
            .OrderByDescending(m => m.Pontos).ThenByDescending(m => m.Falhas).ThenBy(m => m.Ativo, StringComparer.Ordinal)
            .Take(Plano.MaquinasNaSegundaAcao)
            .ToList();
        if (porMaquina.Count > 0)
        {
            var eliminadas = porMaquina.SelectMany(m => m.Eventos).ToList();
            var pct = Percentual(eliminadas.Count, total);
            var atualizacoes = eliminadas.Select(e => (e.Ativo, (e.Pacote ?? e.Id).ToLowerInvariant())).Distinct().Count();
            var servidores = porMaquina.Count(m => m.Tipo == "servidor");
            acoes.Add(Nova(
                AcaoCorrigirMaquinas,
                porMaquina.Count == 1 ? "Corrigir por completo a máquina mais vulnerável" : $"Corrigir por completo as {porMaquina.Count} máquinas mais vulneráveis",
                porMaquina.Count == 1 ? "corrigir a máquina mais vulnerável" : $"corrigir as {porMaquina.Count} máquinas mais vulneráveis",
                $"Aplicar todas as atualizações pendentes {(porMaquina.Count == 1 ? "nessa máquina" : "nessas máquinas")}" +
                ComposicaoMaquinas(servidores, porMaquina.Count - servidores) + ". A lista está no anexo B.",
                $"Elimina mais {Pct(pct)} das falhas graves ({Formatar.Numero(eliminadas.Count)}).",
                pct, Plano.ImpactoDe(pct), Plano.EsforcoDe(atualizacoes), PrazoDe(eliminadas), porMaquina.Count, eliminadas));
            removidas.UnionWith(eliminadas.Select(e => e.Id));
        }

        // 3) Investigar os picos de alertas
        var picos = Correlacoes(correlacoes, "rajada_de_alertas");
        if (picos.Count > 0)
        {
            var maiorPico = picos.Max(c => c.EventoIds.Count);
            acoes.Add(Nova(
                AcaoInvestigarPicos,
                "Verificar os picos de tentativas registrados pelo firewall",
                "verificar os picos de tentativas no firewall",
                $"Pedir à equipe de TI que confira {(picos.Count == 1 ? "o momento" : $"os {picos.Count} momentos")} em que o firewall registrou rajadas de alertas " +
                $"(até {Formatar.Numero(maiorPico)} em poucos minutos) e confirme se tudo foi bloqueado.",
                "Confirma se há um ataque em andamento; não muda o número de falhas.",
                null, picos.Max(c => c.Severidade) >= Severidade.Alta ? Nivel.Alto : Nivel.Medio, Nivel.Baixo,
                Plano.PrazoInvestigacaoDias, picos.SelectMany(c => c.Ativos).Distinct().Count(), []));
        }

        var encadeadas = Correlacoes(correlacoes, "ativo_exposto_com_vulnerabilidade_e_alerta");
        if (encadeadas.Count > 0 && picos.Count == 0)
            acoes.Add(Nova(AcaoInvestigarPicos, "Verificar os alertas ligados a máquinas expostas", "verificar alertas em máquinas expostas",
                "Pedir à equipe que confira os alertas direcionados, o acesso externo e as falhas do mesmo ativo. As evidências estão nas correlações do relatório técnico; não presumir invasão.",
                "Confere a relação entre três fontes e orienta a resposta; preparar o plano não reduz o score.",
                null, Nivel.Alto, Nivel.Baixo, Plano.PrazoInvestigacaoDias, encadeadas.Count, []));

        var expostos = eventos.Where(e => e.Tipo == TiposEvento.Exposicao && e.ExpostoInternet == true && e.Ativo != ContextoConversao.NaoInformado)
            .Select(e => e.Ativo).Distinct().Count();
        if (expostos > 0)
            acoes.Add(Nova(AcaoRevisarExposicao, "Revisar e restringir os acessos pela internet", "revisar os acessos pela internet",
                $"Pedir à equipe que confirme a necessidade do acesso externo em {Qtd(expostos, "máquina", "máquinas")}. Restringir acessos desnecessários com aprovação e validar o inventário após a mudança.",
                "Reduz caminhos de entrada se acessos desnecessários forem fechados. O score só muda após novo inventário confirmar a alteração.",
                null, Nivel.Medio, Nivel.Medio, Plano.PrazoInvestigacaoDias, expostos, []));

        // 4) Medir o que hoje não é medido
        var naoMedidos = score.Fatores.Where(f => f.Valor is null).ToList();
        if (naoMedidos.Count > 0)
        {
            acoes.Add(Nova(
                AcaoMedirFatores,
                "Completar a análise de segurança",
                "passar a medir o que falta",
                $"Incluir nas próximas análises: {Lista(naoMedidos.Select(f => f.Nome.ToLowerInvariant()))}. " +
                $"Hoje o score usa só {score.PercentualMedido:0}% do que deveria.",
                "Aumenta a confiança do score; sozinho, não elimina falhas.",
                null, Nivel.Medio, Nivel.Medio, Plano.PrazoMedicaoDias, 0, []));
        }

        string[] ordemQuadrantes = ["Fazer agora", "Planejar", "Se sobrar tempo", "Não priorizar agora"];
        return acoes
            .Select((a, i) => (a, i))
            .OrderBy(x => Array.IndexOf(ordemQuadrantes, x.a.Quadrante)).ThenBy(x => x.i)
            .Select((x, i) => x.a with { Ordem = i + 1 })
            .ToList();
    }

    private static AcaoRecomendada Nova(string tipo, string titulo, string resumo, string comoFazer, string impacto, double? pct,
        Nivel nivelImpacto, Nivel esforco, int prazo, int maquinas, IReadOnlyList<EventoSeguranca> eventos) => new()
    {
        Ordem = 0,
        Tipo = tipo,
        Titulo = titulo,
        Resumo = resumo,
        ComoFazer = comoFazer,
        Impacto = impacto,
        PercentualFalhasGraves = pct is { } p ? Math.Round(p, 1) : null,
        NivelImpacto = nivelImpacto,
        Esforco = esforco,
        PrazoDias = prazo,
        Quadrante = Quadrante(nivelImpacto, esforco),
        Maquinas = maquinas,
        EventoIds = eventos.Select(e => e.Id).Order(StringComparer.Ordinal).ToList(),
    };

    /// <summary>" (1 servidor e 2 outras), começando pelos servidores", " (1 servidor)" ou nada: nunca "e 0 outras".</summary>
    private static string ComposicaoMaquinas(int servidores, int outras) => (servidores, outras) switch
    {
        ( > 0, > 0) => $" ({Qtd(servidores, "servidor", "servidores")} e {Qtd(outras, "outra", "outras")}), começando pelos servidores",
        ( > 0, _) => $" ({Qtd(servidores, "servidor", "servidores")})",
        _ => "",
    };

    /// <summary>Matriz impacto × esforço.</summary>
    public static string Quadrante(Nivel impacto, Nivel esforco) => (impacto, esforco) switch
    {
        (Nivel.Alto, Nivel.Alto) => "Planejar",
        (Nivel.Alto, _) => "Fazer agora",
        (Nivel.Medio, _) => "Planejar",
        (Nivel.Baixo, Nivel.Baixo) => "Se sobrar tempo",
        _ => "Não priorizar agora",
    };

    private int PrazoDe(IEnumerable<EventoSeguranca> eventos) =>
        Plano.PrazoDias[eventos.Max(e => e.Severidade) ?? Severidade.Baixa];

    private static string? FraseUmaCoisa(IReadOnlyList<AcaoRecomendada> acoes)
    {
        var programas = acoes.FirstOrDefault(a => a.Tipo == AcaoAtualizarProgramas);
        if (programas?.PercentualFalhasGraves is not { } p1)
            return acoes.Count > 0 ? $"{acoes[0].Titulo}: {acoes[0].Impacto}" : null;

        var frase = $"{char.ToUpper(programas.Resumo[0])}{programas.Resumo[1..]} em até {programas.PrazoDias} dias elimina {Pct(p1)} das falhas graves";
        var maquinas = acoes.FirstOrDefault(a => a.Tipo == AcaoCorrigirMaquinas);
        if (maquinas?.PercentualFalhasGraves is { } p2)
            frase += $"; se também {maquinas.Resumo}, chega a {Pct(p1 + p2)}";
        return frase + ".";
    }

    // ---------------- riscos principais ----------------

    private List<RiscoNegocio> MontarRiscos(IReadOnlyList<EventoSeguranca> eventos, List<EventoSeguranca> graves,
        IReadOnlyList<Correlacao> correlacoes, ScoreRisco score, IReadOnlyList<AcaoRecomendada> acoes)
    {
        var candidatos = new List<RiscoNegocio>();
        string Acoes(params string[] tipos)
        {
            var numeros = acoes.Where(a => tipos.Contains(a.Tipo)).Select(a => a.Ordem.ToString()).ToList();
            return numeros.Count switch
            {
                0 => "Ver plano de ação.",
                1 => $"Ação {numeros[0]} do plano.",
                _ => $"Ações {Lista(numeros)} do plano.",
            };
        }

        var encadeadas = Correlacoes(correlacoes, "ativo_exposto_com_vulnerabilidade_e_alerta");
        var expostasComFalha = Correlacoes(correlacoes, "ativo_exposto_com_vulnerabilidade");
        if (encadeadas.Count > 0 || expostasComFalha.Count > 0)
        {
            var relacionadas = encadeadas.Count > 0 ? encadeadas : expostasComFalha;
            candidatos.Add(new RiscoNegocio
            {
                Titulo = encadeadas.Count > 0 ? "Máquinas expostas com falhas graves e sinais de ataque" : "Falhas graves em máquinas acessíveis pela internet",
                OQueE = $"{Qtd(relacionadas.Count, "máquina identificada", "máquinas identificadas")} por fontes distintas, com o mesmo ativo e ambiente declarados.",
                PorQueImporta = "A combinação merece prioridade de conferência. Não comprova invasão nem que a falha conhecida tenha sido explorada.",
                ComoResolver = $"Revisar acessos, corrigir programas e verificar os alertas. {Acoes(AcaoRevisarExposicao, AcaoAtualizarProgramas, AcaoInvestigarPicos)}",
                Severidade = relacionadas.Max(c => c.Severidade),
                CorrelacaoIds = relacionadas.Select(c => c.Id).ToList(),
            });
        }
        var expostos = eventos.Where(e => e.Tipo == TiposEvento.Exposicao && e.ExpostoInternet == true && e.Ativo != ContextoConversao.NaoInformado).Select(e => e.Ativo).Distinct().Count();
        if (expostos > 0 && encadeadas.Count == 0 && expostasComFalha.Count == 0)
            candidatos.Add(new RiscoNegocio
            {
                Titulo = $"{Qtd(expostos, "máquina acessível", "máquinas acessíveis")} pela internet",
                OQueE = "O inventário informa acesso externo direto. Não comprova que o acesso seja indevido nem que tenha ocorrido invasão.",
                PorQueImporta = "Acesso externo pode ampliar os caminhos de entrada. A equipe deve confirmar sua necessidade e as proteções existentes.",
                ComoResolver = $"Revisar e restringir acessos desnecessários. {Acoes(AcaoRevisarExposicao)}",
                Severidade = Severidade.Media,
            });

        var maquinasVuln = eventos.Where(e => e.Tipo == TiposEvento.Vulnerabilidade && e.Ativo != ContextoConversao.NaoInformado)
            .Select(e => e.Ativo).Distinct().Count();
        if (graves.Count > 0)
        {
            var comGrave = graves.GroupBy(e => e.Ativo).ToList();
            var porTipo = comGrave.GroupBy(g => g.First().TipoAtivo ?? RegrasAtivos.Desconhecido)
                .OrderByDescending(t => regras.Ativos.Tipo(t.Key).PesoNoScore).ThenByDescending(t => t.Count())
                .Select(t => $"{Formatar.Numero(t.Count())} {(t.Count() == 1 ? regras.Ativos.Tipo(t.Key).Singular : regras.Ativos.Tipo(t.Key).Plural)}");
            candidatos.Add(new RiscoNegocio
            {
                Titulo = $"Programas com falhas graves em {Pct(Percentual(comGrave.Count, maquinasVuln))} das máquinas",
                OQueE = $"{Formatar.Numero(comGrave.Count)} de {Formatar.Numero(maquinasVuln)} máquinas usam programas com falhas graves já conhecidas: {Lista(porTipo)}.",
                PorQueImporta = "Falha conhecida é uma porta de entrada que criminosos já sabem usar, sem precisar de senha. Num servidor, uma invasão pode parar sistemas usados pela empresa inteira.",
                ComoResolver = $"Atualizar os programas. {Acoes(AcaoAtualizarProgramas, AcaoCorrigirMaquinas)}",
                Severidade = graves.Max(e => e.Severidade) ?? Severidade.Alta,
            });
        }

        var espalhadas = Correlacoes(correlacoes, "mesma_vulnerabilidade_em_varios_ativos");
        if (espalhadas.Count > 0)
        {
            candidatos.Add(new RiscoNegocio
            {
                Titulo = "O mesmo defeito repetido em muitas máquinas",
                OQueE = $"{Qtd(espalhadas.Count, "falha aparece repetida", "falhas aparecem repetidas")} em muitas máquinas; a mais espalhada está em {Formatar.Numero(espalhadas.Max(c => c.Ativos.Count))} máquinas.",
                PorQueImporta = "Um ataque que funcione em uma delas funciona em todas: o estrago se multiplica.",
                ComoResolver = $"Atualizar os programas mais repetidos resolve de uma vez. {Acoes(AcaoAtualizarProgramas)}",
                Severidade = espalhadas.Max(c => c.Severidade),
                CorrelacaoIds = espalhadas.Select(c => c.Id).ToList(),
            });
        }

        var alertasRelevantes = eventos.Where(e => e.Tipo == TiposEvento.Alerta && e.Severidade >= Severidade.Media).ToList();
        var picos = Correlacoes(correlacoes, "rajada_de_alertas");
        if (alertasRelevantes.Count > 0 || picos.Count > 0)
        {
            var textoPicos = picos.Count == 0 ? "" :
                $" e {Qtd(picos.Count, "pico", "picos")}, com até {Formatar.Numero(picos.Max(c => c.EventoIds.Count))} alertas em poucos minutos";
            candidatos.Add(new RiscoNegocio
            {
                Titulo = "Sinais de tentativas de ataque no firewall",
                OQueE = $"O firewall registrou {Qtd(alertasRelevantes.Count, "alerta que merece", "alertas que merecem")} análise{textoPicos}.",
                PorQueImporta = "Pode ser alguém testando a entrada da empresa. Os dados não mostram se alguma tentativa teve sucesso.",
                ComoResolver = $"A equipe de TI verifica os picos. {Acoes(AcaoInvestigarPicos)}",
                Severidade = alertasRelevantes.Count > 0 ? alertasRelevantes.Max(e => e.Severidade)!.Value : picos.Max(c => c.Severidade),
                CorrelacaoIds = picos.Select(c => c.Id).ToList(),
            });
        }

        var acumulam = Correlacoes(correlacoes, "ativo_acumula_vulnerabilidades");
        if (acumulam.Count > 0)
        {
            candidatos.Add(new RiscoNegocio
            {
                Titulo = "Máquinas que acumulam muitas falhas críticas",
                OQueE = $"{Qtd(acumulam.Count, "máquina acumula", "máquinas acumulam")} muitas falhas críticas; a pior tem {Formatar.Numero(acumulam.Max(c => c.EventoIds.Count))}.",
                PorQueImporta = "São o alvo mais fácil: basta uma das falhas para alguém entrar.",
                ComoResolver = $"Corrigir primeiro as piores. {Acoes(AcaoCorrigirMaquinas)}",
                Severidade = acumulam.Max(c => c.Severidade),
                CorrelacaoIds = acumulam.Select(c => c.Id).ToList(),
            });
        }

        var naoMedidos = score.Fatores.Where(f => f.Valor is null).ToList();
        if (naoMedidos.Count > 0)
        {
            candidatos.Add(new RiscoNegocio
            {
                Titulo = "Parte do risco não está sendo medida",
                OQueE = $"Não há dados sobre: {Lista(naoMedidos.Select(f => f.Nome.ToLowerInvariant()))}.",
                PorQueImporta = $"O que não é medido pode esconder problemas. O score considera só {score.PercentualMedido:0}% do que deveria.",
                ComoResolver = $"Incluir essas fontes nas próximas análises. {Acoes(AcaoMedirFatores)}",
                Severidade = Severidade.Media,
            });
        }

        // Ordem fixa e explicável: falhas, repetição, ataques, acúmulo, pontos cegos. Mostramos os 3 primeiros.
        return candidatos.Take(3).ToList();
    }

    private static string FraseSituacao(IReadOnlyList<EventoSeguranca> eventos, List<EventoSeguranca> graves, ScoreRisco score)
    {
        if (score.Valor is null)
            return "Não há dados suficientes para avaliar o ambiente.";
        var maquinas = eventos.Where(e => e.Tipo == TiposEvento.Vulnerabilidade && e.Ativo != ContextoConversao.NaoInformado)
            .Select(e => e.Ativo).Distinct().Count();
        var partes = new List<string>();
        var expostos = eventos.Where(e => e.Tipo == TiposEvento.Exposicao && e.ExpostoInternet == true && e.Ativo != ContextoConversao.NaoInformado).Select(e => e.Ativo).Distinct().Count();
        if (expostos > 0)
            partes.Add($"{Qtd(expostos, "máquina tem", "máquinas têm")} acesso declarado pela internet");
        if (graves.Count > 0)
            partes.Add($"{Pct(Percentual(graves.Select(e => e.Ativo).Distinct().Count(), maquinas))} das máquinas usam programas com falhas graves já conhecidas");
        if (eventos.Any(e => e.Tipo == TiposEvento.Alerta && e.Severidade >= Severidade.Media))
            partes.Add("o firewall registrou sinais de tentativas de ataque");
        return partes.Count == 0
            ? "Nenhum problema grave foi encontrado nas fontes analisadas."
            : char.ToUpper(partes[0][0]) + string.Join(", e ", partes)[1..] + ".";
    }

    // ---------------- apoio ----------------

    private List<Correlacao> Correlacoes(IReadOnlyList<Correlacao> correlacoes, string tipoRegra)
    {
        var ids = regras.Correlacoes.Where(r => r.Tipo == tipoRegra).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        return correlacoes.Where(c => ids.Contains(c.RegraId)).ToList();
    }

    private static double Percentual(int parte, int total) => total == 0 ? 0 : 100.0 * parte / total;

    /// <summary>"1 máquina", "3 máquinas" — sem o "(s)" que confunde o leitor.</summary>
    private static string Qtd(int n, string singular, string plural) => $"{Formatar.Numero(n)} {(n == 1 ? singular : plural)}";

    private static string Pct(double valor) => valor.ToString("0", Formatar.PtBr) + "%";

    private static string Lista(IEnumerable<string> itens)
    {
        var l = itens.ToList();
        return l.Count switch
        {
            0 => "",
            1 => l[0],
            _ => string.Join(", ", l[..^1]) + " e " + l[^1],
        };
    }
}
