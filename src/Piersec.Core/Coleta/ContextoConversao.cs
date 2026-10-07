using Piersec.Core.Modelo;
using Piersec.Core.Padronizacao;

namespace Piersec.Core.Coleta;

/// <summary>
/// Ferramentas que todo adaptador usa ao converter um arquivo. Cada conversão que falha
/// registra uma lacuna com arquivo, linha e motivo — assim todos os adaptadores tratam erros do mesmo jeito.
/// </summary>
public sealed class ContextoConversao(
    string arquivo,
    string sha256,
    Anonimizador anonimizador,
    NormalizadorDatas datas,
    Func<string> novoId)
{
    public const string NaoInformado = "(não informado)";

    public string Arquivo { get; } = arquivo;
    public string Sha256 { get; } = sha256;
    public List<Lacuna> Lacunas { get; } = [];

    public string NovoId() => novoId();

    public Evidencia Evidencia(long linha) => new(Arquivo, Sha256, linha);

    public void RegistrarLacuna(long linha, string campo, string motivo) =>
        Lacunas.Add(new Lacuna(Arquivo, linha, campo, motivo));

    public string ConverterAtivo(string? valor, long linha)
    {
        var ativo = NormalizarAtivo(valor);
        if (ativo.Length > 0)
            return ativo;
        RegistrarLacuna(linha, "ativo", "ativo não informado");
        return NaoInformado;
    }

    /// <summary>"  srv-fict-01 " → "SRV-FICT-01".</summary>
    public static string NormalizarAtivo(string? valor) =>
        string.Join(' ', (valor ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    public string? ConverterUsuario(string? valor, long linha)
    {
        if (!string.IsNullOrWhiteSpace(valor))
            return anonimizador.Anonimizar(valor);
        RegistrarLacuna(linha, "usuario", "usuário não informado");
        return null;
    }

    public DateTimeOffset? ConverterData(string? valor, long linha)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            RegistrarLacuna(linha, "data", "data não informada");
            return null;
        }
        if (datas.TentarNormalizar(valor, out var utc))
            return utc;
        RegistrarLacuna(linha, "data", "data em formato não reconhecido");
        return null;
    }

    public Severidade? ConverterSeveridade(string? valor, long linha)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            RegistrarLacuna(linha, "severidade", "severidade não informada");
            return null;
        }
        var severidade = ConversorSeveridade.Converter(valor);
        if (severidade is null)
            RegistrarLacuna(linha, "severidade", "severidade de origem fora da tabela de conversão");
        return severidade;
    }
}
