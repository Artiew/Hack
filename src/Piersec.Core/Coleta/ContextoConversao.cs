using System.Globalization;
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
    string ambiente,
    Anonimizador anonimizador,
    NormalizadorDatas datas,
    Func<string> novoId)
{
    public const string NaoInformado = "(não informado)";

    public string Arquivo { get; } = arquivo;
    public string Sha256 { get; } = sha256;
    public string Ambiente { get; } = ambiente;
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

    /// <summary>Metadados opcionais de vínculo: só identificadores curtos; texto inválido não vira chave de correlação.</summary>
    public string? ConverterIdentificadorDeclarado(string? valor, long linha, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var identificador = NormalizarAtivo(valor);
        if (identificador.Length > 120 || identificador.Any(c => !char.IsLetterOrDigit(c) && !" ._-".Contains(c)))
        {
            RegistrarLacuna(linha, campo, "identificador declarado inválido: use até 120 letras, números, espaços, ponto, hífen ou sublinhado; não usado para correlação");
            return null;
        }
        return identificador;
    }

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

    /// <summary>Nota CVSS de 0 a 10. Aceita "9.8" e "9,8".</summary>
    public double? ConverterCvss(string? valor, long linha)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            RegistrarLacuna(linha, "cvss", "nota CVSS não informada");
            return null;
        }
        if (!double.TryParse(valor.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var nota))
        {
            RegistrarLacuna(linha, "cvss", "nota CVSS não é um número");
            return null;
        }
        if (nota is < 0 or > 10)
        {
            RegistrarLacuna(linha, "cvss", "nota CVSS fora da faixa de 0 a 10");
            return null;
        }
        return nota;
    }

    /// <summary>Número inteiro dentro de uma faixa (ex.: nível do alerta de 0 a 15).</summary>
    public int? ConverterInteiro(string? valor, long linha, string campo, int minimo, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            RegistrarLacuna(linha, campo, $"{campo} não informado");
            return null;
        }
        if (!int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero) || numero < minimo || numero > maximo)
        {
            RegistrarLacuna(linha, campo, $"{campo} fora da faixa de {minimo} a {maximo}");
            return null;
        }
        return numero;
    }
}
