using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Piersec.Core.Analise;
using Piersec.Core.Modelo;

namespace Piersec.Core.Relatorio;

/// <summary>
/// Monta o relatório executivo em HTML a partir do lote (RG1: ninguém digita número nenhum).
/// Todo texto vindo dos dados passa por <see cref="H"/> (escape de HTML), porque o dado sintético
/// pode trazer texto malicioso de propósito. O HTML não carrega nada externo (sem CDN, sem script).
/// A metodologia é lida das mesmas regras que a plataforma usou (regras/*.json).
/// </summary>
public sealed class GeradorRelatorioHtml(TimeZoneInfo fusoExibicao, ConjuntoRegras regras)
{
    public const string SeloConfidencialidade = "CONFIDENCIAL — uso restrito à diretoria e à equipe de segurança";
    public const int MaximoEventosListados = 25;
    public const int MaximoCorrelacoesListadas = 15;
    public const int MaximoGruposDeLacunas = 50;
    public const int TamanhoMaximoTexto = 220;

    public string Gerar(Lote lote)
    {
        var sb = new StringBuilder();
        var idCurto = lote.Id[..8];
        var reconhecidos = lote.Arquivos.Count(a => a.Fonte is not null);

        sb.Append($"""
            <!doctype html>
            <html lang="pt-BR">
            <head>
            <meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Relatório executivo de segurança — lote {H(idCurto)}</title>
            <style>{Estilo}</style>
            </head>
            <body>
            <header class="capa">
              <p class="selo">{H(SeloConfidencialidade)}</p>
              <h1>Relatório executivo de segurança</h1>
              <p class="meta">Lote {H(idCurto)} · gerado automaticamente pela plataforma em {H(Formatar.DataHora(lote.CriadoEmUtc, fusoExibicao))} (horário de Brasília)
              · {reconhecidos} de {lote.Arquivos.Count} arquivo(s) reconhecido(s)</p>
              <p class="aviso"><strong>Versão parcial (Fase 2).</strong> Já traz severidade decidida por nota técnica (com justificativa)
              e correlações entre eventos. Ainda não traz score de risco, impacto no negócio nem plano de ação priorizado.</p>
            </header>
            """);

        SecaoResumo(sb, lote);
        SecaoComoDecidimos(sb, lote);
        SecaoCorrelacoes(sb, lote);
        SecaoEventos(sb, lote);
        SecaoArquivos(sb, lote);
        SecaoLacunas(sb, lote);
        SecaoMetodologia(sb);

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

    private static void SecaoResumo(StringBuilder sb, Lote lote)
    {
        var criticas = lote.ContarPorSeveridade(Severidade.Critica);
        var correlacoesCriticas = lote.Correlacoes.Count(c => c.Severidade == Severidade.Critica);
        sb.Append($"""
            <section>
            <h2>1. Resumo</h2>
            <ul class="destaques">
              <li><strong>{Formatar.Numero(lote.Eventos.Count)}</strong> eventos analisados, dos quais <strong>{Formatar.Numero(criticas)}</strong> classificados como Crítica.</li>
              <li><strong>{Formatar.Numero(lote.Correlacoes.Count)}</strong> correlações encontradas ({Formatar.Numero(correlacoesCriticas)} críticas), por {lote.RegrasAvaliadas.Count} regras.</li>
              <li><strong>{Formatar.Numero(lote.Lacunas.Count)}</strong> lacunas declaradas (dados faltando, inválidos ou limites das fontes) — ver seção 6.</li>
            </ul>
            <table><thead><tr><th>Severidade (decidida pela plataforma)</th><th class="num">Eventos</th></tr></thead><tbody>
            """);
        foreach (var severidade in SeveridadeExtensoes.DoMaisGrave)
            sb.Append($"<tr><td>{Selo(severidade)}</td><td class=\"num\">{Formatar.Numero(lote.ContarPorSeveridade(severidade))}</td></tr>");
        sb.Append($"<tr><td>{Selo(null)} <span class=\"meta\">(sem informação para classificar — ver lacunas)</span></td>" +
                  $"<td class=\"num\">{Formatar.Numero(lote.ContarPorSeveridade(null))}</td></tr>");
        sb.Append($"</tbody><tfoot><tr><th>Total</th><th class=\"num\">{Formatar.Numero(lote.Eventos.Count)}</th></tr></tfoot></table>");
        if (lote.Eventos.Count == 0)
            sb.Append("<p class=\"aviso\">Nenhum evento foi lido. Isso <strong>não</strong> significa ambiente sem problemas: veja a seção de lacunas.</p>");
        sb.Append("</section>");
    }

    private static void SecaoComoDecidimos(StringBuilder sb, Lote lote)
    {
        var transicoes = Agrupamentos.Transicoes(lote.Eventos).Take(20).ToList();
        sb.Append("""
            <section>
            <h2>2. Como a severidade foi decidida</h2>
            <p>A plataforma não copia a severidade da fonte: usa a nota técnica (CVSS das vulnerabilidades ou nível dos alertas).
            A tabela mostra o que cada fonte informou e o que a plataforma decidiu. Cada evento traz a sua justificativa.</p>
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

    private static void SecaoCorrelacoes(StringBuilder sb, Lote lote)
    {
        var lista = Agrupamentos.CorrelacoesMaisGraves(lote.Correlacoes).Take(MaximoCorrelacoesListadas).ToList();
        sb.Append($"""
            <section>
            <h2>3. Correlações: o que só aparece olhando o conjunto</h2>
            <p class="meta">As {lista.Count} mais graves de {Formatar.Numero(lote.Correlacoes.Count)}. Cada correlação vem de uma regra
            declarada (seção 7) e lista as máquinas e eventos envolvidos.</p>
            """);
        if (lista.Count > 0)
        {
            sb.Append("<table><thead><tr><th>Id</th><th>Severidade</th><th>O que foi encontrado</th><th>Por que importa</th><th class=\"num\">Máquinas</th><th class=\"num\">Eventos</th><th>Frameworks</th></tr></thead><tbody>");
            foreach (var c in lista)
            {
                sb.Append($"<tr><td class=\"id\"><code>{H(c.Id)}</code></td><td>{Selo(c.Severidade)}</td><td>{H(c.Titulo)}</td>" +
                          $"<td>{H(c.Justificativa)}</td><td class=\"num\">{Formatar.Numero(c.Ativos.Count)}</td>" +
                          $"<td class=\"num\">{Formatar.Numero(c.EventoIds.Count)}</td><td class=\"pequeno\">{H(Agrupamentos.Frameworks(c.Frameworks))}</td></tr>");
            }
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

    private void SecaoEventos(StringBuilder sb, Lote lote)
    {
        var maisGraves = Agrupamentos.MaisGraves(lote.Eventos).Take(MaximoEventosListados).ToList();
        sb.Append($"""
            <section>
            <h2>4. Eventos mais graves</h2>
            <p class="meta">Os {maisGraves.Count} primeiros de {Formatar.Numero(lote.Eventos.Count)}. "Origem" é o arquivo e a linha de onde o evento saiu.</p>
            """);
        if (maisGraves.Count == 0)
        {
            sb.Append("<p>Sem eventos para listar.</p></section>");
            return;
        }
        sb.Append("<table><thead><tr><th>Id</th><th>Severidade</th><th>Máquina</th><th>Evento</th><th>Por que esta severidade</th><th>Data</th><th>Origem</th></tr></thead><tbody>");
        foreach (var e in maisGraves)
        {
            sb.Append($"<tr><td class=\"id\"><code>{H(e.Id)}</code></td><td>{Selo(e.Severidade)}</td><td>{H(e.Ativo)}</td><td>{H(Curto(e.Descricao))}</td>" +
                      $"<td class=\"pequeno\">{H(e.Justificativa)}</td><td>{H(Formatar.DataHora(e.DataUtc, fusoExibicao))}</td>" +
                      $"<td class=\"origem\"><code>{H(e.Evidencia.Arquivo)}:{e.Evidencia.Linha}</code></td></tr>");
        }
        sb.Append("</tbody></table></section>");
    }

    private static void SecaoArquivos(StringBuilder sb, Lote lote)
    {
        sb.Append("""
            <section>
            <h2>5. Fontes recebidas</h2>
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
        sb.Append("""
            <p class="meta">SHA-256 é a "impressão digital" do arquivo: se um único caractere mudar, ela muda.
            Serve para provar qual arquivo gerou este relatório.</p>
            </section>
            """);
    }

    private static void SecaoLacunas(StringBuilder sb, Lote lote)
    {
        var grupos = Agrupamentos.Lacunas(lote.Lacunas);
        sb.Append($"""
            <section>
            <h2>6. Lacunas e limites</h2>
            <p>O que faltou, veio inválido ou limita a análise. Nada foi descartado em silêncio:
            {Formatar.Numero(lote.Lacunas.Count)} lacuna(s), agrupadas por motivo.</p>
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
        sb.Append("""
            <h3>Limites desta versão</h3>
            <ul>
              <li>Ainda não há score de risco, impacto no negócio nem plano de remediação priorizado (próximas fases).</li>
              <li>A severidade usa a nota técnica da fonte; exposição à internet e importância de cada máquina para o negócio ainda não entram na conta.</li>
              <li>Os dados não informam cliente ou ambiente: cada arquivo foi tratado como um ambiente (premissa).</li>
              <li>Datas sem fuso horário foram consideradas no horário de Brasília (premissa).</li>
            </ul>
            </section>
            """);
    }

    private void SecaoMetodologia(StringBuilder sb)
    {
        var s = regras.Severidade;
        sb.Append($"""
            <section>
            <h2>7. Metodologia</h2>
            <p>{H(s.Descricao)}</p>
            <h3>Severidade pela nota CVSS ({H(s.Cvss.Fonte)})</h3>
            {TabelaFaixas(s.Cvss)}
            <h3>Severidade pelo nível do alerta ({H(s.NivelAlerta.Fonte)})</h3>
            {TabelaFaixas(s.NivelAlerta)}
            <h3>Severidade pelo texto da fonte (quando não há nota nem nível)</h3>
            <table><thead><tr><th>Severidade</th><th>Textos aceitos (sem diferenciar maiúsculas e acentos)</th></tr></thead><tbody>
            """);
        foreach (var severidade in SeveridadeExtensoes.DoMaisGrave)
            sb.Append($"<tr><td>{Selo(severidade)}</td><td>{H(string.Join(", ", s.Textos[severidade]))}</td></tr>");
        sb.Append("</tbody></table>");

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
            <p>Nomes de usuário, quando existem, são trocados na entrada por um apelido (ex.: USR-3FA9C01B7E) calculado com HMAC-SHA256
            e uma chave secreta. O mesmo usuário recebe sempre o mesmo apelido, mas o nome original não é guardado.</p>
            </section>
            """);
    }

    private static string TabelaFaixas(EscalaFaixas escala)
    {
        var sb = new StringBuilder("<table><thead><tr><th>Severidade</th><th>Faixa</th></tr></thead><tbody>");
        foreach (var f in escala.Faixas)
            sb.Append($"<tr><td>{Selo(f.Severidade)}</td><td>{H(f.Descricao)}</td></tr>");
        return sb.Append("</tbody></table>").ToString();
    }

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
        :root{--tinta:#1b2430;--suave:#5b6675;--linha:#d5dbe3;--cabecalho:#eef1f5;
        --critica:#b42318;--alta:#c4320a;--media:#8a5a00;--baixa:#2f6b3a;--nenhuma:#5b6675}
        *{box-sizing:border-box}
        body{font-family:"Segoe UI",Arial,sans-serif;color:var(--tinta);background:#fff;margin:0 auto;max-width:1000px;padding:32px;line-height:1.45;font-size:14px}
        .selo{display:inline-block;border:2px solid var(--tinta);padding:4px 10px;font-weight:700;letter-spacing:.04em;font-size:12px;margin:0}
        h1{font-size:28px;margin:16px 0 4px}
        h2{font-size:19px;border-bottom:2px solid var(--tinta);padding-bottom:4px;margin-top:32px}
        h3{font-size:15px;margin-top:20px}
        table{width:100%;border-collapse:collapse;margin:8px 0}
        th,td{border:1px solid var(--linha);padding:6px 8px;text-align:left;vertical-align:top}
        th{background:var(--cabecalho)}
        .num{text-align:right;font-variant-numeric:tabular-nums}
        .meta{color:var(--suave)}
        .pequeno{font-size:12px}
        .destaques li{margin:4px 0}
        .aviso{border-left:4px solid var(--suave);padding:8px 12px;background:#f5f7fa}
        .sev{font-weight:700;white-space:nowrap}
        .sev-critica{color:var(--critica)}.sev-alta{color:var(--alta)}.sev-media{color:var(--media)}.sev-baixa{color:var(--baixa)}.sev-nenhuma{color:var(--nenhuma)}
        code{font-family:Consolas,monospace;font-size:12px;word-break:break-all}
        td.id code{white-space:nowrap;word-break:normal}
        td.origem{min-width:120px}td.origem code{word-break:normal;overflow-wrap:anywhere}
        footer{margin-top:40px;border-top:1px solid var(--linha);padding-top:8px;color:var(--suave);font-size:12px}
        @page{size:A4;margin:16mm}
        @media print{body{padding:0;max-width:none}h2,h3{break-after:avoid}tr{break-inside:avoid}}
        """;
}
