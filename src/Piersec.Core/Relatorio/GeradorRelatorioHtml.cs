using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Piersec.Core.Analise;
using Piersec.Core.Coleta;
using Piersec.Core.Modelo;
using Piersec.Core.Operacao;

namespace Piersec.Core.Relatorio;

/// <summary>
/// Completo: tudo, para admin e analista. Diretoria: só o que o executivo usa para decidir (resumo, serviços e prejuízo,
/// score, plano completo e glossário); automação, limites e anexos ficam na versão completa.
/// </summary>
public enum VersaoRelatorio { Completo, Diretoria }

/// <summary>
/// Monta o relatório em HTML a partir do lote (RG1: ninguém digita número nenhum).
/// Página 1 é para quem não é da área: como estamos, o que é mais grave, o que fazer agora.
/// O detalhe técnico fica nos anexos. Todo texto vindo dos dados passa por <see cref="H"/> (escape de HTML);
/// o HTML não carrega nada externo (sem CDN, sem script).
/// </summary>
public sealed class GeradorRelatorioHtml(TimeZoneInfo fusoExibicao, ConjuntoRegras regras)
{
    public const string SeloConfidencialidade = "CONFIDENCIAL — uso restrito à diretoria e à equipe de segurança";
    public const int MaximoEventosListados = 25;
    public const int MaximoCorrelacoesListadas = 15;
    public const int MaximoMaquinasListadas = 15;
    public const int MaximoGruposDeLacunas = 50;
    public const int TamanhoMaximoTexto = 220;

    private readonly CalculadoraScore _calculadora = new(regras.Score, regras.Ativos);

    /// <summary>Na versão da diretoria, as referências a anexos apontam para a versão completa (que o executivo não recebe).</summary>
    public const string ReferenciaVersaoCompleta = "versão completa do relatório, com a equipe técnica";

    public string Gerar(Lote lote, VersaoRelatorio versao = VersaoRelatorio.Completo)
    {
        var diretoria = versao == VersaoRelatorio.Diretoria;
        var sb = new StringBuilder();
        sb.Append($"""
            <!doctype html>
            <html lang="pt-BR">
            <head>
            <meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Relatório de segurança{(diretoria ? " para a diretoria" : "")} — lote {H(lote.Id[..8])}</title>
            <style>{Estilo}</style>
            </head>
            <body>
            {Sprite}
            <header class="capa">
              <p class="selo">{H(SeloConfidencialidade)}</p>
              <h1>Relatório de segurança{(diretoria ? " — versão para a diretoria" : "")}</h1>
              <p class="meta">Gerado automaticamente pela plataforma em {H(Formatar.DataHora(lote.CriadoEmUtc, fusoExibicao))} (horário de Brasília)
              · lote {H(lote.Id[..8])} · {lote.Arquivos.Count(a => a.Fonte is not null)} de {lote.Arquivos.Count} arquivo(s) analisado(s).
              {(diretoria
                  ? $"O detalhe técnico (operação automática, limites da análise e anexos) está na {ReferenciaVersaoCompleta}."
                  : "A primeira página é para a diretoria; o detalhe técnico está nos anexos.")}</p>
              {SeloValidacao(lote.Validacao, diretoria)}
            </header>
            """);

        if (lote.Avaliacao is { } avaliacao)
        {
            PaginaDiretoria(sb, lote, avaliacao, diretoria);
            SecaoServicos(sb, lote);
            SecaoScore(sb, avaliacao);
            SecaoPlano(sb, lote, avaliacao, diretoria);
            if (!diretoria)
                SecaoAutomacao(sb, lote.Operacao);
        }

        if (diretoria)
        {
            AnexoGlossario(sb, "Glossário");
        }
        else
        {
            SecaoLimites(sb, lote);
            sb.Append("<h2 class=\"quebra\">Anexos para a equipe técnica</h2>");
            AnexoCorrelacoes(sb, lote);
            AnexoMaquinasEEventos(sb, lote);
            AnexoComoDecidimos(sb, lote);
            AnexoArquivos(sb, lote);
            AnexoLacunas(sb, lote);
            AnexoMitre(sb, lote);
            AnexoMetodologia(sb);
            AnexoGlossario(sb, "G. Glossário");
            AnexoValidacao(sb, lote.Validacao);
            AnexoAjustes(sb, lote);
        }

        sb.Append($"""
            <footer>
              <p>{H(SeloConfidencialidade)}</p>
              <p>Gerado pela plataforma a partir dos arquivos enviados; nenhum número foi digitado à mão.
              Dados sintéticos fornecidos para o Hackathon Piersec.</p>
            </footer>
            </body>
            </html>
            """);
        return sb.ToString();
    }

    /// <summary>
    /// Versão emitida quando a leitura final encontra algo com cara de credencial (V12): nada dos dados sai,
    /// só o aviso de que o relatório foi retido (S1).
    /// </summary>
    public string GerarRetido(Lote lote) => $"""
        <!doctype html>
        <html lang="pt-BR">
        <head>
        <meta charset="utf-8">
        <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'">
        <title>Relatório retido — lote {H(lote.Id[..8])}</title>
        <style>{Estilo}</style>
        </head>
        <body>
        <p class="selo">{H(SeloConfidencialidade)}</p>
        <h1>Relatório retido</h1>
        <div class="nao-validado">A verificação final encontrou no relatório um trecho com cara de credencial (senha, token ou chave).
        Por segurança, o relatório deste lote NÃO foi emitido. A equipe técnica deve revisar os arquivos enviados.</div>
        <p class="meta">Lote {H(lote.Id[..8])} · {H(Formatar.DataHora(lote.CriadoEmUtc, fusoExibicao))} (horário de Brasília)</p>
        </body>
        </html>
        """;

    private static string SeloValidacao(ResultadoValidacao? v, bool diretoria)
    {
        if (v is null)
            return "";
        if (v.TudoCerto)
            return $"<p class=\"selo-ok\">✔ {v.Total} verificações de consistência, {v.Aprovadas} aprovadas{(diretoria ? "" : " (anexo H)")}</p>";
        var falhas = v.Total - v.Aprovadas;
        return $"<div class=\"nao-validado\">✖ RELATÓRIO NÃO VALIDADO: {falhas} de {v.Total} verificações de consistência falharam " +
               $"({H(string.Join("; ", v.Verificacoes.Where(x => !x.Aprovada).Select(x => $"{x.Id}: {x.Descricao}")))}). " +
               $"Os números abaixo NÃO devem ser usados para decisão até a correção. Detalhes {(diretoria ? "na " + ReferenciaVersaoCompleta : "no anexo H")}.</div>";
    }

    /// <summary>
    /// Revisão com severidades ajustadas por pessoas: dito com todas as letras na página 1, com o score de antes,
    /// para ninguém confundir o número revisado com o calculado pela plataforma. Null no lote original.
    /// </summary>
    public static string? AvisoRevisao(Lote lote, bool diretoria = false)
    {
        if (lote.RevisaoDe is null || lote.Ajustes.Count == 0)
            return null;
        var antes = lote.Comparacao is { } c ? $" Antes dos ajustes, o score era {Inteiro(c.ScoreAnterior)}." : "";
        return $"Esta é uma REVISÃO do lote {lote.RevisaoDe[..8]}: " +
               $"{Formatar.Qtd(lote.Ajustes.Count, "severidade foi ajustada", "severidades foram ajustadas")} por pessoas que discordaram da plataforma, " +
               $"cada uma com motivo registrado ({(diretoria ? ReferenciaVersaoCompleta : "anexo I")}).{antes}";
    }

    private void AnexoAjustes(StringBuilder sb, Lote lote)
    {
        if (lote.Ajustes.Count == 0)
            return;
        sb.Append($"""
            <section>
            <h2>I. Severidades ajustadas por pessoas</h2>
            <p>Revisão do lote {H(lote.RevisaoDe?[..8] ?? "")}. A plataforma não muda severidade sozinha: cada linha abaixo é uma pessoa
            discordando, com o motivo dela. O lote original continua guardado como foi calculado.</p>
            <table><thead><tr><th>Evento</th><th>De</th><th>Para</th><th>Quem e quando</th><th>Motivo</th><th>O que a plataforma tinha dito</th></tr></thead><tbody>
            """);
        foreach (var a in lote.Ajustes.OrderBy(a => a.QuandoUtc))
            sb.Append($"<tr><td class=\"id\"><code>{H(a.EventoId)}</code></td><td>{Selo(a.De)}</td><td>{Selo(a.Para)}</td>" +
                      $"<td>{H(a.Usuario)}<br><span class=\"pequeno\">{H(Formatar.DataHora(a.QuandoUtc, fusoExibicao))} (Brasília)</span></td>" +
                      $"<td>{H(a.Motivo)}</td><td class=\"pequeno\">{H(a.JustificativaOriginal)}</td></tr>");
        sb.Append("</tbody></table></section>");
    }

    private static void AnexoValidacao(StringBuilder sb, ResultadoValidacao? v)
    {
        if (v is null)
            return;
        sb.Append($"""
            <section>
            <h2>H. Verificações de consistência</h2>
            <p>Feitas pela plataforma antes de emitir este relatório: {v.Aprovadas} de {v.Total} aprovadas.</p>
            <table><thead><tr><th>Id</th><th>O que foi conferido</th><th>Resultado</th><th>Detalhe</th></tr></thead><tbody>
            """);
        foreach (var x in v.Verificacoes)
            sb.Append($"<tr><td class=\"id\"><code>{H(x.Id)}</code></td><td>{H(x.Descricao)}</td><td>{(x.Aprovada ? "✔ aprovada" : "<strong>✖ reprovada</strong>")}</td><td>{H(x.Detalhe)}</td></tr>");
        sb.Append("</tbody></table></section>");
    }

    // ================= Página 1: para a diretoria =================

