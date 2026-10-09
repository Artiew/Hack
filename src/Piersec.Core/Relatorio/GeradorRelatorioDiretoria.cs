using System.Text;
using Piersec.Core.Analise;
using Piersec.Core.Modelo;

namespace Piersec.Core.Relatorio;

public sealed partial class GeradorRelatorioHtml
{
    // Documento próprio para decisão: duas páginas de decisão e uma de glossário, sem tabelas técnicas.
    private string GerarDiretoria(Lote lote)
    {
        var a = lote.Avaliacao;
        var score = a?.Score;
        var sb = new StringBuilder($"""
            <!doctype html><html lang="pt-BR"><head><meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Relatório de segurança — versão para a diretoria</title><style>{EstiloResumoDiretoria}</style></head><body>
            <section class='folha'><header><p class='selo'>{H(SeloConfidencialidade)}</p>
            <p class='marca'>ARV Security · decisão executiva</p><h1>Resumo para a diretoria</h1>
            <p class='meta'>{H(Formatar.DataHora(lote.CriadoEmUtc, fusoExibicao))} (Brasília) · referência {H(lote.Id[..8])}</p></header>
            """);
        var validado = lote.Validacao?.TudoCerto == true;
        sb.Append(validado
            ? "<p class='validacao'>Consistência dos números verificada pela plataforma.</p>"
            : "<p class='alerta'><strong>RELATÓRIO NÃO VALIDADO.</strong> Não use estes números para decidir até a equipe responsável concluir a conferência.</p>");
        var cobertura = score is null ? "não disponível" : $"{score.PercentualMedido:0}%";
        sb.Append($"""
            <div class='situacao'><div class='numero'>{(score?.Valor is { } v ? Inteiro(v) : "—")}<small>de 100 · maior = pior</small></div>
            <div><p class='rotulo'>Como estamos</p><h2>Risco {H(score?.Faixa.ToLowerInvariant() ?? "não calculado")}</h2>
            <p>{H(Curto(a?.FraseSituacao ?? "Não há informações suficientes para avaliar o ambiente."))}</p></div></div>
            <div class='indicadores'>
            <article class='perda'><p>Prejuízo potencial</p><strong>{H(lote.Impacto is { } i ? Formatar.FaixaCurta(i.Minimo, i.Maximo) : "Não estimado")}</strong><small>Se os serviços em risco forem atingidos</small></article>
            <article class='custo'><p>{(a?.Acoes.Count > 0 && a.Acoes.All(x => x.Tipo == PlanejadorAvaliacao.AcaoMedirFatores) ? "Custo para ampliar a análise" : "Custo do plano")}</p>
            <strong>{H(a?.CustoTotalMinimo is { } min && a.CustoTotalMaximo is { } max ? Formatar.FaixaCurta((decimal)min, (decimal)max) : "A estimar")}</strong><small>Estimativa para o plano inteiro</small></article></div>
            <p class='premissa'>Valores de referência do exercício. Não são perdas comprovadas, orçamento aprovado ou economia garantida.</p>
            <div class='contexto'><p><strong>Evolução:</strong> {H(TendenciaDiretoria(lote))}</p>
            <p><strong>Confiança {H(score?.Confianca ?? "não disponível")} · cobertura {H(cobertura)}.</strong> A conclusão vale apenas para as informações recebidas. Falta de dados não significa ausência de risco.</p></div>
            <h2>Principal causa</h2>
            """);
        var principal = score?.Fatores.Where(f => f.Valor is not null && f.Contribuicao > 0).OrderByDescending(f => f.Contribuicao).FirstOrDefault();
        sb.Append($"<p>{H(principal?.Nome ?? "Nenhum fator medido somou risco; isso não comprova segurança completa.")}</p>");
        sb.Append("<h2>O que é mais grave</h2><ul class='riscos'>");
        foreach (var r in a?.Riscos.Take(3) ?? [])
            sb.Append($"<li><strong>{H(Curto(r.Titulo))}</strong> <span class='gravidade'>{H(r.Severidade.Nome())}</span></li>");
        if (a?.Riscos.Count is not > 0)
            sb.Append("<li>Nenhum problema grave identificado nas informações analisadas.</li>");
        sb.Append("</ul><h2>Serviços que merecem atenção</h2><p>");
        sb.Append(H(Curto(ServicosEmRisco(lote) ?? "Nenhum serviço classificado em risco com os dados disponíveis.")));
        sb.Append("</p><footer>1 de 3 · A próxima página traz as decisões e os prazos.</footer></section>");
        sb.Append("<section class='folha'><header><p class='marca'>ARV Security · prioridades</p><h1>O que fazer agora, em ordem</h1><p>Encaminhe estas prioridades à equipe responsável e acompanhe os prazos.</p></header>");
        foreach (var acao in a?.Acoes.OrderBy(x => x.Ordem).Take(5) ?? [])
        {
            var custo = acao.CustoMinimo is { } cmin && acao.CustoMaximo is { } cmax
                ? Formatar.FaixaCurta((decimal)cmin, (decimal)cmax) : "a estimar";
            sb.Append($"""
                <article class='acao'><h2>{acao.Ordem}. {H(AcaoParaDiretoria(acao))}</h2>
                <p>{H(Curto(acao.Impacto))}</p><p class='meta'><strong>Até {acao.PrazoDias} dias</strong> · esforço {H(NomeNivel(acao.Esforco))} · custo {H(custo)}</p></article>
                """);
        }
        if (a?.Acoes.Count is not > 0)
            sb.Append("<p>Nenhuma correção foi proposta com as informações disponíveis. Mantenha o acompanhamento e complete a análise se faltarem fontes.</p>");
        if (a?.Acoes.Count > 5)
            sb.Append("<p>Mostramos as cinco primeiras prioridades. As demais ações e seu detalhamento estão com a equipe responsável; o custo acima considera o plano inteiro.</p>");
        if (a?.ScorePrevisto is { } previsto && score?.Valor is { } atual)
            sb.Append($"<p class='resultado'><strong>Resultado simulado: risco de {H(regras.Score.FaixaDe(atual).Nome.ToLowerInvariant())} para {H(regras.Score.FaixaDe(previsto).Nome.ToLowerInvariant())} ({Inteiro(atual)} → {Inteiro(previsto)}).</strong> Projeção condicionada à realização e verificação das correções; nenhuma alteração foi executada.</p>");
        sb.Append("<h2>O que considerar antes de decidir</h2><ul class='limites'><li>O relatório considera somente os arquivos enviados. A consistência interna não comprova a veracidade da fonte.</li>");
        if (score is null || score.PercentualMedido < 100)
            sb.Append("<li>A análise está incompleta. Peça à equipe que obtenha as informações que faltam antes de concluir que todo o ambiente está seguro.</li>");
        if (lote.AvisosAmbiente.Count > 0)
            sb.Append("<li>Há indícios de ambientes diferentes entre os arquivos. Confirme se pertencem à mesma empresa antes de usar os totais consolidados.</li>");
        if (lote.Arquivos.Any(x => x.Fonte is null) || lote.FontesAusentes.Count > 0)
            sb.Append("<li>Há fontes não recebidas ou arquivos não reconhecidos. A equipe deve completar a coleta.</li>");
        if (lote.Ajustes.Count > 0)
            sb.Append($"<li>Esta análise contém {Formatar.Qtd(lote.Ajustes.Count, "classificação revisada", "classificações revisadas")} por pessoas, com motivos registrados.</li>");
        sb.Append($"<li>As ações são simuladas. Planos preparados não significam correções aplicadas.</li></ul><p class='meta'>A origem dos números, os critérios e as instruções de execução estão na {H(ReferenciaVersaoCompleta)}.</p><footer>2 de 3 · Consulte os termos na próxima página. {H(SeloConfidencialidade)}</footer></section>");
        sb.Append($"""
            <section class='folha'><header><p class='marca'>ARV Security · consulta rápida</p><h1>Glossário rápido</h1>
            <p>Os termos do relatório, em linguagem simples, para apoiar sua decisão.</p></header>
            <dl class='glossario-curto'>
            <div><dt>Score de risco</dt><dd>Nota de 0 a 100: quanto maior, maior o risco nas informações analisadas. Não representa a chance percentual de um ataque.</dd></div>
            <div><dt>Falha ou vulnerabilidade</dt><dd>Uma fraqueza que pode facilitar um ataque. Encontrar uma falha não significa que houve invasão.</dd></div>
            <div><dt>Exposição à internet</dt><dd>Um equipamento ou serviço pode ser acessado de fora da empresa. A equipe deve confirmar se esse acesso é necessário e está protegido.</dd></div>
            <div><dt>Alerta de segurança</dt><dd>Um sinal que merece investigação. Sozinho, não confirma um incidente.</dd></div>
            <div><dt>Correlação</dt><dd>Ligação entre informações de fontes diferentes sobre a mesma máquina e o mesmo ambiente. Ajuda a priorizar a investigação; não comprova que uma falha foi usada em um ataque.</dd></div>
            <div><dt>Cobertura e confiança da análise</dt><dd>A cobertura mostra quanto do risco previsto foi possível medir. A confiança indica o quanto a conclusão está apoiada nas informações recebidas. Dados ausentes limitam a conclusão.</dd></div>
            <div><dt>Prejuízo potencial</dt><dd>Faixa de perda estimada caso os serviços em risco sejam atingidos. É uma referência do exercício, não uma perda já ocorrida.</dd></div>
            <div><dt>Custo do plano</dt><dd>Faixa estimada para realizar as ações propostas. Precisa ser confirmada pela equipe e não equivale a um orçamento aprovado.</dd></div>
            <div><dt>Evolução do risco</dt><dd>Comparação com a análise anterior. Só é válida quando os mesmos fatores foram medidos; uma nota menor com menos dados não comprova melhora.</dd></div>
            <div><dt>Simulação</dt><dd>Projeção do resultado se as correções forem realizadas e verificadas. Não altera equipamentos nem garante economia.</dd></div>
            </dl><footer>3 de 3 · {H(SeloConfidencialidade)}</footer></section></body></html>
            """);
        return sb.ToString();
    }

