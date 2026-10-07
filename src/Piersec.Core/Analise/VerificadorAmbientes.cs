using Piersec.Core.Coleta;
using Piersec.Core.Modelo;
using Piersec.Core.Relatorio;

namespace Piersec.Core.Analise;

/// <summary>
/// Avisa quando arquivos do MESMO tipo (ex.: dois arquivos de vulnerabilidades) não têm nenhuma máquina em comum:
/// é um forte indício de empresas ou ambientes diferentes misturados no mesmo lote. É aviso, não erro — os dados
/// não dizem a empresa, então a plataforma não tem como ter certeza (pode ser um arquivo dividido em dois).
/// Tipos diferentes (vulnerabilidade × alerta) não entram: neles, máquinas diferentes são esperadas.
/// </summary>
public static class VerificadorAmbientes
{
    /// <summary>Arquivos com menos máquinas que isso não são comparados (pouca informação para concluir).</summary>
    public const int MinimoMaquinasParaComparar = 3;

    public static IReadOnlyList<string> Verificar(IReadOnlyList<EventoSeguranca> eventos, IReadOnlyList<ResultadoArquivo> arquivos)
    {
        var maquinasPorArquivo = eventos
            .Where(e => e.Ativo != ContextoConversao.NaoInformado)
            .GroupBy(e => e.Evidencia.Arquivo, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Ativo).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

        var candidatos = arquivos
            .Where(a => a.TipoEvento is not null && maquinasPorArquivo.TryGetValue(a.Nome, out var m) && m.Count >= MinimoMaquinasParaComparar)
            .ToList();

        var avisos = new List<string>();
        for (var i = 0; i < candidatos.Count; i++)
        {
            for (var j = i + 1; j < candidatos.Count; j++)
            {
                var (a, b) = (candidatos[i], candidatos[j]);
                if (a.TipoEvento != b.TipoEvento)
                    continue;
                var (ma, mb) = (maquinasPorArquivo[a.Nome], maquinasPorArquivo[b.Nome]);
                if (ma.Overlaps(mb))
                    continue;
                avisos.Add(
                    $"Os arquivos \"{a.Nome}\" ({Formatar.Numero(ma.Count)} máquinas) e \"{b.Nome}\" ({Formatar.Numero(mb.Count)} máquinas) " +
                    "não têm nenhuma máquina em comum. Podem ser de empresas ou ambientes diferentes: este relatório junta tudo como se " +
                    "fosse um ambiente só. Se forem empresas diferentes, envie cada uma em um lote separado.");
            }
        }
        return avisos;
    }
}