    private void PaginaDiretoria(StringBuilder sb, Lote lote, Avaliacao a, bool diretoria)
    {
        var s = a.Score;
        var numero = s.Valor is { } v ? Inteiro(v) : "—";
        var comparacao = lote.Comparacao is { } c
            ? $"{(c.Diferenca > 0 ? "Piorou" : c.Diferenca < 0 ? "Melhorou" : "Igual")}: {Inteiro(Math.Abs(c.Diferenca))} ponto(s) em relação à análise de " +
              $"{Formatar.DataHora(c.DataAnterior, fusoExibicao)} ({Inteiro(c.ScoreAnterior)} de 100) — principal motivo: {c.Motivo}."
            : "Primeira análise: ainda não há uma anterior para comparar.";
        var avisoConfianca = AvisoConfianca(s) is { } aviso ? $"<p class=\"aviso-forte\">⚠ {H(aviso)}</p>" : "";
        var servicos = ServicosEmRisco(lote) is { } emRisco
            ? $"<p><strong>Serviços em risco:</strong> {H(emRisco)}. {H(FrasePrejuizo(lote) ?? "")} <span class=\"meta\">(premissas na seção 2)</span></p>"
            : "";

        sb.Append($"""
            <section class="diretoria">
            <h2>{Ic("resumo")}Resumo para a diretoria</h2>
            <div class="score-caixa {ClasseFaixa(s.Faixa)}">
              <div class="score-numero"><span class="grande">{numero}</span><span class="de"> de 100</span></div>
              <div class="score-texto">
                <p class="rotulo">COMO ESTAMOS: <strong>risco {H(s.Faixa.ToLowerInvariant())}</strong> <span class="meta">(0 = sem risco · 100 = risco máximo)</span></p>
                {avisoConfianca}
                <p class="frase">{H(a.FraseSituacao)}</p>
                {servicos}
                <p>{H(s.FraseFaixa)}</p>
                <p class="meta">{H(comparacao)}<br>Confiança {H(s.Confianca)}: o score usa {s.PercentualMedido:0}% do que deveria medir ({s.Fatores.Count(f => f.Valor is not null)} de {s.Fatores.Count} fatores) — ver seção 3.</p>
              </div>
            </div>
            """);
        sb.Append(FaixaIndicadores(lote, a));

        if (AvisoRevisao(lote, diretoria) is { } revisao)
            sb.Append($"<p class=\"aviso-forte\">⚠ {H(revisao)}</p>");
        foreach (var avisoAmbiente in lote.FontesAusentes.Concat(lote.AvisosAmbiente))
            sb.Append($"<p class=\"aviso-forte\">⚠ {H(avisoAmbiente)}</p>");

        sb.Append($"<div class=\"duas-colunas\"><div class=\"quadro\"><h3>{Ic("limite")}O que é mais grave</h3><div class=\"riscos\">");
        var n = 1;
        foreach (var r in a.Riscos)
        {
            // Cartão: o leitor bate o olho e pega a consequência. O detalhe fica em letra menor, para quem quiser.
            sb.Append($"""
                <div class="risco {ClasseSeveridade(r.Severidade)}">
                  <p class="risco-topo"><span class="ordem">{n++}</span> {Selo(r.Severidade)}</p>
                  <p class="risco-titulo">{H(r.Titulo)}</p>
                  <p class="risco-frase">{H(r.PorQueImporta)}</p>
                  <p class="risco-detalhe">{H(r.OQueE)}</p>
                  <p class="risco-acao"><strong>O que fazer:</strong> {H(r.ComoResolver)}</p>
                </div>
                """);
        }
        if (a.Riscos.Count == 0)
            sb.Append("<p>Nenhum risco relevante encontrado nas fontes analisadas.</p>");
        sb.Append("</div></div>");

        // A mensagem principal vem antes da tabela, para caber sempre na primeira página.
        if (a.FraseUmaCoisa is { } umaCoisa)
        {
            var previsto = a.ScorePrevisto is { } p && s.Valor is { } atual
                ? $" Com as ações de correção, o score estimado cai de {Inteiro(atual)} para {Inteiro(p)}."
                : "";
            sb.Append($"<div class=\"uma-coisa\"><strong>SE FIZER SÓ UMA COISA:</strong> {H(umaCoisa)}{H(previsto)}</div>");
        }

        var principais = a.Acoes.Take(3).ToList();
        if (principais.Count > 0)
        {
            // Direção, não relatório: cada passo numerado, com o prazo e o efeito, para o executivo sair da leitura sabendo o que pedir.
            sb.Append($"<div class=\"quadro\"><h3>{Ic("plano")}Como resolver, em ordem</h3><ol class=\"passos\">");
            foreach (var acao in principais)
            {
                var efeito = acao.PercentualScore is { } pct && pct > 0 ? $" Derruba {Pct(pct)} do risco." : "";
                var custo = acao.CustoMinimo is { } cmin && acao.CustoMaximo is { } cmax ? $" Custa {Formatar.FaixaCurta((decimal)cmin, (decimal)cmax)}." : "";
                sb.Append($"<li><strong>{H(acao.Titulo)}</strong>" +
                          $"<span class=\"passo-meta\">em até {acao.PrazoDias} dias · esforço {H(NomeNivel(acao.Esforco))}</span>" +
                          $"<span class=\"passo-efeito\">{H(acao.Impacto)}{H(efeito)}{H(custo)}</span></li>");
            }
            sb.Append("</ol></div>");
        }
        else
        {
            sb.Append($"<div class=\"quadro\"><h3>{Ic("plano")}Como resolver</h3><p>Nenhuma ação de correção foi proposta com os dados recebidos.</p></div>");
        }
        sb.Append("</div>");
        sb.Append("</section>");
    }

    // ================= 2. Serviços da empresa e prejuízo potencial =================

    /// <summary>"Faturamento (alto), Dados de clientes (crítico) e mais 1": os serviços em risco, para a página 1.</summary>
    public static string? ServicosEmRisco(Lote lote, int maximo = 3)
    {
        var emRisco = lote.Servicos.Where(s => s.EmRisco).ToList();
        if (emRisco.Count == 0)
            return null;
        var nomes = emRisco.Take(maximo).Select(s => $"{s.Nome} ({s.Faixa.ToLowerInvariant()})").ToList();
        if (emRisco.Count > maximo)
            nomes.Add($"mais {emRisco.Count - maximo}");
        return nomes.Count == 1 ? nomes[0] : string.Join(", ", nomes[..^1]) + " e " + nomes[^1];
    }

    /// <summary>"Se forem atingidos, o prejuízo pode ficar entre R$ 700 mil e R$ 7 milhões." (null sem serviço em risco)</summary>
    public static string? FrasePrejuizo(Lote lote) => lote.Impacto is { } i
        ? $"Se {(i.ServicosEmRisco == 1 ? "esse serviço for atingido" : "esses serviços forem atingidos")}, o prejuízo pode ficar {Formatar.FaixaDinheiro(i.Minimo, i.Maximo)}."
        : null;

    /// <summary>
    /// Os quatro números que o executivo lê de relance, antes de qualquer texto: quanto se perde, quanto custa
    /// arrumar, onde o score chega se o plano for feito e para onde o risco está indo. Cada um com a nota do que é.
    /// Quando falta dado, o cartão DIZ que falta — não desaparece nem inventa número.
    /// </summary>
    private string FaixaIndicadores(Lote lote, Avaliacao a)
    {
        var s = a.Score;

        var (prejuizo, notaPrejuizo) = lote.Impacto is { } i
            ? (Formatar.FaixaCurta(i.Minimo, i.Maximo), Formatar.Qtd(i.ServicosEmRisco, "serviço em risco", "serviços em risco"))
            : ("não estimado", "nenhum serviço em risco com valor estimado");

        var (custo, notaCusto) = a.CustoTotalMinimo is { } cmin && a.CustoTotalMaximo is { } cmax
            ? (Formatar.FaixaCurta((decimal)cmin, (decimal)cmax), $"plano inteiro, {Formatar.Qtd(a.Acoes.Count, "ação", "ações")}")
            : ("a estimar", "sem premissa de custo para estas ações");

        var (depois, notaDepois) = s.Valor is { } atual && a.ScorePrevisto is { } previsto
            ? ($"{Inteiro(atual)} → {Inteiro(previsto)}", $"de {regras.Score.FaixaDe(atual).Nome.ToLowerInvariant()} para {regras.Score.FaixaDe(previsto).Nome.ToLowerInvariant()}")
            : (s.Valor is { } so ? Inteiro(so) : "—", "sem ação que derrube o score");

        var (tendencia, notaTendencia) = lote.Comparacao is { } c
            ? (c.Diferenca > 0 ? $"▲ +{Inteiro(c.Diferenca)}" : c.Diferenca < 0 ? $"▼ −{Inteiro(Math.Abs(c.Diferenca))}" : "igual",
               $"em relação à análise anterior ({Inteiro(c.ScoreAnterior)})")
            : ("primeira análise", "ainda não há com o que comparar");

        string Cartao(string rotulo, string valor, string nota, string classe) =>
            $"<div class=\"kpi {classe}\"><span class=\"kpi-rotulo\">{H(rotulo)}</span><strong>{H(valor)}</strong><span class=\"kpi-nota\">{H(nota)}</span></div>";

        return "<div class=\"kpis\">" +
               Cartao("Quanto podemos perder", prejuizo, notaPrejuizo, "kpi-perda") +
               Cartao("Quanto custa corrigir", custo, notaCusto, "kpi-custo") +
               Cartao("Score se o plano for feito", depois, notaDepois, "kpi-score") +
               Cartao("Para onde o risco vai", tendencia, notaTendencia, "kpi-tendencia") +
               "</div>";
    }

