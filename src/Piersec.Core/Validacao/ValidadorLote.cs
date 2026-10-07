using Piersec.Core.Analise;
using Piersec.Core.Coleta;
using Piersec.Core.Modelo;
using Piersec.Core.Relatorio;

namespace Piersec.Core.Validacao;

/// <summary>
/// Confere a consistência do lote ANTES de emitir o relatório (RF8). Cada verificação diz o que conferiu e,
/// se falhar, por quê. O score é recalculado aqui por um SEGUNDO CAMINHO, escrito de forma independente da
/// <see cref="CalculadoraScore"/>: se as duas contas divergirem, uma delas está errada.
/// </summary>
public sealed class ValidadorLote(ConjuntoRegras regras, TimeProvider relogio)
{
    public static readonly DateTimeOffset DataMinimaPlausivel = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const double Tolerancia = 0.2; // arredondamentos de 1 casa decimal

    public ResultadoValidacao Validar(Lote lote)
    {
        var v = new List<Verificacao>
        {
            SomaPorSeveridade(lote),
            LidosPorArquivo(lote),
            TodoEventoTemOrigem(lote),
            DatasCoerentes(lote),
            ArquivoVazioDeclarado(lote),
            CorrelacoesApontamParaEventos(lote),
        };
        if (lote.Avaliacao is { } a)
        {
            v.Add(ContribuicoesSomamScore(a));
            v.Add(ScorePorSegundoCaminho(lote, a));
            v.Add(PercentuaisDoPlano(a));
            v.Add(MaquinasPorTipo(lote, a));
        }
        v.Add(SemCredenciais(lote));
        return new ResultadoValidacao(v);
    }

    // ---------------- verificações ----------------

    private static Verificacao SomaPorSeveridade(Lote lote)
    {
        var soma = SeveridadeExtensoes.DoMaisGrave.Sum(s => lote.ContarPorSeveridade(s)) + lote.ContarPorSeveridade(null);
        return new("V01", "A soma dos eventos por severidade é igual ao total",
            soma == lote.Eventos.Count, $"soma {Formatar.Numero(soma)} · total {Formatar.Numero(lote.Eventos.Count)}");
    }

    private static Verificacao LidosPorArquivo(Lote lote)
    {
        var porArquivo = lote.Eventos.GroupBy(e => e.Evidencia.Arquivo).ToDictionary(g => g.Key, g => g.Count());
        var divergentes = lote.Arquivos.Where(a => a.RegistrosLidos != porArquivo.GetValueOrDefault(a.Nome)).Select(a => a.Nome).ToList();
        var somaLidos = lote.Arquivos.Sum(a => a.RegistrosLidos);
        return new("V02", "Os registros lidos de cada arquivo batem com os eventos do lote",
            divergentes.Count == 0 && somaLidos == lote.Eventos.Count,
            divergentes.Count == 0 ? $"{Formatar.Numero(somaLidos)} lidos = {Formatar.Numero(lote.Eventos.Count)} eventos"
                                   : $"divergência em: {string.Join(", ", divergentes)}");
    }

    private static Verificacao TodoEventoTemOrigem(Lote lote)
    {
        var sem = lote.Eventos.Count(e =>
            string.IsNullOrWhiteSpace(e.Fonte) || string.IsNullOrWhiteSpace(e.Evidencia.Arquivo) || e.Evidencia.Linha <= 0 ||
            e.Evidencia.Sha256Arquivo.Length != 64 || string.IsNullOrWhiteSpace(e.Justificativa));
        return new("V03", "Todo evento tem fonte, arquivo, linha, impressão digital do arquivo e justificativa",
            sem == 0, sem == 0 ? $"{Formatar.Numero(lote.Eventos.Count)} eventos conferidos" : $"{Formatar.Numero(sem)} evento(s) sem origem completa");
    }

