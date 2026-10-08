using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Piersec.Core.Analise;
using Piersec.Core.Armazenamento;
using Piersec.Core.Modelo;
using Piersec.Core.Operacao;
using Piersec.Core.Relatorio;
using Piersec.Web.Seguranca;

namespace Piersec.Web.Pages;

public sealed class LoteModel(RepositorioLotes repositorio, LotesEmMemoria lotes, GeradorRelatorioHtml gerador,
    ServicoOperacao operacao, ConjuntoRegras regras) : PageModel
{
    public Lote Lote { get; private set; } = null!;
    public bool PdfDisponivel { get; private set; }
    public bool DiretoriaDisponivel { get; private set; }
    public bool PdfDiretoriaDisponivel { get; private set; }
    public string? AvisoConfianca { get; private set; }
    public string? FraseAchados { get; private set; }
    public IReadOnlyList<ResumoLote> Revisoes { get; private set; } = [];
    public IReadOnlyList<ResumoLote> Historico { get; private set; } = [];
    public IReadOnlyList<Chamado> Aguardando { get; private set; } = [];
    public SimulacaoRemediacao? Simulacao { get; private set; }
    public string? ErroSimulacao { get; private set; }
    public IReadOnlySet<int> Selecionadas { get; private set; } = new HashSet<int>();
    [BindProperty(SupportsGet = true)] public string? Sev { get; set; }
    [BindProperty(SupportsGet = true)] public string? Secao { get; set; }
    public string SecaoAtual => Secao switch
    {
        "panorama" or "servicos" or "simulacao" or "fontes" => Secao,
        "visao-tecnica" when Sessao.PodeEnviar(User) => "visao-tecnica",
        null when Sev is not null && Sessao.PodeEnviar(User) => "visao-tecnica",
        _ => "panorama"
    };
    public Severidade? SeveridadeEscolhida => Enum.TryParse<Severidade>(Sev, out var s) && Enum.IsDefined(s) ? s : null;

    public IActionResult OnGet(string id, int[]? acoesSelecionadas, bool simular = false)
    {
        var lote = lotes.Obter(id);
        if (lote is null) return NotFound();
        Lote = lote;
        if (Secao is null && simular) Secao = "simulacao";
        PdfDisponivel = repositorio.ExisteRelatorio(id, "pdf");
        DiretoriaDisponivel = repositorio.ExisteRelatorio(id, "html", VersaoRelatorio.Diretoria);
        PdfDiretoriaDisponivel = repositorio.ExisteRelatorio(id, "pdf", VersaoRelatorio.Diretoria);
        AvisoConfianca = lote.Avaliacao is { } a ? gerador.AvisoConfianca(a.Score) : null;
        FraseAchados = lote.Avaliacao is { } av ? gerador.FraseAchados(av) : null;
        var resumos = repositorio.Listar();
        Revisoes = resumos.Where(r => r.RevisaoDe == lote.Id).ToList();
        Historico = resumos.Where(r => r.CriadoEmUtc < lote.CriadoEmUtc && r.RevisaoDe is null && r.Id != lote.Id)
            .OrderByDescending(r => r.CriadoEmUtc).Take(5).Append(ResumoLote.De(lote)).OrderBy(r => r.CriadoEmUtc).ToList();
        Aguardando = operacao.Estado().Chamados.Where(c => c.Estado == EstadoChamado.AguardandoAprovacao).ToList();
        if (lote.Avaliacao is not null)
        {
            Selecionadas = simular ? (acoesSelecionadas ?? []).ToHashSet() : new HashSet<int>();
            try
            {
                if (!ModelState.IsValid) throw new ArgumentException("Selecione somente as correções disponíveis neste lote.");
                Simulacao = new SimuladorRemediacao(regras).Simular(lote, Selecionadas);
            }
            catch (ArgumentException ex) { ErroSimulacao = ex.Message; }
        }
        return Page();
    }

    // GET: cálculo sem efeitos; não cria revisão, chamado, relatório ou auditoria de decisão.
    public IActionResult OnGetSimular(string id, int[]? acoesSelecionadas)
    {
        var lote = lotes.Obter(id);
        if (lote is null) return NotFound();
        if (!ModelState.IsValid || lote.Avaliacao is null) return BadRequest(new { erro = "Não foi possível simular este lote." });
        try
        {
            var simulacao = new SimuladorRemediacao(regras).Simular(lote, acoesSelecionadas ?? []);
            return new JsonResult(new { score = simulacao.Score.Valor, faixa = simulacao.Score.Faixa,
                resumo = ResumoSimulacao(simulacao), acoes = simulacao.Acoes });
        }
        catch (ArgumentException ex) { return BadRequest(new { erro = ex.Message }); }
    }

    public static string ResumoSimulacao(SimulacaoRemediacao s)
    {
        if (s.Acoes == 0) return "Selecione as correções para comparar o cenário.";
        var esforcos = string.Join(" · ", s.Esforcos.OrderBy(e => e.Key).Select(e =>
            $"{Formatar.Qtd(e.Value, "ação", "ações")} de esforço {NomeEsforco(e.Key)}"));
        return $"{Formatar.Qtd(s.Falhas, "falha corrigida", "falhas corrigidas")} em {Formatar.Qtd(s.Maquinas, "máquina", "máquinas")} · {esforcos}. Maior prazo individual: {Formatar.Qtd(s.PrazoDias, "dia", "dias")}.";
    }

    public static string NomeEsforco(Nivel nivel) => nivel switch { Nivel.Alto => "alto", Nivel.Medio => "médio", _ => "baixo" };
    public string ContextoScore()
    {
        if (Lote.Comparacao is not { } c) return "primeira análise";
        var pontos = Math.Round(Math.Abs(c.Diferenca));
        var quem = Lote.RevisaoDe is not null ? "o lote original" : "a análise anterior";
        return c.Diferenca >= 0.5 ? $"pior que {quem} (+{pontos:0})"
            : c.Diferenca <= -0.5 ? $"melhor que {quem} (−{pontos:0})" : $"igual a {quem}";
    }
}