    private void SecaoServicos(StringBuilder sb, Lote lote)
    {
        sb.Append($"<section class=\"quebra\"><h2>{Ic("negocio")}2. Serviços da empresa e prejuízo potencial</h2>");
        if (lote.Servicos.Count == 0)
        {
            sb.Append("<p>Nenhuma máquina identificada nos dados: não dá para dizer quais serviços estão em risco.</p></section>");
            return;
        }
        sb.Append(FrasePrejuizo(lote) is { } fraseGeral
            ? $"<p class=\"frase\"><strong>Serviços em risco: {H(ServicosEmRisco(lote, int.MaxValue))}.</strong> {H(fraseGeral)}</p>"
            : "<p class=\"frase\"><strong>Nenhum serviço está em risco elevado ou pior</strong> nas fontes analisadas.</p>");
        sb.Append("<table><thead><tr><th>Serviço</th><th>Importância para o negócio</th><th>Situação</th><th class=\"num\">Máquinas com falha grave</th>" +
                  "<th class=\"num\">Alertas que merecem análise</th><th>Prejuízo se for atingido</th></tr></thead><tbody>");
        foreach (var s in lote.Servicos)
        {
            var prejuizo = s.Impacto is { } i
                ? $"{H(Formatar.FaixaDinheiro(i.Minimo, i.Maximo))}<br><span class=\"pequeno\">{H(i.Premissa)}</span>"
                : "<em>não estimado</em>";
            sb.Append($"<tr><td><strong>{H(s.Nome)}</strong></td><td>{H(NomeImportancia(s.Criticidade))}</td>" +
                      $"<td>{(s.EmRisco ? "<strong>" : "")}{H(s.Faixa)}{(s.Nota is { } n ? $" ({Inteiro(n)})" : "")}{(s.EmRisco ? "</strong>" : "")}</td>" +
                      $"<td class=\"num\">{Formatar.Numero(s.MaquinasComFalhaGrave)} de {Formatar.Numero(s.Maquinas)}</td>" +
                      $"<td class=\"num\">{Formatar.Numero(s.AlertasRelevantes)}</td><td>{prejuizo}</td></tr>");
        }
        sb.Append("</tbody></table>");
        sb.Append($"<p class=\"meta\"><strong>Premissas.</strong> {H(regras.Servicos.Premissa)} {H(regras.Servicos.PremissaImpacto)} " +
                  $"Um serviço conta como \"em risco\" a partir da nota {Inteiro(regras.Servicos.FaixaEmRiscoAPartirDe)} (mesma escala do score).</p></section>");
    }

    private static string NomeImportancia(Severidade criticidade) => criticidade switch
    {
        Severidade.Critica or Severidade.Alta => "alta",
        Severidade.Media => "média",
        _ => "baixa",
    };

    // ================= 3. Score =================

    private void SecaoScore(StringBuilder sb, Avaliacao a)
    {
        var s = a.Score;
        sb.Append($"""
            <section class="quebra">
            <h2>{Ic("score")}3. Entendendo o score</h2>
            <p>{H(regras.Score.Descricao)}</p>
            <p><strong>Por que o score pode mudar quando chegam mais fontes:</strong> ele é a média dos fatores medidos.
            Uma fonte nova acrescenta um fator à conta: se esse fator estiver bom, o score cai; se estiver ruim, sobe.
            Nos dois casos a confiança aumenta, porque mais do risco passa a ser medido.</p>
            <h3>O que mais pesa no score</h3>
            <table><thead><tr><th>Fator (pergunta que responde)</th><th>Situação (0 a 100)</th><th class="num">Peso</th><th class="num">Pontos no score</th></tr></thead><tbody>
            """);
        foreach (var f in s.Fatores.OrderByDescending(f => f.Contribuicao).ThenByDescending(f => f.Peso))
        {
            var situacao = f.Valor is { } v
                ? $"<div class=\"barra\"><span style=\"width:{v.ToString("0", System.Globalization.CultureInfo.InvariantCulture)}%\"></span></div> {Inteiro(v)}"
                : "<em>não medido</em>";
            sb.Append($"<tr><td><strong>{H(f.Nome)}</strong><br><span class=\"meta\">{H(f.Pergunta)}</span><br><span class=\"pequeno\">{H(f.Explicacao)}</span></td>" +
                      $"<td>{situacao}</td><td class=\"num\">{f.Peso:0}</td><td class=\"num\">{(f.Valor is null ? "—" : f.Contribuicao.ToString("0.0", Formatar.PtBr))}</td></tr>");
        }
        sb.Append($"<tr><th colspan=\"3\">Score (soma dos pontos)</th><th class=\"num\">{(s.Valor is { } total ? total.ToString("0.0", Formatar.PtBr) : "—")}</th></tr></tbody></table>");

        if (FraseAchados(a) is { } frase)
        {
            sb.Append($"<h3>{H(TituloAchados(a))}</h3><p><strong>{H(frase)}</strong></p><ol>");
            foreach (var achado in a.AchadosPrincipais)
            {
                // Linguagem simples na frente; o detalhe técnico fica numa linha pequena, para a equipe conferir.
                var detalhe = achado.Acao is null ? "" : $"<br><span class=\"pequeno\">Detalhe técnico: {H(achado.Descricao)}; sozinho, tira {H(Pontos(achado.Pontos))} do score.</span>";
                sb.Append($"<li>{H(achado.Texto)}{detalhe}</li>");
            }
            sb.Append("</ol><p class=\"meta\">Como calculamos: refazendo a conta do score sem cada problema (máquinas corrigidas continuam na média, valendo 0). " +
                      "Juntos, o efeito é menor que a soma, porque uma mesma falha pode estar em dois problemas.</p>");

            // Gráfico: quanto cada problema pesa no score. Uma pergunta só — "o que pesa mais?" — em barras
            // ordenadas, que continuam legíveis impressas em preto e branco.
            if (s.Valor is { } totalScore && totalScore > 0)
            {
                var maior = a.AchadosPrincipais.Max(x => x.Pontos);
                sb.Append("<h3>Peso de cada problema no score</h3><table class=\"grafico\"><tbody>");
                foreach (var achado in a.AchadosPrincipais.OrderByDescending(x => x.Pontos))
                {
                    var largura = maior > 0 ? Math.Round(100 * achado.Pontos / maior) : 0;
                    var pctScore = Math.Round(100 * achado.Pontos / totalScore, 1);
                    sb.Append($"<tr><td>{H(achado.Texto)}</td>" +
                              $"<td class=\"barra-celula\"><div class=\"barra\"><span style=\"width:{largura.ToString("0", System.Globalization.CultureInfo.InvariantCulture)}%\"></span></div></td>" +
                              $"<td class=\"num\"><strong>{H(Pct(pctScore))}</strong> do score<br><span class=\"pequeno\">{H(Pontos(achado.Pontos))}</span></td></tr>");
                }
                sb.Append("</tbody></table><p class=\"meta\">Leia assim: resolver só o primeiro item já tira essa fatia do score. " +
                          "A barra compara os problemas entre si; a porcentagem é sobre o score de hoje.</p>");
            }
        }

        sb.Append("<h3>Faixas</h3><table><thead><tr><th>Score</th><th>Faixa</th><th>O que significa</th></tr></thead><tbody>");
        var faixas = regras.Score.Faixas.OrderByDescending(f => f.Minimo).ToList();
        for (var i = 0; i < faixas.Count; i++)
        {
            var teto = i == 0 ? 100 : faixas[i - 1].Minimo - 1;
            var atual = faixas[i].Nome == s.Faixa ? " class=\"atual\"" : "";
            sb.Append($"<tr{atual}><td>{faixas[i].Minimo:0} a {teto:0}</td><td>{H(faixas[i].Nome)}{(atual.Length > 0 ? " ◀ atual" : "")}</td><td>{H(faixas[i].Frase)}</td></tr>");
        }
        sb.Append("</tbody></table></section>");
    }

    // ================= 3. Plano de ação =================

