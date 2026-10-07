using Piersec.Core.Armazenamento;
using Piersec.Core.Modelo;
using Piersec.Core.Pipeline;
using Piersec.Core.Relatorio;

namespace Piersec.Tests;

/// <summary>"Discordar da severidade": revisão com motivo registrado e recálculo; o lote original nunca muda.</summary>
public class RevisaoTests : IDisposable
{
    private const string CabecalhoVuln =
        "agent.name,host.os.name,package.name,package.version,vulnerability.id,vulnerability.description,vulnerability.severity,vulnerability.score.base,vulnerability.reference";

    private readonly string _pasta = Directory.CreateTempSubdirectory("piersec-rev-").FullName;
    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    private static readonly Lote Original = Validado(Fabrica.ProcessadorReal().Processar([Fabrica.Csv("vuln.csv", CabecalhoVuln,
        "DB-01,Windows Server 2019,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref",
        "PC-01,Windows 10,zoom,5.0,CVE-2099-0002,d,Low,2.0,ref")]));

    private static Lote Validado(Lote l) => l with { Validacao = Fabrica.Validador().Validar(l) };

    private static EventoSeguranca Critico(Lote l) => l.Eventos.Single(e => e.Cve == "CVE-2099-0001");

    private static AjusteSeveridade Ajuste(Lote l, Severidade para, string motivo = "máquina desligada desde setembro, aguardando descarte") =>
        new(Critico(l).Id, Critico(l).Severidade, para, motivo, "ana", Fabrica.Agora, Critico(l).Justificativa!);

    private CicloAnalise Ciclo() =>
        new(Fabrica.ProcessadorReal(), new RepositorioLotes(_pasta), Fabrica.Gerador(), new ConversorPdf(), Fabrica.Validador(), relogio: new RelogioFixo(Fabrica.Agora));

    [Fact]
    public void Revisao_recalcula_tudo_e_o_lote_original_nao_muda()
    {
        var revisao = Validado(Fabrica.ProcessadorReal().Revisar(Original, [Ajuste(Original, Severidade.Baixa)]));

        Assert.NotEqual(Original.Id, revisao.Id);
        Assert.Equal(Original.Id, revisao.RevisaoDe);
        Assert.Equal(Severidade.Critica, Critico(Original).Severidade);                     // original intacto
        Assert.Equal(Severidade.Baixa, Critico(revisao).Severidade);
        Assert.StartsWith("Ajustada por ana: máquina desligada", Critico(revisao).Justificativa);
        Assert.True(revisao.Avaliacao!.Score.Valor < Original.Avaliacao!.Score.Valor);   // recalculado de verdade
        Assert.False(revisao.Servicos.Single(s => s.Id == "dados-clientes").EmRisco);       // e o serviço também
        Assert.True(revisao.Validacao!.Verificacoes.Single(v => v.Id == "V15").Aprovada);
        Assert.Equal(Original.Eventos.Count, revisao.Eventos.Count);
    }

    [Fact]
    public async Task Sem_motivo_ou_sem_mudanca_nada_e_criado()
    {
        var ciclo = Ciclo();
        var id = Critico(Original).Id;

        var semMotivo = await ciclo.RevisarAsync(Original, id, Severidade.Baixa, "  curto  ", "ana", TestContext.Current.CancellationToken);
        var igual = await ciclo.RevisarAsync(Original, id, Severidade.Critica, "motivo suficientemente longo", "ana", TestContext.Current.CancellationToken);
        var inexistente = await ciclo.RevisarAsync(Original, "EV-99999", Severidade.Baixa, "motivo suficientemente longo", "ana", TestContext.Current.CancellationToken);

        Assert.False(semMotivo.Ok);
        Assert.Contains("motivo", semMotivo.Mensagem);
        Assert.False(igual.Ok);
        Assert.False(inexistente.Ok);
        Assert.Empty(new RepositorioLotes(_pasta).Listar());                                 // nada foi gravado
    }

    [Fact]
    public async Task Revisao_pelo_ciclo_compara_com_o_original_filtra_credencial_e_vai_para_o_relatorio()
    {
        var conversor = new ConversorPdf();
        Assert.SkipWhen(conversor.Navegador is null, "Nenhum Edge/Chrome nesta máquina.");

        var r = await Ciclo().RevisarAsync(Original, Critico(Original).Id, Severidade.Media,
            "servidor isolado da internet; senha=Segredo123 anotada no chamado", "ana", TestContext.Current.CancellationToken);

        Assert.True(r.Ok, r.Mensagem);
        var revisao = r.Ciclo!.Lote;
        Assert.Equal(Original.Id, revisao.Comparacao!.LoteAnteriorId);                       // "antes" = o original
        Assert.DoesNotContain("Segredo123", revisao.Ajustes.Single().Motivo);
        Assert.True(revisao.Validacao!.TudoCerto);
        var html = System.Text.Encoding.UTF8.GetString(new RepositorioLotes(_pasta).LerRelatorio(revisao.Id, "html")!);
        Assert.Contains($"Esta é uma REVISÃO do lote {Original.Id[..8]}", html);
        Assert.Contains("I. Severidades ajustadas por pessoas", html);
        Assert.Contains("servidor isolado da internet", html);
    }

    [Fact]
    public void Revisao_de_revisao_mantem_o_lote_raiz_e_o_que_a_plataforma_tinha_dito()
    {
        var primeira = Fabrica.ProcessadorReal().Revisar(Original, [Ajuste(Original, Severidade.Baixa)]);
        var segundoAjuste = Ajuste(primeira, Severidade.Media, "na verdade ainda responde na rede interna") with
        {
            JustificativaOriginal = primeira.Ajustes.Single().JustificativaOriginal,
        };

        var segunda = Fabrica.ProcessadorReal().Revisar(primeira, [segundoAjuste]);

        Assert.Equal(Original.Id, segunda.RevisaoDe);
        Assert.Equal(Severidade.Media, Critico(segunda).Severidade);
        Assert.Equal(Critico(Original).Justificativa, segunda.Ajustes.Single().JustificativaOriginal);
    }

    [Fact]
    public void V15_reprova_ajuste_sem_registro_ou_que_nao_bate_com_o_evento()
    {
        var revisao = Fabrica.ProcessadorReal().Revisar(Original, [Ajuste(Original, Severidade.Baixa)]);

        var semRegistro = revisao with { Ajustes = [] };
        Assert.False(Fabrica.Validador().Validar(semRegistro).Verificacoes.Single(v => v.Id == "V15").Aprovada);

        var naoBate = revisao with { Ajustes = [revisao.Ajustes[0] with { Para = Severidade.Alta }] };
        Assert.Contains("difere do ajuste", Fabrica.Validador().Validar(naoBate).Verificacoes.Single(v => v.Id == "V15").Detalhe);

        Assert.Equal("nenhuma severidade foi mudada por pessoas neste lote",
            Original.Validacao!.Verificacoes.Single(v => v.Id == "V15").Detalhe);
        Assert.Null(GeradorRelatorioHtml.AvisoRevisao(Original));
    }
}
