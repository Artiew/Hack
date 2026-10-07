using System.Text;
using Piersec.Core.Armazenamento;
using Piersec.Core.Modelo;
using Piersec.Core.Operacao;
using Piersec.Core.Relatorio;
using Piersec.Core.Validacao;

namespace Piersec.Core.Pipeline;

public sealed record ResultadoCiclo(Lote Lote, bool PdfGerado, string MensagemPdf);

/// <summary>Resposta a um pedido de "discordar da severidade": a revisão criada, ou por que não foi criada.</summary>
public sealed record ResultadoRevisao(bool Ok, string Mensagem, ResultadoCiclo? Ciclo = null);

/// <summary>Etapas do ciclo, para a tela mostrar o progresso ("lendo os arquivos…", "gerando o PDF…").</summary>
public enum EtapaCiclo { Lendo, Analisando, Validando, Operando, Relatorio, Pdf }

/// <summary>
/// O ciclo completo, sem depender da tela: processa o lote, compara com o lote anterior, VALIDA, salva e gera o
/// relatório (HTML + PDF). Se a validação reprovar, o relatório sai marcado como "não validado"; se o relatório
/// pronto contiver algo com cara de credencial, ele é retido. Fica no núcleo para a automação poder chamá-lo.
/// </summary>
public sealed class CicloAnalise(
    ProcessadorLote processador,
    RepositorioLotes repositorio,
    GeradorRelatorioHtml gerador,
    ConversorPdf conversorPdf,
    ValidadorLote validador,
    ServicoOperacao? operacao = null,
    TimeProvider? relogio = null)
{
    public const int TamanhoMinimoMotivo = 10;

    public async Task<ResultadoCiclo> ExecutarAsync(IReadOnlyList<ArquivoEnviado> arquivos, CancellationToken ct = default,
        Action<EtapaCiclo>? etapa = null)
    {
        etapa?.Invoke(EtapaCiclo.Lendo);
        var lote = processador.Processar(arquivos, etapa);
        var anterior = repositorio.Listar().FirstOrDefault(r => r.Score is not null);
        return await FinalizarAsync(lote, anterior, ct, etapa);
    }

    /// <summary>
    /// "Discordar da severidade": cria uma REVISÃO do lote com o ajuste (o original fica como está), refaz tudo e
    /// compara com o original, para mostrar o efeito do ajuste. Motivo obrigatório; sem ele, nada muda.
    /// </summary>
    public async Task<ResultadoRevisao> RevisarAsync(Lote original, string eventoId, Severidade para, string? motivo, string usuario,
        CancellationToken ct = default)
    {
        if (original.Eventos.FirstOrDefault(e => e.Id == eventoId) is not { } evento)
            return new(false, "Evento não encontrado neste lote.");
        if (evento.Severidade == para)
            return new(false, $"O evento já está como {para.Nome()}: nada a ajustar.");
        var texto = ServicoOperacao.LimparMotivo(motivo);
        if (texto.Length < TamanhoMinimoMotivo)
            return new(false, $"Explique o motivo (pelo menos {TamanhoMinimoMotivo} caracteres): ele fica registrado e aparece no relatório.");

        // Se o evento já tinha sido ajustado numa revisão anterior, guarda a justificativa ORIGINAL da plataforma.
        var anteriorDoEvento = original.Ajustes.LastOrDefault(a => a.EventoId == eventoId);
        var ajuste = new AjusteSeveridade(eventoId, evento.Severidade, para, texto, usuario, (relogio ?? TimeProvider.System).GetUtcNow(),
            anteriorDoEvento?.JustificativaOriginal ?? evento.Justificativa ?? "");
        var ajustes = original.Ajustes.Where(a => a.EventoId != eventoId).Append(ajuste).ToList();

        var revisao = processador.Revisar(original, ajustes);
        var ciclo = await FinalizarAsync(revisao, ResumoLote.De(original), ct);
        return new(true, $"Revisão criada com {ajustes.Count} ajuste(s) de severidade.", ciclo);
    }

    private async Task<ResultadoCiclo> FinalizarAsync(Lote lote, ResumoLote? anterior, CancellationToken ct, Action<EtapaCiclo>? etapa = null)
    {
        lote = lote with { Comparacao = Comparar(lote, anterior) };
        etapa?.Invoke(EtapaCiclo.Validando);
        lote = lote with { Validacao = validador.Validar(lote) };

        // Operação automática (simulada): roda depois da validação, porque lote não validado não dispara nada sozinho.
        // Em seguida, V13 confere por outro caminho que nenhuma ação feita sozinha furou as travas.
        if (operacao is not null)
        {
            etapa?.Invoke(EtapaCiclo.Operando);
            var resultado = operacao.Processar(lote);
            lote = lote with
            {
                Operacao = resultado,
                Validacao = new ResultadoValidacao([.. lote.Validacao!.Verificacoes, MotorDecisao.Conferir(resultado, operacao.Config)]),
            };
        }

        // Relatório gerado e relido por inteiro antes de sair (V12). Se algo escapou do filtro, é retido.
        etapa?.Invoke(EtapaCiclo.Relatorio);
        var html = gerador.Gerar(lote);
        var final = ValidadorLote.ConferirRelatorioFinal(html);
        lote = lote with { Validacao = new ResultadoValidacao([.. lote.Validacao!.Verificacoes, final]) };
        html = final.Aprovada ? gerador.Gerar(lote) : gerador.GerarRetido(lote);

        repositorio.Salvar(lote);
        repositorio.SalvarRelatorio(lote.Id, "html", new UTF8Encoding(false).GetBytes(html));

        // O navegador precisa do HTML aberto para "imprimir" o PDF: arquivos temporários em var/tmp, apagados no fim.
        var temporaria = repositorio.CriarPastaTemporaria();
        try
        {
            var caminhoHtml = Path.Combine(temporaria, "relatorio.html");
            var caminhoPdf = Path.Combine(temporaria, "relatorio.pdf");
            await File.WriteAllTextAsync(caminhoHtml, html, new UTF8Encoding(false), ct);
            etapa?.Invoke(EtapaCiclo.Pdf);
            var pdf = await conversorPdf.GerarAsync(caminhoHtml, caminhoPdf, ct);
            if (pdf.Sucesso)
                repositorio.SalvarRelatorio(lote.Id, "pdf", await File.ReadAllBytesAsync(caminhoPdf, ct));
            return new ResultadoCiclo(lote, pdf.Sucesso, pdf.Mensagem);
        }
        finally
        {
            ApagarTemporaria(temporaria);
        }
    }

    private static void ApagarTemporaria(string pasta)
    {
        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            try
            {
                Directory.Delete(pasta, recursive: true);
                return;
            }
            catch (IOException) { Thread.Sleep(200); } // o navegador pode segurar o arquivo por instantes
            catch (UnauthorizedAccessException) { Thread.Sleep(200); }
        }
    }

    /// <summary>Tendência: quanto o score mudou e qual fator mais explica a mudança.</summary>
    public static ComparacaoLote? Comparar(Lote atual, ResumoLote? anterior)
    {
        if (anterior?.Score is not { } scoreAnterior || atual.Avaliacao?.Score.Valor is not { } scoreAtual)
            return null;

        var motivo = "os fatores medidos não mudaram";
        var fatoresAntes = anterior.Fatores ?? new Dictionary<string, double?>();
        var mudancas = atual.Avaliacao.Score.Fatores
            .Select(f => (f.Nome, Antes: fatoresAntes.GetValueOrDefault(f.Id), Agora: f.Valor))
            .Where(m => m.Antes != m.Agora)
            .OrderByDescending(m => Math.Abs((m.Agora ?? 0) - (m.Antes ?? 0)))
            .ToList();
        if (mudancas.Count > 0)
        {
            var m = mudancas[0];
            motivo = (m.Antes, m.Agora) switch
            {
                (null, { } agora) => $"o fator \"{m.Nome}\" passou a ser medido ({agora:0})",
                ({ } antes, null) => $"o fator \"{m.Nome}\" deixou de ser medido (era {antes:0})",
                ({ } antes, { } agora) => $"o fator \"{m.Nome}\" foi de {antes:0} para {agora:0}",
                _ => motivo,
            };
        }
        return new ComparacaoLote(anterior.Id, anterior.CriadoEmUtc, scoreAnterior, Math.Round(scoreAtual - scoreAnterior, 1), motivo);
    }
}