    private void SecaoPlano(StringBuilder sb, Lote lote, Avaliacao a, bool diretoria)
    {
        sb.Append($"""
            <section>
            <h2>{Ic("plano")}4. Plano de ação completo</h2>
            <p>O resultado de cada ação é <strong>calculado</strong> contando as falhas que ela elimina. Prazos e esforço seguem as premissas {(diretoria ? "da " + ReferenciaVersaoCompleta : "do anexo F")}.</p>
            <table><thead><tr><th>#</th><th>Ação e como fazer</th><th>Resultado esperado</th><th>Melhora no score</th><th>Custo estimado</th><th>Impacto</th><th>Esforço</th><th>Prazo</th></tr></thead><tbody>
            """);
        foreach (var acao in a.Acoes)
        {
            // A lista de máquinas (anexo B) só existe na versão completa.
            var comoFazer = diretoria ? acao.ComoFazer.Replace("A lista está no anexo B.", $"A lista está na {ReferenciaVersaoCompleta}.") : acao.ComoFazer;
            // Ou a ação derruba o score (e dizemos quanto), ou não derruba. "Não calculado" deixaria o leitor em dúvida.
            var melhora = acao.QuedaScore is { } q && acao.PercentualScore is { } pct && q > 0
                ? $"<strong>−{H(Pct(pct))}</strong><br><span class=\"pequeno\">{H(Pontos(q))} a menos</span>"
                : "<em>não baixa o score</em><br><span class=\"pequeno\">ver o resultado esperado</span>";
            var custo = acao.CustoMinimo is { } cmin && acao.CustoMaximo is { } cmax
                ? H(Formatar.FaixaDinheiro((decimal)cmin, (decimal)cmax))
                : "<em>sem premissa</em>";
            sb.Append($"<tr><td class=\"num\">{acao.Ordem}</td><td><strong>{H(acao.Titulo)}</strong><br>{H(comoFazer)}</td><td>{H(acao.Impacto)}</td>" +
                      $"<td>{melhora}</td><td class=\"pequeno\">{custo}</td>" +
                      $"<td>{H(NomeNivel(acao.NivelImpacto))}</td><td>{H(NomeNivel(acao.Esforco))}</td><td>até {acao.PrazoDias} dias</td></tr>");
        }
        sb.Append("</tbody></table>");

        // Quanto custa × quanto evita: as duas faixas lado a lado, na mesma escala, para a diretoria comparar de relance.
        if (a.CustoTotalMinimo is { } totalMin && a.CustoTotalMaximo is { } totalMax)
        {
            var prejuizo = lote.Impacto;
            sb.Append("<h3>Quanto custa corrigir, e quanto está em jogo</h3>");
            sb.Append($"<p class=\"frase\"><strong>Executar o plano inteiro custa {H(Formatar.FaixaDinheiro((decimal)totalMin, (decimal)totalMax))}</strong>");
            if (a.Score.Valor is { } atual && a.ScorePrevisto is { } depois)
                sb.Append($" e leva o score de {Inteiro(atual)} para {Inteiro(depois)}");
            sb.Append(".</p>");

            if (prejuizo is not null)
            {
                var escala = Math.Max((double)prejuizo.Maximo, totalMax);
                string Barra(double valor) => escala <= 0 ? "" :
                    $"<div class=\"barra\"><span style=\"width:{Math.Round(100 * valor / escala).ToString("0", System.Globalization.CultureInfo.InvariantCulture)}%\"></span></div>";
                sb.Append($"""
                    <table class="grafico"><tbody>
                    <tr><td>Prejuízo potencial se os serviços em risco forem atingidos</td><td class="barra-celula">{Barra((double)prejuizo.Maximo)}</td><td class="num"><strong>{H(Formatar.FaixaDinheiro(prejuizo.Minimo, prejuizo.Maximo))}</strong></td></tr>
                    <tr><td>Custo de executar o plano de ação</td><td class="barra-celula">{Barra(totalMax)}</td><td class="num"><strong>{H(Formatar.FaixaDinheiro((decimal)totalMin, (decimal)totalMax))}</strong></td></tr>
                    </tbody></table>
                    <p class="meta">As duas barras estão na mesma escala. Não é promessa de economia: o prejuízo é o que se PERDE caso o
                    ataque aconteça, e ninguém sabe se vai acontecer. A comparação serve para dar ordem de grandeza à decisão.</p>
                    """);
            }
            else
            {
                sb.Append("<p class=\"meta\">Nenhum serviço em risco com prejuízo estimado neste lote, então não há com o que comparar o custo.</p>");
            }

            var hora = regras.Custos.HoraTecnica;
            sb.Append($"<p class=\"pequeno\"><strong>Premissa do custo (não veio dos dados):</strong> {H(hora.Premissa ?? "")} " +
                      "O total é a soma das faixas de cada ação, com horas fixas mais horas por máquina, arredondado para cima no limite superior.");
            if (regras.Custos.NaoIncluido.Count > 0)
                sb.Append($" <strong>Não está incluído:</strong> {H(string.Join("; ", regras.Custos.NaoIncluido))}.");
            sb.Append("</p>");
        }

        string Celula(string quadrante)
        {
            var itens = a.Acoes.Where(x => x.Quadrante == quadrante).Select(x => $"#{x.Ordem}").ToList();
            return $"<td><strong>{H(quadrante)}</strong><br>{(itens.Count == 0 ? "—" : H(string.Join(", ", itens)))}</td>";
        }
        sb.Append($"""
            <h3>Impacto × esforço</h3>
            <table class="matriz"><tbody>
            <tr><th></th><th>Esforço baixo ou médio</th><th>Esforço alto</th></tr>
            <tr><th>Impacto alto</th>{Celula("Fazer agora")}{Celula("Planejar")}</tr>
            <tr><th>Impacto baixo</th>{Celula("Se sobrar tempo")}{Celula("Não priorizar agora")}</tr>
            </tbody></table>
            <p class="meta">Ações de impacto médio entram em "Planejar".</p>
            </section>
            """);
    }

    // ================= 5. Automação =================

    private const int MaximoItensAutomacao = 20;

    private void SecaoAutomacao(StringBuilder sb, ResultadoOperacao? op)
    {
        sb.Append($"<section><h2>{Ic("robo")}5. O que a plataforma fez sozinha</h2>");
        if (op is null)
        {
            sb.Append("<p class=\"meta\">A operação automática não rodou neste lote.</p></section>");
            return;
        }
        var feito = op.Ensaio ? "faria" : "fez";
        sb.Append($"<p><strong>Tudo é simulado:</strong> a plataforma registra o chamado e gera o arquivo com o plano; ela não envia nada a outro sistema nem mexe em nenhuma máquina. " +
                  $"Nível de autonomia {op.Nivel} de {RegrasAutomacao.NivelMaximo}: {H(op.DescricaoNivel)}.{(op.Ensaio ? " <strong>MODO ENSAIO: nada foi gravado.</strong>" : "")}</p>");
        string Q(int n, string singular, string plural) => H(Formatar.Qtd(n, singular, plural));
        sb.Append($"<p>Neste lote a plataforma {feito} sozinha <strong>{Q(op.Sozinho, "ação", "ações")}</strong>; " +
                  $"<strong>{Q(op.AguardandoAprovacao, "chamado aguarda", "chamados aguardam")}</strong> aprovação de uma pessoa; " +
                  $"{Q(op.Contar(ResultadoItem.Sugerido), "ficou como sugestão", "ficaram como sugestão")}; " +
                  $"{Q(op.Contar(ResultadoItem.FechouSozinho), "chamado fechou sozinho", "chamados fecharam sozinhos")} porque o problema sumiu; " +
                  $"{Q(op.Contar(ResultadoItem.Reaberto), "reabriu", "reabriram")}; " +
                  $"{Q(op.Contar(ResultadoItem.JaExistia), "já existia e não foi duplicado", "já existiam e não foram duplicados")}." +
                  $"{(op.LoteValidado ? "" : " <strong>O lote não passou nas verificações: nada foi feito sozinho.</strong>")}</p>");
        sb.Append("<p class=\"meta\">Travas que valem em qualquer nível: alto impacto ou ação sem volta sempre esperam uma pessoa; servidores e equipamentos de rede sempre têm uma pessoa no circuito; " +
                  $"no máximo {op.LimitePorDia} ações automáticas por dia; lote não validado não dispara nada sozinho. A verificação V13 (anexo H) confere isso por outro caminho.</p>");
        var itens = op.Itens.Where(i => i.Resultado != ResultadoItem.JaExistia || i.Estado == EstadoChamado.AguardandoAprovacao)
            .OrderByDescending(i => i.AgiuSozinho).ThenBy(i => i.Estado != EstadoChamado.AguardandoAprovacao).Take(MaximoItensAutomacao).ToList();
        if (itens.Count > 0)
        {
            sb.Append("<table><thead><tr><th>Nº</th><th>Chamado</th><th>Resultado</th><th>Por quê (regra, evidência e confiança)</th></tr></thead><tbody>");
            foreach (var i in itens)
                sb.Append($"<tr><td class=\"id\"><code>{H(i.Numero ?? "—")}</code></td><td>{H(i.Titulo)}</td><td>{H(i.Resultado.Nome(op.Ensaio))}</td>" +
                          $"<td>{H(i.Motivo)} <span class=\"meta\">Regra {H(i.PlaybookId)} · {H(Formatar.Qtd(i.Evidencias, "evento", "eventos"))} · confiança {H(i.Confianca)}</span></td></tr>");
            sb.Append("</tbody></table>");
        }
        sb.Append("<p class=\"meta\">Aprovar, rejeitar e desfazer: tela Automação da plataforma. Cada decisão fica no histórico do chamado e na auditoria.</p></section>");
    }

    // ================= 6. Limites =================

    private void SecaoLimites(StringBuilder sb, Lote lote)
    {
        var itens = new List<string>(lote.FontesAusentes.Concat(lote.AvisosAmbiente));
        if (lote.Avaliacao is { } a)
            foreach (var f in a.Score.Fatores.Where(f => f.Valor is null))
                itens.Add($"\"{f.Nome}\" não foi medido: {f.Explicacao.Replace("Não medido: ", "")}");
        foreach (var r in lote.RegrasAvaliadas.Where(r => r.MotivoSemOcorrencia?.Contains("0 em comum") == true))
            itens.Add("Não foi possível ligar os alertas do firewall às máquinas com falhas: os arquivos não têm uma informação em comum (como o nome da máquina atacada).");
        if (lote.Lacunas.Any(l => l.Motivo.Contains("cortada")))
            itens.Add("Alguns arquivos têm exatamente 10.000 linhas, o que pode indicar que a exportação foi cortada: pode haver mais problemas do que os mostrados.");
        itens.Add(regras.Ativos.Premissa);
        itens.Add("Os dados não informam cliente ou ambiente: cada arquivo foi tratado como um ambiente. Datas sem fuso foram consideradas no horário de Brasília.");

        sb.Append($"<section><h2>{Ic("limite")}6. O que esta análise não consegue dizer</h2><ul>");
        foreach (var item in itens)
            sb.Append($"<li>{H(item)}</li>");
        sb.Append($"</ul><p class=\"meta\">Detalhes: {Formatar.Numero(lote.Lacunas.Count)} lacuna(s) listadas no anexo E.</p></section>");
    }

    // ================= Anexos =================

    private static void AnexoCorrelacoes(StringBuilder sb, Lote lote)
    {
        var lista = Agrupamentos.CorrelacoesMaisGraves(lote.Correlacoes).Take(MaximoCorrelacoesListadas).ToList();
        sb.Append($"""
            <section>
            <h2>A. Correlações</h2>
            <p class="meta">As {lista.Count} mais graves de {Formatar.Numero(lote.Correlacoes.Count)}. Cada uma vem de uma regra declarada (anexo F).</p>
            """);
        if (lista.Count > 0)
        {
            sb.Append("<table><thead><tr><th>Id</th><th>Severidade</th><th>O que foi encontrado</th><th>Por que importa</th><th class=\"num\">Máquinas</th><th class=\"num\">Eventos</th><th>Frameworks</th></tr></thead><tbody>");
            foreach (var c in lista)
                sb.Append($"<tr><td class=\"id\"><code>{H(c.Id)}</code></td><td>{Selo(c.Severidade)}</td><td>{H(c.Titulo)}</td>" +
                          $"<td>{H(c.Justificativa)}</td><td class=\"num\">{Formatar.Numero(c.Ativos.Count)}</td>" +
                          $"<td class=\"num\">{Formatar.Numero(c.EventoIds.Count)}</td><td class=\"pequeno\">{H(Agrupamentos.Frameworks(c.Frameworks))}</td></tr>");
            sb.Append("</tbody></table>");
        }
        sb.Append("""
            <h3>Resultado de cada regra (inclusive as que não encontraram nada)</h3>
            <table><thead><tr><th>Regra</th><th class="num">Ocorrências</th><th>Observação</th></tr></thead><tbody>
            """);
        foreach (var r in lote.RegrasAvaliadas)
            sb.Append($"<tr><td><code>{H(r.RegraId)}</code><br>{H(r.Nome)}</td><td class=\"num\">{Formatar.Numero(r.Ocorrencias)}</td><td>{H(r.MotivoSemOcorrencia ?? "")}</td></tr>");
        sb.Append("</tbody></table></section>");
    }