    private static string TendenciaDiretoria(Lote lote) => lote.Comparacao switch
    {
        null => "Primeira análise; ainda não há comparação.",
        { MesmosFatoresMedidos: not true } => "Comparação indisponível: as informações medidas mudaram ou não puderam ser confirmadas.",
        { Diferenca: > 0 } c => $"Piorou {Inteiro(c.Diferenca)} pontos em relação à análise anterior.",
        { Diferenca: < 0 } c => $"Melhorou {Inteiro(Math.Abs(c.Diferenca))} pontos em relação à análise anterior.",
        _ => "Sem mudança em relação à análise anterior.",
    };

    private static string AcaoParaDiretoria(AcaoRecomendada acao) => acao.Tipo switch
    {
        PlanejadorAvaliacao.AcaoAtualizarProgramas => "Atualizar os programas com mais falhas",
        PlanejadorAvaliacao.AcaoCorrigirMaquinas => "Corrigir as máquinas mais vulneráveis",
        PlanejadorAvaliacao.AcaoInvestigarPicos => "Verificar os sinais de tentativa de ataque",
        PlanejadorAvaliacao.AcaoMedirFatores => "Completar a análise de segurança",
        PlanejadorAvaliacao.AcaoRevisarExposicao => "Revisar e restringir acessos pela internet",
        _ => "Avaliar a recomendação com a equipe responsável",
    };

