using Piersec.Core.Coleta;
using Piersec.Core.Modelo;

namespace Piersec.Core.Analise;

/// <summary>Arquivos do lote de um mesmo tipo (ex.: os dois de vulnerabilidades) e quantas máquinas eles citam.</summary>
public sealed record GrupoDeArquivos(string TipoEvento, string Descricao, IReadOnlyList<string> Arquivos, int Maquinas);

/// <summary>
/// "De onde vêm os dados": separa os arquivos por tipo e junta, numa lista só, os avisos de que eles não se ligam
/// (fonte ausente, arquivos do mesmo tipo sem máquina em comum, alertas sem ligação com as máquinas com falhas).
/// Mesma lista na tela e no relatório, para os dois nunca dizerem coisas diferentes.
/// </summary>
public static class OrigemDosDados
{
    public const string AlertasSemLigacao =
        "Não foi possível ligar os alertas do firewall às máquinas com falhas: os arquivos não têm uma informação em comum (como o nome da máquina atacada).";

    public static IReadOnlyList<GrupoDeArquivos> Grupos(Lote lote)
    {
        var maquinasPorArquivo = lote.Eventos
            .Where(e => e.Ativo != ContextoConversao.NaoInformado)
            .GroupBy(e => e.Evidencia.Arquivo, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Ativo).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

        return lote.Arquivos
            .Where(a => a.TipoEvento is not null)
            .GroupBy(a => a.TipoEvento!, StringComparer.Ordinal)
            .Select(g =>
            {
                var maquinas = g.SelectMany(a => maquinasPorArquivo.GetValueOrDefault(a.Nome) ?? []).ToHashSet(StringComparer.Ordinal);
                return new GrupoDeArquivos(g.Key, TiposEvento.Descricao(g.Key), g.Select(a => a.Nome).ToList(), maquinas.Count);
            })
            .ToList();
    }

    public static IReadOnlyList<string> Avisos(Lote lote)
    {
        var avisos = new List<string>(lote.FontesAusentes.Concat(lote.AvisosAmbiente));
        if (lote.RegrasAvaliadas.Any(r => r.MotivoSemOcorrencia?.Contains("0 em comum") == true))
            avisos.Add(AlertasSemLigacao);
        return avisos;
    }
}