    private Verificacao DatasCoerentes(Lote lote)
    {
        var limiteFuturo = relogio.GetUtcNow().AddDays(1);
        var futuras = lote.Eventos.Count(e => e.DataUtc > limiteFuturo);
        var antigas = lote.Eventos.Count(e => e.DataUtc < DataMinimaPlausivel);
        return new("V04", "Datas coerentes (nenhuma no futuro nem antes de 2000)",
            futuras == 0 && antigas == 0,
            futuras + antigas == 0 ? $"{Formatar.Numero(lote.Eventos.Count(e => e.DataUtc is not null))} datas conferidas"
                                   : $"{Formatar.Numero(futuras)} data(s) no futuro e {Formatar.Numero(antigas)} antes de 2000");
    }

    private static Verificacao ArquivoVazioDeclarado(Lote lote)
    {
        var vazios = lote.Arquivos.Where(a => a.Fonte is not null && a.RegistrosLidos == 0).ToList();
        var semLacuna = vazios.Where(a => !lote.Lacunas.Any(l => l.Arquivo == a.Nome && l.Campo == "arquivo")).Select(a => a.Nome).ToList();
        return new("V05", "Fonte sem registros aparece como lacuna, nunca como \"zero problemas\"",
            semLacuna.Count == 0,
            vazios.Count == 0 ? "nenhuma fonte vazia" : semLacuna.Count == 0 ? $"{vazios.Count} fonte(s) vazia(s), todas declaradas" : $"sem lacuna: {string.Join(", ", semLacuna)}");
    }

    private static Verificacao CorrelacoesApontamParaEventos(Lote lote)
    {
        var ids = lote.Eventos.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var quebradas = lote.Correlacoes.Count(c => c.EventoIds.Count == 0 || c.EventoIds.Any(id => !ids.Contains(id)) || string.IsNullOrWhiteSpace(c.Justificativa));
        return new("V06", "Toda correlação aponta para eventos que existem e tem justificativa",
            quebradas == 0, quebradas == 0 ? $"{Formatar.Numero(lote.Correlacoes.Count)} correlações conferidas" : $"{quebradas} correlação(ões) com problema");
    }

    private static Verificacao ContribuicoesSomamScore(Avaliacao a)
    {
        var soma = a.Score.Fatores.Sum(f => f.Contribuicao);
        var ok = a.Score.Valor is not { } valor ? soma == 0 : Math.Abs(soma - valor) <= Tolerancia;
        return new("V07", "A soma das contribuições dos fatores é igual ao score",
            ok, $"soma {soma.ToString("0.0", Formatar.PtBr)} · score {(a.Score.Valor?.ToString("0.0", Formatar.PtBr) ?? "não calculado")}");
    }

    /// <summary>Segundo caminho: mesmo resultado, código diferente (laço simples, sem reaproveitar a calculadora).</summary>
    private Verificacao ScorePorSegundoCaminho(Lote lote, Avaliacao a)
    {
        var sc = regras.Score;
        var medidos = lote.Arquivos.Where(x => x.TipoEvento is not null).Select(x => x.TipoEvento!).ToHashSet();

        // Fator falhas: por máquina, pior falha + acúmulo (uma falha por CVE), limitado ao teto; média ponderada pelo tipo
        var porMaquina = new Dictionary<string, (Severidade Pior, HashSet<string> Falhas, string? Tipo)>(StringComparer.Ordinal);
        foreach (var e in lote.Eventos)
        {
            if (e.Tipo != TiposEvento.Vulnerabilidade || e.Ativo == ContextoConversao.NaoInformado || e.Severidade is not { } sev)
                continue;
            if (!porMaquina.TryGetValue(e.Ativo, out var m))
                m = (sev, new HashSet<string>(StringComparer.Ordinal), e.TipoAtivo);
            m.Falhas.Add(e.Cve ?? e.Id);
            porMaquina[e.Ativo] = (sev > m.Pior ? sev : m.Pior, m.Falhas, m.Tipo);
        }
        double somaPesos = 0, somaPontos = 0;
        foreach (var m in porMaquina.Values)
        {
            var peso = regras.Ativos.Tipo(m.Tipo).PesoNoScore;
            var pontos = Math.Min(sc.TetoPorMaquina, sc.NotaPelaPiorFalha[m.Pior] + (m.Falhas.Count - 1) * sc.PontosPorFalhaAdicional);
            somaPesos += peso;
            somaPontos += peso * pontos;
        }
        double? falhas = medidos.Contains(TiposEvento.Vulnerabilidade) ? (somaPesos == 0 ? 0 : somaPontos / somaPesos) : null;

        // Fator ameaças: peso médio dos alertas classificados
        double totalAlertas = 0, pesoAlertas = 0;
        foreach (var e in lote.Eventos)
        {
            if (e.Tipo != TiposEvento.Alerta || e.Severidade is not { } sev)
                continue;
            totalAlertas++;
            pesoAlertas += sc.PesoPorAlerta[sev];
        }
        double? ameacas = medidos.Contains(TiposEvento.Alerta) ? (totalAlertas == 0 ? 0 : 100 * pesoAlertas / totalAlertas) : null;

        double numerador = 0, denominador = 0;
        foreach (var f in sc.Fatores)
        {
            var valor = f.Id switch { "vulnerabilidades" => falhas, "ameacas" => ameacas, _ => null };
            if (valor is null)
                continue;
            numerador += f.Peso * Math.Round(valor.Value, 1);
            denominador += f.Peso;
        }
        double? recalculado = denominador == 0 ? null : Math.Round(numerador / denominador, 1);

        var ok = (recalculado, a.Score.Valor) switch
        {
            (null, null) => true,
            ({ } x, { } y) => Math.Abs(x - y) <= Tolerancia,
            _ => false,
        };
        return new("V08", "O score recalculado por um segundo caminho é igual ao do relatório",
            ok, $"relatório {(a.Score.Valor?.ToString("0.0", Formatar.PtBr) ?? "—")} · recálculo {(recalculado?.ToString("0.0", Formatar.PtBr) ?? "—")}");
    }