    private const string EstiloResumoDiretoria = """
        @page{size:A4;margin:14mm}*{box-sizing:border-box}body{font-family:"Segoe UI",Arial,sans-serif;color:#222a25;background:#fff;margin:0;font-size:11pt;line-height:1.4}
        .folha{max-width:180mm;margin:24px auto;padding:20px}.folha+.folha{break-before:page}h1{font-size:24pt;margin:8px 0}h2{font-size:13pt;margin:16px 0 6px}p{margin:6px 0}strong{font-weight:700}
        .selo{display:inline-block;border:1px solid #555;padding:4px 8px;font-size:8pt;letter-spacing:.04em}.marca{color:#7d5709;font-size:9pt;font-weight:700;text-transform:uppercase;letter-spacing:.08em;margin-top:16px}
        .meta,.premissa,small,footer{color:#505b53;font-size:9pt}.validacao{color:#276142;font-size:9pt}.alerta{border:2px solid #ac3025;padding:10px;color:#7e241d}
        .situacao{display:flex;gap:20px;align-items:center;background:#f3f5f1;border-left:5px solid #c6993e;padding:14px;margin:14px 0}.situacao h2{font-size:18pt;margin:0}.rotulo{text-transform:uppercase;font-size:9pt}.numero{font-size:36pt;font-weight:750;min-width:105px}.numero small{display:block;font-size:8pt;font-weight:400}
        .indicadores{display:grid;grid-template-columns:1fr 1fr;gap:12px}.indicadores article{padding:12px;border:1px solid #ded7c6;border-radius:8px}.indicadores strong{display:block;font-size:18pt}.indicadores small{display:block;margin-top:4px}.perda{background:#fff0e8}.custo{background:#fff6d9}.premissa{margin:8px 0}.contexto{border-bottom:1px solid #d7ddd5;padding:8px 0}
        .riscos{padding-left:18px}.riscos li{padding:4px 0}.gravidade{font-size:9pt;color:#8b342a}.acao{border:1px solid #d7ddd5;border-left:4px solid #c6993e;border-radius:6px;padding:10px 14px;margin:12px 0;break-inside:avoid}.acao h2{margin:0 0 5px}.resultado{background:#f3f5f1;padding:10px}.limites{padding-left:18px}.limites li{margin:5px 0}footer{border-top:1px solid #d7ddd5;padding-top:10px;margin-top:18px}
        .glossario-curto{margin:18px 0 0}.glossario-curto div{padding:9px 0;border-bottom:1px solid #d7ddd5;break-inside:avoid}.glossario-curto dt{font-weight:700;color:#7d5709}.glossario-curto dd{margin:3px 0 0}
        @media print{body{font-size:10pt}.folha{padding:0;margin:0;max-width:none}h1{font-size:22pt}.marca{margin-top:10px}.situacao{margin:10px 0;padding:12px}.indicadores strong{font-size:17pt}.acao{padding:9px 12px;margin:10px 0}.acao h2{font-size:12pt}h2{break-after:avoid}.alerta{font-size:10pt}}
        @media screen and (max-width:600px){.folha{padding:16px;margin:0}.situacao{align-items:flex-start;gap:12px}.indicadores{grid-template-columns:1fr}.numero{min-width:80px;font-size:28pt}}
        """;
}