    private void AnexoMaquinasEEventos(StringBuilder sb, Lote lote)
    {
        var maquinas = lote.Eventos
            .Where(e => e.Tipo == TiposEvento.Vulnerabilidade && e.Severidade >= Severidade.Alta && e.Ativo != ContextoConversao.NaoInformado)
            .GroupBy(e => e.Ativo, StringComparer.Ordinal)
            .Select(g => (Ativo: g.Key, Tipo: regras.Ativos.Tipo(g.First().TipoAtivo).Singular, Falhas: g.Count(),
                          Pontos: _calculadora.PontosDaMaquina(g) * regras.Ativos.Tipo(g.First().TipoAtivo).PesoNoScore))
            .OrderByDescending(m => m.Pontos).ThenByDescending(m => m.Falhas).ThenBy(m => m.Ativo, StringComparer.Ordinal)
            .Take(MaximoMaquinasListadas).ToList();

        sb.Append("<section><h2>B. Máquinas mais vulneráveis e eventos mais graves</h2>");
        if (maquinas.Count > 0)
        {
            sb.Append("<table><thead><tr><th>Máquina</th><th>Tipo</th><th class=\"num\">Falhas graves</th><th class=\"num\">Pontos de risco</th></tr></thead><tbody>");
            foreach (var m in maquinas)
                sb.Append($"<tr><td>{H(m.Ativo)}</td><td>{H(m.Tipo)}</td><td class=\"num\">{Formatar.Numero(m.Falhas)}</td><td class=\"num\">{m.Pontos:0}</td></tr>");
            sb.Append("</tbody></table><p class=\"meta\">Pontos de risco: nota da pior falha + pontos por falha a mais (até 100), × peso do tipo (servidor pesa mais).</p>");
        }

        var maisGraves = Agrupamentos.MaisGraves(lote.Eventos).Take(MaximoEventosListados).ToList();
        sb.Append($"<h3>Eventos mais graves</h3><p class=\"meta\">Os {maisGraves.Count} primeiros de {Formatar.Numero(lote.Eventos.Count)}. \"Origem\" é o arquivo e a linha de onde o evento saiu.</p>");
        if (maisGraves.Count > 0)
        {
            sb.Append("<table><thead><tr><th>Id</th><th>Severidade</th><th>Máquina</th><th>Evento</th><th>Por que esta severidade</th><th>Data</th><th>Origem</th></tr></thead><tbody>");
            foreach (var e in maisGraves)
                sb.Append($"<tr><td class=\"id\"><code>{H(e.Id)}</code></td><td>{Selo(e.Severidade)}</td><td>{H(e.Ativo)}</td><td>{H(Curto(e.Descricao))}</td>" +
                          $"<td class=\"pequeno\">{H(e.Justificativa)}</td><td>{H(Formatar.DataHora(e.DataUtc, fusoExibicao))}</td>" +
                          $"<td class=\"origem\"><code>{H(e.Evidencia.Arquivo)}:{e.Evidencia.Linha}</code></td></tr>");
            sb.Append("</tbody></table>");
        }
        sb.Append("</section>");
    }

    private static void AnexoComoDecidimos(StringBuilder sb, Lote lote)
    {
        var transicoes = Agrupamentos.Transicoes(lote.Eventos).Take(20).ToList();
        sb.Append("""
            <section>
            <h2>C. Como a severidade de cada evento foi decidida</h2>
            <p>A plataforma não copia a severidade da fonte: usa a nota técnica (CVSS das vulnerabilidades ou nível dos alertas).</p>
            """);
        if (transicoes.Count == 0)
        {
            sb.Append("<p>Sem eventos.</p></section>");
            return;
        }
        sb.Append("<table><thead><tr><th>Fonte</th><th>A fonte informou</th>");
        foreach (var s in SeveridadeExtensoes.DoMaisGrave)
            sb.Append($"<th class=\"num\">{Selo(s)}</th>");
        sb.Append("<th class=\"num\">Sem</th><th class=\"num\">Total</th></tr></thead><tbody>");
        foreach (var t in transicoes)
        {
            sb.Append($"<tr><td>{H(t.Fonte)}</td><td>{H(t.Origem)}</td>");
            foreach (var s in SeveridadeExtensoes.DoMaisGrave)
                sb.Append($"<td class=\"num\">{Formatar.Numero(t.PorSeveridade[s])}</td>");
            sb.Append($"<td class=\"num\">{Formatar.Numero(t.SemSeveridade)}</td><td class=\"num\">{Formatar.Numero(t.Total)}</td></tr>");
        }
        sb.Append("</tbody></table></section>");
    }

    private static void AnexoArquivos(StringBuilder sb, Lote lote)
    {
        sb.Append("""
            <section>
            <h2>D. Fontes recebidas</h2>
            <table><thead><tr><th>Arquivo</th><th>Fonte reconhecida</th><th class="num">Tamanho</th>
            <th class="num">Lidos</th><th class="num">Rejeitados</th><th class="num">Repetidos</th><th class="num">Lacunas</th><th>SHA-256 (início)</th></tr></thead><tbody>
            """);
        foreach (var a in lote.Arquivos)
        {
            var fonte = a.Fonte is not null ? H(a.Fonte) : $"<strong>Não processado:</strong> {H(a.MotivoNaoProcessado)}";
            var sha = a.Sha256 is null ? "—" : $"<code>{H(a.Sha256[..16])}…</code>";
            sb.Append($"<tr><td>{H(a.Nome)}</td><td>{fonte}</td><td class=\"num\">{H(Formatar.Bytes(a.TamanhoBytes))}</td>" +
                      $"<td class=\"num\">{Formatar.Numero(a.RegistrosLidos)}</td><td class=\"num\">{Formatar.Numero(a.RegistrosRejeitados)}</td><td class=\"num\">{Formatar.Numero(a.RegistrosRepetidos)}</td>" +
                      $"<td class=\"num\">{Formatar.Numero(a.Lacunas)}</td><td>{sha}</td></tr>");
        }
        sb.Append("</tbody></table>");
        var comAvisos = lote.Arquivos.Where(a => a.Avisos.Count > 0).ToList();
        if (comAvisos.Count > 0)
        {
            sb.Append("<h3>Ajustes feitos na leitura</h3><ul>");
            foreach (var a in comAvisos)
                foreach (var aviso in a.Avisos)
                    sb.Append($"<li><strong>{H(a.Nome)}:</strong> {H(aviso)}</li>");
            sb.Append("</ul>");
        }
        sb.Append("</section>");
    }

    private static void AnexoLacunas(StringBuilder sb, Lote lote)
    {
        var grupos = Agrupamentos.Lacunas(lote.Lacunas);
        sb.Append($"""
            <section>
            <h2>E. Lacunas detalhadas</h2>
            <p>O que faltou, veio inválido ou limita a análise. Nada foi descartado em silêncio: {Formatar.Numero(lote.Lacunas.Count)} lacuna(s), agrupadas por motivo.</p>
            """);
        if (grupos.Count > 0)
        {
            sb.Append("<table><thead><tr><th>Arquivo</th><th>Campo</th><th>Motivo</th><th class=\"num\">Ocorrências</th><th>Linhas (exemplos)</th></tr></thead><tbody>");
            foreach (var g in grupos.Take(MaximoGruposDeLacunas))
            {
                var linhas = g.LinhasExemplo.Count == 0 ? "—" : string.Join(", ", g.LinhasExemplo);
                sb.Append($"<tr><td>{H(g.Arquivo)}</td><td>{H(g.Campo)}</td><td>{H(g.Motivo)}</td><td class=\"num\">{Formatar.Numero(g.Quantidade)}</td><td>{H(linhas)}</td></tr>");
            }
            sb.Append("</tbody></table>");
            if (grupos.Count > MaximoGruposDeLacunas)
                sb.Append($"<p class=\"meta\">… e mais {grupos.Count - MaximoGruposDeLacunas} tipo(s) de lacuna, disponíveis na plataforma.</p>");
        }
        sb.Append("</section>");
    }

