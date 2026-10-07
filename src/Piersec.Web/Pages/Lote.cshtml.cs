using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Armazenamento;
using Piersec.Core.Modelo;
using Piersec.Core.Operacao;
using Piersec.Core.Relatorio;
using Piersec.Web.Seguranca;

namespace Piersec.Web.Pages;

/// <summary>Resultado de um lote: visão executiva em cards, coluna lateral (score, próximas ações, aprovações) e visão técnica.</summary>
public sealed class LoteModel(RepositorioLotes repositorio, LotesEmMemoria lotes, GeradorRelatorioHtml gerador, ServicoOperacao operacao) : PageModel
{
    public Lote Lote { get; private set; } = null!;
    public bool PdfDisponivel { get; private set; }

    /// <summary>Versão da diretoria (HTML e PDF); lotes analisados antes dela existir não têm.</summary>
    public bool DiretoriaDisponivel { get; private set; }
    public bool PdfDiretoriaDisponivel { get; private set; }

    /// <summary>Mesmo aviso de confiança baixa da página 1 do relatório (null quando a confiança não é baixa).</summary>
    public string? AvisoConfianca { get; private set; }

    /// <summary>"Três problemas respondem por…" ou "O risco está espalhado…" (null quando não se aplica).</summary>
    public string? FraseAchados { get; private set; }

    /// <summary>Revisões feitas a partir deste lote (quando alguém discordou de severidades).</summary>
    public IReadOnlyList<ResumoLote> Revisoes { get; private set; } = [];

    /// <summary>Chamados esperando uma pessoa agora (estado atual da operação, não só deste lote).</summary>
    public IReadOnlyList<Chamado> Aguardando { get; private set; } = [];

    /// <summary>Chip de severidade escolhido nos cards de achados ("Critica", "Alta"…); nulo = todos.</summary>
    [BindProperty(SupportsGet = true)] public string? Sev { get; set; }

    public Severidade? SeveridadeEscolhida => Enum.TryParse<Severidade>(Sev, out var s) && Enum.IsDefined(s) ? s : null;

    public IActionResult OnGet(string id)
    {
        var lote = lotes.Obter(id); // id inválido → null → 404
        if (lote is null)
            return NotFound();
        Lote = lote;
        PdfDisponivel = repositorio.ExisteRelatorio(id, "pdf");
        DiretoriaDisponivel = repositorio.ExisteRelatorio(id, "html", VersaoRelatorio.Diretoria);
        PdfDiretoriaDisponivel = repositorio.ExisteRelatorio(id, "pdf", VersaoRelatorio.Diretoria);
        AvisoConfianca = lote.Avaliacao is { } a ? gerador.AvisoConfianca(a.Score) : null;
        FraseAchados = lote.Avaliacao is { } av ? gerador.FraseAchados(av) : null;
        Revisoes = repositorio.Listar().Where(r => r.RevisaoDe == lote.Id).ToList();
        Aguardando = operacao.Estado().Chamados.Where(c => c.Estado == EstadoChamado.AguardandoAprovacao).ToList();
        return Page();
    }

    /// <summary>"pior que a análise anterior (+5)", "melhor…", "igual…" ou "primeira análise": o número grande sempre com contexto.</summary>
    public string ContextoScore()
    {
        if (Lote.Comparacao is not { } c)
            return "primeira análise";
        var pontos = Math.Round(Math.Abs(c.Diferenca));
        var quem = Lote.RevisaoDe is not null ? "o lote original" : "a análise anterior";
        return c.Diferenca >= 0.5 ? $"pior que {quem} (+{pontos:0})"
             : c.Diferenca <= -0.5 ? $"melhor que {quem} (−{pontos:0})"
             : $"igual a {quem}";
    }
}
