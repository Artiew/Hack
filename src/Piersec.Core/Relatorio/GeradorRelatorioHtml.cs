using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Piersec.Core.Modelo;
using Piersec.Core.Padronizacao;

namespace Piersec.Core.Relatorio;

/// <summary>
/// Monta o relatório executivo em HTML a partir do lote (RG1: ninguém digita número nenhum).
/// Todo texto vindo dos dados passa por <see cref="H"/> (escape de HTML), porque o dado sintético
/// pode trazer texto malicioso de propósito. O HTML não carrega nada externo (sem CDN, sem script).
/// </summary>
public sealed class GeradorRelatorioHtml(TimeZoneInfo fusoExibicao)
{
    public const string SeloConfidencialidade = "CONFIDENCIAL — uso restrito à diretoria e à equipe de segurança";
    public const int MaximoEventosListados = 25;
    public const int MaximoLacunasListadas = 100;

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
              <p class="aviso"><strong>Versão esqueleto (Fase 1).</strong> Este relatório ainda não traz correlação entre fontes,
              score de risco nem plano de ação. A severidade abaixo é a informada por cada fonte, apenas convertida
              para a escala Crítica / Alta / Média / Baixa — ainda sem levar em conta o contexto.</p>
            </header>
            """);

        SecaoResumo(sb, lote);
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
        sb.Append("""
            <section>
            <h2>1. Resumo: quantos eventos por severidade</h2>
            <table><thead><tr><th>Severidade</th><th class="num">Eventos</th></tr></thead><tbody>
            """);
        foreach (var severidade in SeveridadeExtensoes.DoMaisGrave)
            sb.Append($"<tr><td>{Selo(severidade)}</td><td class=\"num\">{Formatar.Numero(lote.ContarPorSeveridade(severidade))}</td></tr>");
        sb.Append($"<tr><td>{Selo(null)} <span class=\"meta\">(não foi possível converter — ver lacunas)</span></td>" +
                  $"<td class=\"num\">{Formatar.Numero(lote.ContarPorSeveridade(null))}</td></tr>");
        sb.Append($"</tbody><tfoot><tr><th>Total</th><th class=\"num\">{Formatar.Numero(lote.Eventos.Count)}</th></tr></tfoot></table>");
        if (lote.Eventos.Count == 0)
            sb.Append("<p class=\"aviso\">Nenhum evento foi lido. Isso <strong>não</strong> significa ambiente sem problemas: veja a seção de lacunas.</p>");
        sb.Append("</section>");
    }

    private void SecaoEventos(StringBuilder sb, Lote lote)
    {
        var maisGraves = lote.Eventos
            .OrderByDescending(e => e.Severidade ?? 0)
            .ThenByDescending(e => e.DataUtc)
            .Take(MaximoEventosListados)
            .ToList();

        sb.Append($"""
            <section>
            <h2>2. Eventos mais graves</h2>
            <p class="meta">Os {maisGraves.Count} primeiros de {Formatar.Numero(lote.Eventos.Count)}, do mais grave para o menos grave.
            A coluna "Origem" mostra arquivo e linha de onde cada evento saiu.</p>
            """);
        if (maisGraves.Count == 0)
        {
            sb.Append("<p>Sem eventos para listar.</p></section>");
            return;
        }
        sb.Append("<table><thead><tr><th>Id</th><th>Severidade</th><th>Ativo</th><th>Tipo</th><th>Data</th><th>Descrição</th><th>Origem</th></tr></thead><tbody>");
        foreach (var e in maisGraves)
        {
            sb.Append($"<tr><td class=\"id\"><code>{H(e.Id)}</code></td><td>{Selo(e.Severidade)}</td><td>{H(e.Ativo)}</td><td>{H(e.Tipo)}</td>" +
                      $"<td>{H(Formatar.DataHora(e.DataUtc, fusoExibicao))}</td><td>{H(e.Descricao)}</td>" +
                      $"<td class=\"origem\"><code>{H(e.Evidencia.Arquivo)}:{e.Evidencia.Linha}</code></td></tr>");
        }
        sb.Append("</tbody></table></section>");
    }

    private static void SecaoArquivos(StringBuilder sb, Lote lote)
    {
        sb.Append("""
            <section>
            <h2>3. Fontes recebidas</h2>
            <table><thead><tr><th>Arquivo</th><th>Fonte reconhecida</th><th class="num">Tamanho</th>
            <th class="num">Lidos</th><th class="num">Rejeitados</th><th class="num">Lacunas</th><th>SHA-256 (início)</th></tr></thead><tbody>
            """);
        foreach (var a in lote.Arquivos)
        {
            var fonte = a.Fonte is not null ? H(a.Fonte) : $"<strong>Não processado:</strong> {H(a.MotivoNaoProcessado)}";
            var sha = a.Sha256 is null ? "—" : $"<code>{H(a.Sha256[..16])}…</code>";
            sb.Append($"<tr><td>{H(a.Nome)}</td><td>{fonte}</td><td class=\"num\">{H(Formatar.Bytes(a.TamanhoBytes))}</td>" +
                      $"<td class=\"num\">{a.RegistrosLidos}</td><td class=\"num\">{a.RegistrosRejeitados}</td><td class=\"num\">{a.Lacunas}</td><td>{sha}</td></tr>");
        }
        sb.Append("""
            </tbody></table>
            <p class="meta">SHA-256 é a "impressão digital" do arquivo: se um único caractere mudar, ela muda.
            Serve para provar qual arquivo gerou este relatório.</p>
            </section>
            """);
    }

    private static void SecaoLacunas(StringBuilder sb, Lote lote)
    {
        sb.Append($"""
            <section>
            <h2>4. Lacunas e limites</h2>
            <p>Itens que faltaram ou vieram inválidos. Nada foi descartado em silêncio: {Formatar.Numero(lote.Lacunas.Count)} lacuna(s) no total.</p>
            """);
        if (lote.Lacunas.Count > 0)
        {
            sb.Append("<table><thead><tr><th>Arquivo</th><th class=\"num\">Linha</th><th>Campo</th><th>Motivo</th></tr></thead><tbody>");
            foreach (var l in lote.Lacunas.Take(MaximoLacunasListadas))
                sb.Append($"<tr><td>{H(l.Arquivo)}</td><td class=\"num\">{(l.Linha?.ToString() ?? "—")}</td><td>{H(l.Campo)}</td><td>{H(l.Motivo)}</td></tr>");
            sb.Append("</tbody></table>");
            if (lote.Lacunas.Count > MaximoLacunasListadas)
                sb.Append($"<p class=\"meta\">… e mais {lote.Lacunas.Count - MaximoLacunasListadas} lacuna(s), disponíveis na plataforma.</p>");
        }
        sb.Append("""
            <h3>Limites desta versão</h3>
            <ul>
              <li>Ainda não há correlação entre fontes, score de risco nem plano de remediação (próximas fases).</li>
              <li>A severidade é a da fonte, convertida pela tabela da seção 5; o contexto (exposição, criticidade do serviço) ainda não é considerado.</li>
              <li>Datas sem fuso horário foram consideradas no horário de Brasília.</li>
            </ul>
            </section>
            """);
    }

    private static void SecaoMetodologia(StringBuilder sb)
    {
        sb.Append("""
            <section>
            <h2>5. Metodologia</h2>
            <h3>Conversão de severidade</h3>
            <table><thead><tr><th>Severidade na plataforma</th><th>Textos aceitos das fontes (sem diferenciar maiúsculas e acentos)</th></tr></thead><tbody>
            """);
        foreach (var severidade in SeveridadeExtensoes.DoMaisGrave)
        {
            var textos = ConversorSeveridade.Tabela.Where(p => p.Value == severidade).Select(p => p.Key);
            sb.Append($"<tr><td>{Selo(severidade)}</td><td>{H(string.Join(", ", textos))}</td></tr>");
        }
        sb.Append("""
            </tbody></table>
            <h3>Privacidade (LGPD)</h3>
            <p>Nomes de usuário são trocados na entrada por um apelido (ex.: USR-3FA9C01B7E) calculado com HMAC-SHA256
            e uma chave secreta. O mesmo usuário recebe sempre o mesmo apelido, mas o nome original não é guardado.</p>
            </section>
            """);
    }

    /// <summary>Severidade sempre com ícone + texto (legível em preto e branco) + cor.</summary>
    private static string Selo(Severidade? severidade) =>
        $"<span class=\"sev {severidade.ClasseCss()}\"><span aria-hidden=\"true\">{severidade.Simbolo()}</span> {H(severidade.Nome())}</span>";

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