    /// <summary>
    /// Anexo: técnicas do MITRE ATT&amp;CK ligadas aos eventos deste lote. Cada linha diz quantos eventos a técnica
    /// explica e QUANTO a plataforma confia nisso — os dados não afirmam a técnica, então o grau de confiança
    /// anda junto. O que não foi possível apontar aparece como lacuna, não como zero.
    /// </summary>
    private void AnexoMitre(StringBuilder sb, Lote lote)
    {
        var mapa = new MapaMitre(regras.Mitre);
        var porTecnica = new Dictionary<string, (int Eventos, string Confianca)>();
        var semTecnica = 0;
        foreach (var e in lote.Eventos)
        {
            var apontadas = mapa.Apontar(e);
            if (apontadas.Count == 0) { semTecnica++; continue; }
            foreach (var t in apontadas)
            {
                var atual = porTecnica.GetValueOrDefault(t.Tecnica, (Eventos: 0, Confianca: t.Confianca));
                porTecnica[t.Tecnica] = (atual.Eventos + 1, MaisForte(atual.Confianca, t.Confianca));
            }
        }

        sb.Append($"<section><h2>{Ic("alvo")}Anexo — Técnicas de ataque (MITRE ATT&amp;CK)</h2>");
        sb.Append($"""
            <p>O MITRE ATT&CK é um catálogo público das técnicas que atacantes usam. Ele serve para falar a mesma língua
            que o resto do mercado. <strong>Atenção ao que esta tabela é e ao que ela não é:</strong> as fontes deste lote
            não dizem qual técnica foi usada — não trazem exposição à internet nem exploração confirmada. A plataforma
            <em>deduz</em> a técnica (pelo texto do alerta ou pelo tipo do problema) e declara o quanto confia em cada dedução.
            Nenhuma linha abaixo é um ataque observado.</p>
            """);

        if (porTecnica.Count == 0)
        {
            sb.Append("<p class=\"vazio\">Nenhuma técnica pôde ser apontada neste lote.</p></section>");
            return;
        }

        sb.Append("<table><thead><tr><th>Técnica</th><th>Fase do ataque</th><th>O que é</th><th>Eventos</th><th>Confiança</th><th>Como reduzir</th></tr></thead><tbody>");
        foreach (var (codigo, dados) in porTecnica.OrderByDescending(p => p.Value.Eventos).ThenBy(p => p.Key, StringComparer.Ordinal))
        {
            var ficha = mapa.Tecnica(codigo);
            var reduzir = string.Join("<br>", (ficha?.Mitigacoes ?? []).Select(m => H(regras.Mitre.Mitigacoes.GetValueOrDefault(m, m))));
            sb.Append($"<tr><td><code>{H(codigo)}</code><br>{H(ficha?.Nome ?? "")}</td><td class=\"pequeno\">{H(ficha?.Tatica ?? "")}</td>" +
                      $"<td class=\"pequeno\">{H(ficha?.Explicacao ?? "")}</td><td>{Formatar.Numero(dados.Eventos)}</td>" +
                      $"<td class=\"pequeno\">{H(ConfiancaMitre.Descricao(dados.Confianca))}</td><td class=\"pequeno\">{reduzir}</td></tr>");
        }
        sb.Append("</tbody></table>");

        if (semTecnica > 0)
            sb.Append($"<p class=\"pequeno\">Lacuna declarada: {Formatar.Qtd(semTecnica, "evento", "eventos")} sem técnica identificada. " +
                      "O texto da fonte não permitiu apontar nenhuma técnica, e a plataforma prefere dizer isso a chutar.</p>");
        sb.Append("</section>");
    }

    /// <summary>Entre duas confianças, fica a mais forte (observada &gt; provável &gt; possível).</summary>
    private static string MaisForte(string a, string b)
    {
        int Peso(string c) => c == ConfiancaMitre.Observada ? 3 : c == ConfiancaMitre.Provavel ? 2 : 1;
        return Peso(a) >= Peso(b) ? a : b;
    }

    private void AnexoMetodologia(StringBuilder sb)
    {
        var s = regras.Severidade;
        var sc = regras.Score;
        var pl = regras.Plano;
        sb.Append($"""
            <section>
            <h2>F. Metodologia e premissas</h2>
            <h3>Severidade de cada evento</h3>
            <p>{H(s.Descricao)}</p>
            <table><thead><tr><th>Severidade</th><th>Nota CVSS ({H(s.Cvss.Fonte)})</th><th>Nível do alerta ({H(s.NivelAlerta.Fonte)})</th><th>Textos aceitos da fonte</th></tr></thead><tbody>
            """);
        foreach (var severidade in SeveridadeExtensoes.DoMaisGrave)
        {
            var cvss = string.Join("; ", s.Cvss.Faixas.Where(f => f.Severidade == severidade).Select(f => f.Descricao));
            var nivel = string.Join("; ", s.NivelAlerta.Faixas.Where(f => f.Severidade == severidade).Select(f => f.Descricao));
            sb.Append($"<tr><td>{Selo(severidade)}</td><td>{H(cvss)}</td><td>{H(nivel)}</td><td class=\"pequeno\">{H(string.Join(", ", s.Textos[severidade]))}</td></tr>");
        }
        sb.Append("</tbody></table>");

        sb.Append($"""
            <h3>Score ({H(sc.Direcao)})</h3>
            <ul>
              <li>Risco de cada máquina: nota da pior falha (crítica {sc.NotaPelaPiorFalha[Severidade.Critica]:0}, alta {sc.NotaPelaPiorFalha[Severidade.Alta]:0}, média {sc.NotaPelaPiorFalha[Severidade.Media]:0}, baixa {sc.NotaPelaPiorFalha[Severidade.Baixa]:0}) + {sc.PontosPorFalhaAdicional:0} por falha a mais; máximo {sc.TetoPorMaquina:0}. Uma única falha crítica conhecida já basta para alguém entrar.</li>
              <li>Peso de cada alerta: crítico {sc.PesoPorAlerta[Severidade.Critica]:0.0}, alto {sc.PesoPorAlerta[Severidade.Alta]:0.0}, médio {sc.PesoPorAlerta[Severidade.Media]:0.0}, baixo {sc.PesoPorAlerta[Severidade.Baixa]:0.0}.</li>
              <li>Pesos dos fatores: {H(string.Join("; ", sc.Fatores.Select(f => $"{f.Nome} {f.Peso:0}")))}.</li>
              <li>Peso de cada tipo de máquina: {H(string.Join("; ", regras.Ativos.Tipos.Values.Select(t => $"{t.Singular} {t.PesoNoScore:0}")))}.</li>
            </ul>
            <h3>Tipo de cada máquina</h3>
            <p>{H(regras.Ativos.Premissa)}</p>
            <h3>Plano de ação</h3>
            <p>{H(pl.Descricao)} Prazo pela falha mais grave eliminada: crítica {pl.PrazoDias[Severidade.Critica]} dias, alta {pl.PrazoDias[Severidade.Alta]},
            média {pl.PrazoDias[Severidade.Media]}, baixa {pl.PrazoDias[Severidade.Baixa]}. Esforço pelo número de atualizações:
            baixo até {pl.Esforco.BaixoAteAtualizacoes}, médio até {pl.Esforco.MedioAteAtualizacoes}, alto acima disso.
            Impacto alto a partir de {pl.ImpactoAltoAPartirDePercentual:0}% das falhas graves, médio a partir de {pl.ImpactoMedioAPartirDePercentual:0}%.</p>
            """);

        sb.Append("<h3>Regras de correlação</h3><table><thead><tr><th>Regra</th><th>Parâmetros</th><th>Frameworks</th></tr></thead><tbody>");
        foreach (var r in regras.Correlacoes)
        {
            var parametros = string.Join(", ", r.Parametros.Select(p => $"{p.Key} = {p.Value}"));
            sb.Append($"<tr><td><code>{H(r.Id)}</code><br>{H(r.Nome)}</td><td class=\"pequeno\">{H(parametros)}</td><td class=\"pequeno\">{H(Agrupamentos.Frameworks(r.Frameworks))}</td></tr>");
        }
        sb.Append("</tbody></table>");

        sb.Append("<h3>Frameworks por tipo de evento</h3><table><thead><tr><th>Tipo</th><th>Controles</th><th>Observação</th></tr></thead><tbody>");
        foreach (var (tipo, f) in regras.FrameworksPorTipo.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var controles = string.Join("<br>", f.Nist.Concat(f.Cis).Concat(f.Iso).Concat(f.Mitre)
                .Select(c => $"<code>{H(c)}</code> {H(regras.NomesControles.GetValueOrDefault(c, ""))}"));
            sb.Append($"<tr><td>{H(tipo)}</td><td class=\"pequeno\">{controles}</td><td class=\"pequeno\">{H(f.Observacao ?? "")}</td></tr>");
        }
        sb.Append("""
            </tbody></table>
            <h3>Privacidade (LGPD)</h3>
            <p>Nomes de usuário, quando existem, são trocados na entrada por um apelido calculado com HMAC-SHA256 e uma chave secreta.
            O nome original não é guardado.</p>
            </section>
            """);
    }

    private static readonly (string Termo, string Explicacao)[] Glossario =
    [
        ("Alerta", "aviso automático de que algo suspeito aconteceu."),
        ("CVE", "código público de uma falha conhecida (ex.: CVE-2024-1234), como um “RG” da falha."),
        ("CVSS", "nota de 0 a 10 da gravidade técnica de uma falha; não considera o contexto da empresa."),
        ("Correlação", "relação entre eventos que, juntos, mostram um problema maior do que cada um sozinho."),
        ("Falha (vulnerabilidade)", "defeito num programa que pode ser usado para invadir ou atrapalhar o funcionamento."),
        ("Firewall", "equipamento que controla o que entra e sai da rede da empresa."),
        ("Lacuna", "dado que faltou ou veio errado; é declarado em vez de escondido."),
        ("NIST, CIS, ISO 27001", "guias reconhecidos de boas práticas de segurança; os códigos (ex.: ID.RA-01) indicam o item do guia."),
        ("MITRE ATT&CK", "catálogo público das técnicas usadas por atacantes."),
        ("Phishing", "e-mail ou mensagem falsa feita para enganar a pessoa e roubar dados ou instalar programas maliciosos."),
        ("Score de risco", "nota de 0 a 100 que resume o risco; 100 é o pior."),
        ("Servidor", "computador que sustenta sistemas usados por muitas pessoas."),
        ("SHA-256", "“impressão digital” de um arquivo: prova qual arquivo gerou o relatório."),
        ("SIEM", "sistema que junta os registros (logs) de várias ferramentas e gera alertas."),
    ];

    private static void AnexoGlossario(StringBuilder sb, string titulo)
    {
        sb.Append($"<section><h2>{H(titulo)}</h2><dl class=\"glossario\">");
        foreach (var (termo, explicacao) in Glossario)
            sb.Append($"<dt>{H(termo)}</dt><dd>{H(explicacao)}</dd>");
        sb.Append("</dl></section>");
    }