    private static Verificacao PercentuaisDoPlano(Avaliacao a)
    {
        var percentuais = a.Acoes.Where(x => x.PercentualFalhasGraves is not null).Select(x => x.PercentualFalhasGraves!.Value).ToList();
        var soma = percentuais.Sum();
        var ok = percentuais.All(p => p is >= 0 and <= 100) && soma <= 100 + Tolerancia;
        return new("V09", "Os percentuais do plano de ação ficam entre 0% e 100% e, somados, não passam de 100%",
            ok, $"soma {soma.ToString("0.0", Formatar.PtBr)}%");
    }

    private static Verificacao MaquinasPorTipo(Lote lote, Avaliacao a)
    {
        var distintas = lote.Eventos.Where(e => e.Ativo != ContextoConversao.NaoInformado).Select(e => e.Ativo).Distinct().Count();
        var soma = a.MaquinasPorTipo.Values.Sum();
        return new("V10", "As máquinas por tipo somam o total de máquinas",
            soma == distintas, $"soma {Formatar.Numero(soma)} · total {Formatar.Numero(distintas)}");
    }

    private static Verificacao SemCredenciais(Lote lote)
    {
        var textos = lote.Eventos.SelectMany(e => new[] { e.Descricao, e.Detalhe, e.Ativo, e.Pacote, e.Referencia })
            .Concat(lote.Correlacoes.SelectMany(c => new[] { c.Titulo, c.Justificativa }));
        var encontradas = textos.Sum(FiltroCredenciais.Contar);
        // Informativa: o relatório mascara tudo o que o filtro encontra. A prova de que nada vazou é a V12,
        // que lê o relatório já pronto (ver ConferirRelatorioFinal).
        return new("V11", "Credenciais encontradas nos dados (senha, token, chave) são mascaradas",
            true, encontradas == 0 ? "nenhuma encontrada nos dados" : $"{Formatar.Numero(encontradas)} encontrada(s) nos dados e mascarada(s)");
    }

    /// <summary>V12: lê o relatório PRONTO. Se ainda houver algo com cara de credencial, o relatório é retido.</summary>
    public static Verificacao ConferirRelatorioFinal(string html)
    {
        var encontradas = FiltroCredenciais.Contar(html);
        return new("V12", "O relatório final não contém nenhuma credencial",
            encontradas == 0, encontradas == 0 ? "relatório lido por inteiro: nada encontrado" : $"{encontradas} trecho(s) suspeito(s): relatório retido");
    }
}
