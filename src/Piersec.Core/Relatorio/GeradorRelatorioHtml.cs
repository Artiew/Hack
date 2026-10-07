using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Piersec.Core.Analise;
using Piersec.Core.Coleta;
using Piersec.Core.Modelo;

namespace Piersec.Core.Relatorio;

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

    public string Gerar(Lote lote)
    {
        var sb = new StringBuilder();
        sb.Append($"""
            <!doctype html>
            <html lang="pt-BR">
            <head>
            <meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Relatório de segurança — lote {H(lote.Id[..8])}</title>
            <style>{Estilo}</style>
            </head>
            <body>
            <header class="capa">
              <p class="selo">{H(SeloConfidencialidade)}</p>
              <h1>Relatório de segurança</h1>
              <p class="meta">Gerado automaticamente pela plataforma em {H(Formatar.DataHora(lote.CriadoEmUtc, fusoExibicao))} (horário de Brasília)
              · lote {H(lote.Id[..8])} · {lote.Arquivos.Count(a => a.Fonte is not null)} de {lote.Arquivos.Count} arquivo(s) analisado(s).
              A primeira página é para a diretoria; o detalhe técnico está nos anexos.</p>
            </header>
            """);

        if (lote.Avaliacao is { } avaliacao)
        {
            PaginaDiretoria(sb, lote, avaliacao);
            SecaoScore(sb, avaliacao.Score);
            SecaoPlano(sb, avaliacao);
        }
        SecaoLimites(sb, lote);

        sb.Append("<h2 class=\"quebra\">Anexos para a equipe técnica</h2>");
        AnexoCorrelacoes(sb, lote);
        AnexoMaquinasEEventos(sb, lote);
        AnexoComoDecidimos(sb, lote);
        AnexoArquivos(sb, lote);
        AnexoLacunas(sb, lote);
        AnexoMetodologia(sb);
        AnexoGlossario(sb);

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

    // ================= Página 1: para a diretoria =================

    private void PaginaDiretoria(StringBuilder sb, Lote lote, Avaliacao a)
    {
        var s = a.Score;
        var numero = s.Valor is { } v ? Inteiro(v) : "—";
        var comparacao = lote.Comparacao is { } c
            ? $"{(c.Diferenca > 0 ? "Piorou" : c.Diferenca < 0 ? "Melhorou" : "Igual")}: {Inteiro(Math.Abs(c.Diferenca))} ponto(s) em relação à análise de " +
              $"{Formatar.DataHora(c.DataAnterior, fusoExibicao)} ({Inteiro(c.ScoreAnterior)} de 100) — principal motivo: {c.Motivo}."
            : "Primeira análise: ainda não há uma anterior para comparar.";
        var avisoConfianca = AvisoConfianca(s) is { } aviso ? $"<p class=\"aviso-forte\">⚠ {H(aviso)}</p>" : "";

        sb.Append($"""
            <section class="diretoria">
            <h2>Resumo para a diretoria</h2>
            <div class="score-caixa {ClasseFaixa(s.Faixa)}">
              <div class="score-numero"><span class="grande">{numero}</span><span class="de"> de 100</span></div>
              <div class="score-texto">
                <p class="rotulo">COMO ESTAMOS: <strong>risco {H(s.Faixa.ToLowerInvariant())}</strong> <span class="meta">(0 = sem risco · 100 = risco máximo)</span></p>
                {avisoConfianca}
                <p class="frase">{H(a.FraseSituacao)}</p>
                <p>{H(s.FraseFaixa)}</p>
                <p class="meta">{H(comparacao)}<br>Confiança {H(s.Confianca)}: o score usa {s.PercentualMedido:0}% do que deveria medir ({s.Fatores.Count(f => f.Valor is not null)} de {s.Fatores.Count} fatores) — ver seção 2.</p>
              </div>
            </div>
            """);
        foreach (var avisoAmbiente in lote.AvisosAmbiente)
            sb.Append($"<p class=\"aviso-forte\">⚠ {H(avisoAmbiente)}</p>");

        sb.Append("<h3>O que é mais grave</h3><div class=\"riscos\">");
        var n = 1;
        foreach (var r in a.Riscos)
        {
            sb.Append($"""
                <div class="risco {ClasseSeveridade(r.Severidade)}">
                  <p class="titulo-risco"><span class="ordem">{n++}</span> {H(r.Titulo)} {Selo(r.Severidade)}</p>
                  <p><strong>O que é:</strong> {H(r.OQueE)}</p>
                  <p><strong>Por que importa:</strong> {H(r.PorQueImporta)}</p>
                  <p><strong>Como resolver:</strong> {H(r.ComoResolver)}</p>
                </div>
                """);
        }
        if (a.Riscos.Count == 0)
            sb.Append("<p>Nenhum risco relevante encontrado nas fontes analisadas.</p>");
        sb.Append("</div>");

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
            sb.Append("<h3>O que fazer agora (em ordem)</h3><table class=\"acoes\"><thead><tr><th>#</th><th>Ação</th><th>Resultado esperado</th><th>Esforço</th><th>Prazo</th></tr></thead><tbody>");
            foreach (var acao in principais)
                sb.Append($"<tr><td class=\"num\">{acao.Ordem}</td><td><strong>{H(acao.Titulo)}</strong><br><span class=\"meta\">{H(acao.Quadrante)}</span></td>" +
                          $"<td>{H(acao.Impacto)}</td><td>{H(NomeNivel(acao.Esforco))}</td><td>até {acao.PrazoDias} dias</td></tr>");
            sb.Append("</tbody></table>");
        }
        sb.Append("</section>");
    }

    // ================= 2. Score =================

    private void SecaoScore(StringBuilder sb, ScoreRisco s)
    {
        sb.Append($"""
            <section class="quebra">
            <h2>2. Entendendo o score</h2>
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

    private static void SecaoPlano(StringBuilder sb, Avaliacao a)
    {
        sb.Append("""
            <section>
            <h2>3. Plano de ação completo</h2>
            <p>O resultado de cada ação é <strong>calculado</strong> contando as falhas que ela elimina. Prazos e esforço seguem as premissas do anexo F.</p>
            <table><thead><tr><th>#</th><th>Ação e como fazer</th><th>Resultado esperado</th><th>Impacto</th><th>Esforço</th><th>Prazo</th></tr></thead><tbody>
            """);
        foreach (var acao in a.Acoes)
            sb.Append($"<tr><td class=\"num\">{acao.Ordem}</td><td><strong>{H(acao.Titulo)}</strong><br>{H(acao.ComoFazer)}</td><td>{H(acao.Impacto)}</td>" +
                      $"<td>{H(NomeNivel(acao.NivelImpacto))}</td><td>{H(NomeNivel(acao.Esforco))}</td><td>até {acao.PrazoDias} dias</td></tr>");
        sb.Append("</tbody></table>");

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

    // ================= 4. Limites =================

    private void SecaoLimites(StringBuilder sb, Lote lote)
    {
        var itens = new List<string>(lote.AvisosAmbiente);
        if (lote.Avaliacao is { } a)
            foreach (var f in a.Score.Fatores.Where(f => f.Valor is null))
                itens.Add($"\"{f.Nome}\" não foi medido: {f.Explicacao.Replace("Não medido: ", "")}");
        foreach (var r in lote.RegrasAvaliadas.Where(r => r.MotivoSemOcorrencia?.Contains("0 em comum") == true))
            itens.Add("Não foi possível ligar os alertas do firewall às máquinas com falhas: os arquivos não têm uma informação em comum (como o nome da máquina atacada).");
        if (lote.Lacunas.Any(l => l.Motivo.Contains("cortada")))
            itens.Add("Alguns arquivos têm exatamente 10.000 linhas, o que pode indicar que a exportação foi cortada: pode haver mais problemas do que os mostrados.");
        itens.Add(regras.Ativos.Premissa);
        itens.Add("Os dados não informam cliente ou ambiente: cada arquivo foi tratado como um ambiente. Datas sem fuso foram consideradas no horário de Brasília.");

        sb.Append("<section><h2>4. O que esta análise não consegue dizer</h2><ul>");
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
            <th class="num">Lidos</th><th class="num">Rejeitados</th><th class="num">Lacunas</th><th>SHA-256 (início)</th></tr></thead><tbody>
            """);
        foreach (var a in lote.Arquivos)
        {
            var fonte = a.Fonte is not null ? H(a.Fonte) : $"<strong>Não processado:</strong> {H(a.MotivoNaoProcessado)}";
            var sha = a.Sha256 is null ? "—" : $"<code>{H(a.Sha256[..16])}…</code>";
            sb.Append($"<tr><td>{H(a.Nome)}</td><td>{fonte}</td><td class=\"num\">{H(Formatar.Bytes(a.TamanhoBytes))}</td>" +
                      $"<td class=\"num\">{Formatar.Numero(a.RegistrosLidos)}</td><td class=\"num\">{Formatar.Numero(a.RegistrosRejeitados)}</td>" +
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

    private static void AnexoGlossario(StringBuilder sb)
    {
        sb.Append("<section><h2>G. Glossário</h2><dl class=\"glossario\">");
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
            ? "não dá para afirmar que o ambiente está seguro"
            : "o risco real pode ser maior ou menor";
        return $"Atenção: só {s.PercentualMedido:0}% do que deveria ser medido foi medido — {conclusao}.";
    }

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

    private static string H(string? texto) => Codificador.Encode(texto ?? "");

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
        .riscos{display:grid;gap:6px}
        .risco{border:1px solid var(--linha);border-left:6px solid var(--nenhuma);border-radius:6px;padding:5px 10px;break-inside:avoid}
        .risco p{margin:2px 0}.titulo-risco{font-size:14px;font-weight:700}
        .risco.sev-critica{border-left-color:var(--critica);color:inherit}.risco.sev-alta{border-left-color:var(--alta);color:inherit}
        .risco.sev-media{border-left-color:var(--media);color:inherit}.risco.sev-baixa{border-left-color:var(--baixa);color:inherit}
        .ordem{display:inline-block;min-width:20px;height:20px;border-radius:10px;background:var(--tinta);color:#fff;text-align:center;font-size:12px;line-height:20px;margin-right:4px}
        .aviso-forte{border:2px solid var(--alta);border-radius:6px;padding:6px 10px;margin:8px 0;font-weight:600}
        .uma-coisa{border:2px dashed var(--tinta);background:var(--fundo-claro);padding:8px 12px;margin:10px 0;font-size:14px}
        .barra{display:inline-block;width:120px;height:10px;border:1px solid var(--tinta);vertical-align:middle;margin-right:6px}
        .barra span{display:block;height:100%;background:var(--tinta)}
        tr.atual td{font-weight:700;background:var(--fundo-claro)}
        .matriz td,.matriz th{text-align:center;width:33%}
        .glossario dt{font-weight:700;margin-top:6px}.glossario dd{margin:0 0 4px 16px}
        code{font-family:Consolas,monospace;font-size:12px;word-break:break-all}
        td.id code{white-space:nowrap;word-break:normal}
        td.origem{min-width:120px}td.origem code{word-break:normal;overflow-wrap:anywhere}
        footer{margin-top:40px;border-top:1px solid var(--linha);padding-top:8px;color:var(--suave);font-size:12px}
        @page{size:A4;margin:14mm}
        @media print{body{padding:0;max-width:none}h2,h3{break-after:avoid}tr{break-inside:avoid}.quebra{break-before:page}}
        """;
}