    // ================= apoio =================

    /// <summary>
    /// Com confiança baixa, o aviso fica junto do número: "risco baixo" medido em 20% não é "ambiente seguro".
    /// Nível "baixa" = o menor nível de confiança definido em regras/score.json.
    /// </summary>
    public string? AvisoConfianca(ScoreRisco s)
    {
        var menor = regras.Score.Confianca.OrderBy(c => c.MinimoPercentualMedido).First().Nome;
        if (s.Valor is null || s.Confianca != menor)
            return null;
        var conclusao = s.Valor < 40
            ? "Não dá para afirmar que o ambiente está seguro."
            : "O risco real pode ser maior ou menor.";
        var medidos = s.Fatores.Count(f => f.Valor is not null);
        var faltam = s.Fatores.Where(f => f.Valor is null).Select(f => f.Nome.ToLowerInvariant()).ToList();
        var lista = faltam.Count == 1 ? faltam[0] : string.Join(", ", faltam[..^1]) + " e " + faltam[^1];
        var verbo = medidos == 1 ? "pôde ser avaliado" : "puderam ser avaliados";
        return $"Atenção: só {medidos} de {s.Fatores.Count} fatores {verbo} ({s.PercentualMedido:0}%). " +
               $"Faltam dados de: {lista}. {conclusao}";
    }

    /// <summary>Abaixo disso, os maiores achados explicam pouco: o risco está espalhado e não há atalho.</summary>
    public const double PercentualRiscoConcentrado = 30;

    /// <summary>O risco está concentrado em poucos achados? (decide o título e o tom da frase)</summary>
    public static bool RiscoConcentrado(Avaliacao a) => a.PercentualExplicado >= PercentualRiscoConcentrado;

    /// <summary>Título da parte "poucos problemas concentram o risco" (ou "o risco está espalhado").</summary>
    public static string TituloAchados(Avaliacao a) =>
        RiscoConcentrado(a) ? "Poucos problemas concentram boa parte do risco" : "O risco está espalhado";

    /// <summary>
    /// Em linguagem de diretoria, com a faixa (alto, moderado…) e não só o número:
    /// "Três problemas respondem por mais da metade do risco (58%). Resolvendo só eles, o risco cairia de alto para moderado (de 74 para 31)."
    /// Quando explicam pouco, diz isso com todas as letras e aponta o caminho, em vez de sugerir um atalho que não existe.
    /// Null quando não se aplica.
    /// </summary>
    public string? FraseAchados(Avaliacao a)
    {
        if (a.AchadosPrincipais.Count == 0 || a.PercentualExplicado is not { } pct || a.Score.Valor is not { } atual || a.ScoreSemAchados is not { } sem)
            return null;
        var n = a.AchadosPrincipais.Count;
        var eles = n == 1 ? "ele" : "eles";
        var antes = regras.Score.FaixaDe(atual).Nome.ToLowerInvariant();
        var depois = regras.Score.FaixaDe(sem).Nome.ToLowerInvariant();
        var numeros = $"de {Inteiro(atual)} para {Inteiro(sem)}";

        if (RiscoConcentrado(a))
        {
            var quantos = n switch { 1 => "Um problema responde", 2 => "Dois problemas respondem", _ => "Três problemas respondem" };
            var parte = pct >= 99.5 ? "por todo o risco" : pct > 50 ? $"por mais da metade do risco ({Inteiro(pct)}%)" : $"por {Inteiro(pct)}% do risco";
            var efeito = antes == depois
                ? $"o risco cairia {numeros}, mas continuaria {antes}"
                : $"o risco cairia de {antes} para {depois} ({numeros})";
            return $"{quantos} {parte}. Resolvendo só {eles}, {efeito}.";
        }

        var maiores = n == 1 ? "o maior" : $"os {n} maiores";
        var pouco = antes == depois
            ? $"o risco continuaria {antes} ({numeros})"
            : $"o risco cairia pouco: de {antes} para {depois} ({numeros})";
        return $"Não existe um problema único para atacar. Mesmo resolvendo {maiores}, {pouco}. " +
               "O caminho é atualizar todas as máquinas de forma automática, e não corrigir uma por uma.";
    }

    /// <summary>Ícones do relatório: SVG desenhado pela equipe, dentro do próprio arquivo (nada vem da internet).</summary>
    private const string Sprite = """
        <svg class="sprite" xmlns="http://www.w3.org/2000/svg" aria-hidden="true" focusable="false">
        <symbol id="ic-resumo" viewBox="0 0 24 24"><rect x="5" y="3" width="14" height="18" rx="2" fill="none" stroke="currentColor" stroke-width="2"/><path d="M9 8h6M9 12h6M9 16h3" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></symbol>
        <symbol id="ic-negocio" viewBox="0 0 24 24"><path d="M3 21h18M5 21V7l7-4 7 4v14" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/><path d="M10 21v-5h4v5" fill="none" stroke="currentColor" stroke-width="2"/></symbol>
        <symbol id="ic-score" viewBox="0 0 24 24"><path d="M4 18a8 8 0 1 1 16 0" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"/><path d="m12 18 5-6" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></symbol>
        <symbol id="ic-plano" viewBox="0 0 24 24"><path d="M4 7h16M4 12h16M4 17h10" stroke="currentColor" stroke-width="2" stroke-linecap="round"/><path d="m17 17 2 2 3-4" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></symbol>
        <symbol id="ic-robo" viewBox="0 0 24 24"><rect x="4" y="8" width="16" height="12" rx="3" fill="none" stroke="currentColor" stroke-width="2"/><path d="M12 4v4M9 13h.01M15 13h.01M9 17h6" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></symbol>
        <symbol id="ic-limite" viewBox="0 0 24 24"><path d="M12 3 2 20h20L12 3Z" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/><path d="M12 10v4M12 17h.01" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></symbol>
        <symbol id="ic-anexo" viewBox="0 0 24 24"><path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7Z" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/></symbol>
        <symbol id="ic-alvo" viewBox="0 0 24 24"><circle cx="12" cy="12" r="8" fill="none" stroke="currentColor" stroke-width="2"/><circle cx="12" cy="12" r="3.5" fill="none" stroke="currentColor" stroke-width="2"/></symbol>
        </svg>
        """;

    private static string Ic(string nome) => $"<svg class=\"ic\" aria-hidden=\"true\"><use href=\"#ic-{nome}\"/></svg>";

    private static string Pontos(double valor) => valor < 1
        ? $"{valor.ToString("0.0", Formatar.PtBr)} ponto"
        : $"{Inteiro(valor)} {(Inteiro(valor) == "1" ? "ponto" : "pontos")}";

    /// <summary>Porcentagem para leitura: "18%" ou "1,4%" quando é menos de 10%.</summary>
    private static string Pct(double valor) => valor < 10
        ? valor.ToString("0.#", Formatar.PtBr) + "%"
        : valor.ToString("0", Formatar.PtBr) + "%";

    private static string Inteiro(double valor) => Math.Round(valor, MidpointRounding.AwayFromZero).ToString("0", Formatar.PtBr);

    private static string NomeNivel(Nivel nivel) => nivel switch { Nivel.Alto => "alto", Nivel.Medio => "médio", _ => "baixo" };

    private static string ClasseFaixa(string faixa) => Padronizacao.Texto.NormalizarChave(faixa) switch
    {
        "critico" => "faixa-critica",
        "alto" => "faixa-alta",
        "elevado" => "faixa-media",
        "moderado" or "baixo" => "faixa-baixa",
        _ => "faixa-nenhuma",
    };

    private static string ClasseSeveridade(Severidade severidade) => ((Severidade?)severidade).ClasseCss();

    /// <summary>Severidade sempre com ícone + texto (legível em preto e branco) + cor.</summary>
    private static string Selo(Severidade? severidade) =>
        $"<span class=\"sev {severidade.ClasseCss()}\"><span aria-hidden=\"true\">{severidade.Simbolo()}</span> {H(severidade.Nome())}</span>";

    private static string Curto(string? texto) =>
        texto is null ? "" : texto.Length <= TamanhoMaximoTexto ? texto : texto[..TamanhoMaximoTexto].TrimEnd() + "…";

    // Escapa o que é perigoso em HTML (< > & aspas) e mantém acentos legíveis
    // (o WebUtility.HtmlEncode trocaria "à" por "&#224;").
    private static readonly HtmlEncoder Codificador = HtmlEncoder.Create(UnicodeRanges.All);

    // Todo texto passa pelo filtro de credenciais ANTES do escape (S1: nunca no relatório, nem parcialmente).
    private static string H(string? texto) => Codificador.Encode(Validacao.FiltroCredenciais.Mascarar(texto ?? ""));

