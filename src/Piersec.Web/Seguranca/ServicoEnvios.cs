using System.Collections.Concurrent;
using Piersec.Core.Pipeline;
using Piersec.Core.Seguranca;

namespace Piersec.Web.Seguranca;

/// <summary>Como está um envio, para a tela mostrar o progresso por etapa.</summary>
public sealed record EstadoEnvio(IReadOnlyList<string> Etapas, int EtapaAtual, bool Concluido, string? LoteId, string? Erro);

/// <summary>
/// Envio em segundo plano: a página manda os arquivos, recebe um número de acompanhamento e pergunta "em que etapa está?"
/// até terminar. Cada envio só é visível para quem o fez. No máximo <see cref="MaximoSimultaneos"/> ao mesmo tempo
/// (protege a máquina) e cada acompanhamento some <see cref="Validade"/> depois de terminar.
/// Os arquivos ficam só na memória durante o processamento, como no envio normal (o CSV bruto não é guardado).
/// </summary>
public sealed class ServicoEnvios(CicloAnalise ciclo, Auditoria auditoria, ILogger<ServicoEnvios> log)
{
    public const int MaximoSimultaneos = 2;
    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(10);

    /// <summary>Nomes das etapas na ordem de <see cref="EtapaCiclo"/>.</summary>
    public static readonly IReadOnlyList<string> NomesEtapas =
    [
        "Lendo e padronizando os arquivos",
        "Classificando, correlacionando e calculando o score",
        "Conferindo a consistência dos números",
        "Rodando a operação automática (simulada)",
        "Gerando o relatório",
        "Gerando o PDF",
    ];

    private sealed class Envio(string usuario)
    {
        public string Usuario { get; } = usuario;
        public volatile int Etapa;
        public volatile bool Concluido;
        public string? LoteId;
        public string? Erro;
        public DateTimeOffset Fim = DateTimeOffset.MaxValue;
    }

    private readonly ConcurrentDictionary<string, Envio> _envios = new();

    /// <summary>Começa o ciclo em segundo plano. Devolve o número de acompanhamento, ou o motivo de não começar.</summary>
    public (string? Id, string? Erro) Iniciar(string usuario, IReadOnlyList<ArquivoEnviado> arquivos)
    {
        Limpar();
        if (_envios.Values.Count(e => !e.Concluido) >= MaximoSimultaneos)
            return (null, "Já há análises em andamento. Espere uma terminar e envie de novo.");

        var id = Guid.NewGuid().ToString("N");
        var envio = new Envio(usuario);
        _envios[id] = envio;
        _ = Task.Run(async () =>
        {
            try
            {
                // Sem o token da requisição: fechar a aba não interrompe a análise pela metade.
                var resultado = await ciclo.ExecutarAsync(arquivos, CancellationToken.None, e => envio.Etapa = (int)e);
                var lote = resultado.Lote;
                envio.LoteId = lote.Id;
                // Só números no log e na auditoria: nada de conteúdo dos arquivos (RS4).
                log.LogInformation("Lote {Id}: {Arquivos} arquivo(s), {Eventos} evento(s), {Lacunas} lacuna(s). PDF: {Pdf}",
                    lote.Id, lote.Arquivos.Count, lote.Eventos.Count, lote.Lacunas.Count, resultado.MensagemPdf);
                auditoria.Registrar(usuario, "lote_processado",
                    $"lote {lote.Id[..8]} · {lote.Arquivos.Count} arquivo(s) · verificações {lote.Validacao?.Aprovadas}/{lote.Validacao?.Total}");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Falha ao processar um envio");
                envio.Erro = "Não foi possível analisar estes arquivos. Nada foi salvo. Se o problema continuar, mande para a equipe a mensagem do terminal.";
            }
            finally
            {
                envio.Fim = DateTimeOffset.UtcNow;
                envio.Concluido = true;
            }
        });
        return (id, null);
    }

    /// <summary>Estado do envio; null se não existir ou se for de outra pessoa.</summary>
    public EstadoEnvio? Estado(string id, string usuario) =>
        _envios.TryGetValue(id, out var e) && e.Usuario == usuario
            ? new EstadoEnvio(NomesEtapas, e.Etapa, e.Concluido, e.LoteId, e.Erro)
            : null;

    private void Limpar()
    {
        var agora = DateTimeOffset.UtcNow;
        foreach (var (id, e) in _envios)
            if (e.Concluido && agora - e.Fim > Validade)
                _envios.TryRemove(id, out _);
    }
}