    private const string Estilo = """
        :root{--tinta:#1b2430;--suave:#5b6675;--linha:#d5dbe3;--cabecalho:#eef1f5;--fundo-claro:#f5f7fa;
        --critica:#b42318;--alta:#c4320a;--media:#8a5a00;--baixa:#2f6b3a;--nenhuma:#5b6675}
        *{box-sizing:border-box}
        body{font-family:"Segoe UI",Arial,sans-serif;color:var(--tinta);background:#fff;margin:0 auto;max-width:1000px;padding:32px;line-height:1.45;font-size:14px}
        .selo{display:inline-block;border:2px solid var(--tinta);padding:4px 10px;font-weight:700;letter-spacing:.04em;font-size:12px;margin:0}
        h1{font-size:28px;margin:16px 0 4px}
        h2{font-size:20px;border-bottom:2px solid var(--tinta);padding-bottom:4px;margin-top:28px}
        h3{font-size:15px;margin:20px 0 8px}
        table{width:100%;border-collapse:collapse;margin:8px 0}
        th,td{border:1px solid var(--linha);padding:6px 8px;text-align:left;vertical-align:top}
        th{background:var(--cabecalho)}
        .num{text-align:right;font-variant-numeric:tabular-nums}
        .meta{color:var(--suave)}
        .pequeno{font-size:12px}
        .sev{font-weight:700;white-space:nowrap}
        .sev-critica{color:var(--critica)}.sev-alta{color:var(--alta)}.sev-media{color:var(--media)}.sev-baixa{color:var(--baixa)}.sev-nenhuma{color:var(--nenhuma)}
        .diretoria{font-size:13px}.diretoria h2{margin-top:16px}.diretoria h3{margin:12px 0 6px}.diretoria td,.diretoria th{padding:4px 6px}
        .score-caixa{display:flex;gap:20px;align-items:center;border:3px solid var(--tinta);border-left-width:12px;border-radius:8px;padding:10px 16px;margin:8px 0}
        .faixa-critica{border-left-color:var(--critica)}.faixa-alta{border-left-color:var(--alta)}.faixa-media{border-left-color:var(--media)}.faixa-baixa{border-left-color:var(--baixa)}
        .score-numero{white-space:nowrap}.grande{font-size:56px;font-weight:800;line-height:1}.de{font-size:16px;color:var(--suave)}
        .score-texto p{margin:3px 0}.rotulo{font-size:15px}.frase{font-size:16px;font-weight:600}
        .sprite{position:absolute;width:0;height:0;overflow:hidden}
        .ic{width:17px;height:17px;flex:none;color:var(--suave)}
        h2{display:flex;align-items:center;gap:8px}
        h2 .ic{width:20px;height:20px;color:var(--tinta)}
        h3{display:flex;align-items:center;gap:7px}
        .duas-colunas{display:grid;grid-template-columns:1fr 1fr;gap:10px;align-items:start;margin:8px 0}
        .duas-colunas .quadro{border:1px solid var(--linha);border-radius:6px;padding:10px 12px}
        .duas-colunas h3{margin:0 0 8px;font-size:13px;text-transform:uppercase;letter-spacing:.05em;color:var(--suave);border-bottom:1px solid var(--linha);padding-bottom:5px}
        .duas-colunas .riscos{grid-template-columns:1fr}
        .passos{margin:0;padding-left:20px}
        .passos li{margin:0 0 9px}
        .passos strong{display:block;font-size:13.5px;line-height:1.25}
        .passo-meta{display:block;font-size:11px;color:var(--suave);margin:1px 0 2px}
        .passo-efeito{display:block;font-size:12px}
        @media(max-width:720px){.duas-colunas{grid-template-columns:1fr}}
        .kpis{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:8px;margin:10px 0}
        .kpi{border:1px solid var(--linha);border-top:4px solid var(--tinta);border-radius:6px;padding:8px 10px}
        .kpi-perda{border-top-color:var(--critica)}.kpi-custo{border-top-color:var(--media)}
        .kpi-score{border-top-color:var(--baixa)}.kpi-tendencia{border-top-color:var(--suave)}
        .kpi-rotulo{display:block;font-size:10.5px;text-transform:uppercase;letter-spacing:.06em;color:var(--suave)}
        .kpi strong{display:block;font-size:19px;line-height:1.15;margin:3px 0 2px}
        .kpi-nota{display:block;font-size:11px;color:var(--suave);line-height:1.3}
        .riscos{display:grid;grid-template-columns:repeat(auto-fit,minmax(255px,1fr));gap:8px;align-items:stretch}
        .risco{display:flex;flex-direction:column;border:1px solid var(--linha);border-top:4px solid var(--suave);border-radius:6px;padding:9px 11px}
        .risco-topo{display:flex;align-items:center;gap:8px;margin:0 0 4px}
        .risco .ordem{display:inline-flex;align-items:center;justify-content:center;width:20px;height:20px;border-radius:50%;background:var(--tinta);color:#fff;font-size:11px;font-weight:700}
        .risco-titulo{font-size:14px;font-weight:700;margin:0 0 5px;line-height:1.25}
        .risco-frase{font-size:13px;margin:0 0 6px;line-height:1.35}
        .risco-detalhe{font-size:11.5px;color:var(--suave);margin:0 0 6px;line-height:1.3}
        .risco-acao{font-size:11.5px;margin:auto 0 0;padding-top:6px;border-top:1px solid var(--linha)}
        .sev-critica.risco{border-top-color:var(--critica)}.sev-alta.risco{border-top-color:var(--alta)}
        .sev-media.risco{border-top-color:var(--media)}.sev-baixa.risco{border-top-color:var(--baixa)}

        .risco{border:1px solid var(--linha);border-left:6px solid var(--nenhuma);border-radius:6px;padding:5px 10px;break-inside:avoid}

        .risco.sev-critica{border-left-color:var(--critica);color:inherit}.risco.sev-alta{border-left-color:var(--alta);color:inherit}
        .risco.sev-media{border-left-color:var(--media);color:inherit}.risco.sev-baixa{border-left-color:var(--baixa);color:inherit}
        .ordem{display:inline-block;min-width:20px;height:20px;border-radius:10px;background:var(--tinta);color:#fff;text-align:center;font-size:12px;line-height:20px;margin-right:4px}
        .selo-ok{display:inline-block;border:2px solid var(--baixa);color:var(--baixa);border-radius:6px;padding:3px 10px;font-weight:700;margin:6px 0 0}
        .nao-validado{border:3px solid var(--critica);color:var(--critica);border-radius:6px;padding:10px 14px;margin:10px 0;font-weight:700;font-size:15px}
        .aviso-forte{border:2px solid var(--alta);border-radius:6px;padding:6px 10px;margin:8px 0;font-weight:600}
        .uma-coisa{border:2px dashed var(--tinta);background:var(--fundo-claro);padding:8px 12px;margin:10px 0;font-size:14px}
        .barra{display:inline-block;width:120px;height:10px;border:1px solid var(--tinta);vertical-align:middle;margin-right:6px}
        .barra span{display:block;height:100%;background:var(--tinta)}
        .grafico{width:100%;border-collapse:collapse;margin:10px 0 6px}
        .grafico td{border:0;padding:5px 8px;vertical-align:middle}
        .grafico .barra-celula{width:45%}
        .grafico .barra{width:100%;max-width:none;height:14px}
        .grafico .num{text-align:right;white-space:nowrap}
        tr.atual td{font-weight:700;background:var(--fundo-claro)}
        .matriz td,.matriz th{text-align:center;width:33%}
        .glossario dt{font-weight:700;margin-top:6px}.glossario dd{margin:0 0 4px 16px}
        code{font-family:Consolas,monospace;font-size:12px;word-break:break-all}
        td.id code{white-space:nowrap;word-break:normal}
        td.origem{min-width:120px}td.origem code{word-break:normal;overflow-wrap:anywhere}
        footer{margin-top:40px;border-top:1px solid var(--linha);padding-top:8px;color:var(--suave);font-size:12px}
        @page{size:A4;margin:14mm}
        @media print{body{padding:0;max-width:none}h2,h3{break-after:avoid}tr{break-inside:avoid}.quebra{break-before:page}
          /* Página 1 numa folha só (regra do corredor): capa e resumo mais compactos só no papel. */
          h1{font-size:22px;margin:6px 0 2px}.capa .meta{font-size:11px;margin:2px 0}.selo-ok{margin:2px 0 0;padding:2px 8px}
          .diretoria{font-size:12px;line-height:1.35}.diretoria h2{font-size:17px;margin:10px 0 4px}.diretoria h3{font-size:13px;margin:8px 0 4px}
          .score-caixa{padding:6px 12px;margin:4px 0}.grande{font-size:48px}.rotulo{font-size:13px}.frase{font-size:14px}
          .duas-colunas{gap:8px}.duas-colunas .quadro{padding:8px 10px}.passos li{margin:0 0 7px}
          .kpis{gap:6px;margin:6px 0}.kpi{padding:6px 8px}.kpi strong{font-size:17px}
          .riscos{gap:6px}.risco{padding:7px 9px}.risco-titulo{font-size:13px}.risco-frase{font-size:12px}
          .aviso-forte{padding:4px 8px;margin:4px 0}.uma-coisa{font-size:12px;padding:5px 10px;margin:6px 0}}
        @media screen{
        body{max-width:1280px;padding:48px 56px 80px;font-size:17px;line-height:1.6}
        .selo{font-size:14px;padding:6px 14px}
        h1{font-size:38px;margin:20px 0 8px}
        h2{font-size:26px;margin:56px 0 18px;padding-bottom:8px}
        h3{font-size:20px;margin:32px 0 12px}
        table{margin:12px 0}
        th,td{padding:10px 14px}
        .pequeno{font-size:15px}
        code{font-size:14px}
        .diretoria{font-size:17px}.diretoria h2{margin-top:32px}.diretoria h3{margin:32px 0 14px}.diretoria td,.diretoria th{padding:10px 14px}
        .score-caixa{gap:40px;padding:24px 32px;margin:16px 0}
        .grande{font-size:84px}.de{font-size:20px}
        .score-texto p{margin:8px 0}.rotulo{font-size:19px}.frase{font-size:21px}
        .riscos{gap:16px}
        .risco{padding:14px 20px}.risco p{margin:6px 0}.titulo-risco{font-size:19px}
        .ordem{min-width:26px;height:26px;border-radius:13px;font-size:15px;line-height:26px;margin-right:8px}
        .selo-ok{font-size:16px;padding:6px 14px;margin:14px 0 0}
        .nao-validado{font-size:18px;padding:16px 20px;margin:18px 0}
        .aviso-forte{padding:12px 16px;margin:16px 0}
        .uma-coisa{font-size:18px;padding:16px 20px;margin:20px 0}
        .barra{width:160px;height:12px}
        .glossario dt{margin-top:12px}.glossario dd{margin:2px 0 8px 20px}
        footer{margin-top:64px;font-size:14px}
        }
        @media screen and (max-width:700px){body{padding:20px 16px 48px;font-size:16px}.score-caixa{flex-direction:column;align-items:flex-start;gap:12px}table{display:block;overflow-x:auto}}
        """;
}
