using System.Security.Cryptography;
using System.Text;
using Piersec.Core.Coleta;
using Piersec.Core.Modelo;
using Piersec.Core.Padronizacao;

namespace Piersec.Core.Pipeline;

/// <summary>
/// Um arquivo recebido. <paramref name="Conteudo"/> é nulo quando o arquivo foi recusado
/// antes de ser lido (ex.: grande demais) — assim não carregamos na memória o que vamos recusar.
/// </summary>
public sealed record ArquivoEnviado(string Nome, long Tamanho, byte[]? Conteudo)
{
    public static ArquivoEnviado De(string nome, byte[] conteudo) => new(nome, conteudo.LongLength, conteudo);
}

/// <summary>
/// Etapas coleta + padronização: valida cada arquivo, escolhe o adaptador pela 1ª linha
/// e junta tudo em um único lote.
/// </summary>
public sealed class ProcessadorLote(
    IEnumerable<IAdaptadorFonte> adaptadores,
    Anonimizador anonimizador,
    NormalizadorDatas datas,
    TimeProvider relogio)
{
    public const long TamanhoMaximoBytes = 10 * 1024 * 1024;
    public const int MaximoArquivos = 20;
    public const string ArquivoDoLote = "(lote)";

    private readonly IReadOnlyList<IAdaptadorFonte> _adaptadores = adaptadores.ToList();

    public Lote Processar(IReadOnlyList<ArquivoEnviado> arquivos)
    {
        var resultados = new List<ResultadoArquivo>();
        var eventos = new List<EventoSeguranca>();
        var lacunas = new List<Lacuna>();
        var jaVistos = new Dictionary<string, string>(); // sha256 → nome do primeiro arquivo
        var sequencia = 0;

        foreach (var (arquivo, posicao) in arquivos.Select((a, i) => (a, i)))
        {
            var nome = NomeSeguro(arquivo.Nome);
            var sha = arquivo.Conteudo is null ? null : Convert.ToHexStringLower(SHA256.HashData(arquivo.Conteudo));

            var motivo = posicao >= MaximoArquivos
                ? $"limite de {MaximoArquivos} arquivos por lote"
                : ValidarArquivo(nome, arquivo);
            if (motivo is null && sha is not null && jaVistos.TryGetValue(sha, out var original))
                motivo = $"conteúdo idêntico a \"{original}\"; ignorado para não contar duas vezes";

            if (motivo is not null)
            {
                resultados.Add(Recusado(nome, arquivo.Tamanho, sha, motivo));
                lacunas.Add(new Lacuna(nome, null, "arquivo", motivo));
                continue;
            }
            jaVistos[sha!] = nome;

            var texto = Decodificar(arquivo.Conteudo!, out var avisoCodificacao);
            var tabela = LeitorCsv.Ler(texto);
            var adaptador = _adaptadores.FirstOrDefault(a => a.Reconhece(tabela.Cabecalho));
            if (adaptador is null)
            {
                var colunas = string.Join(", ", tabela.CabecalhoOriginal.Take(12));
                motivo = $"cabeçalho não corresponde a nenhuma fonte conhecida (colunas: {colunas})";
                resultados.Add(Recusado(nome, arquivo.Tamanho, sha, motivo));
                lacunas.Add(new Lacuna(nome, null, "arquivo", motivo));
                continue;
            }

            var contexto = new ContextoConversao(nome, sha!, anonimizador, datas, () => $"EV-{++sequencia:00000}");
            if (avisoCodificacao is not null)
                lacunas.Add(new Lacuna(nome, null, "arquivo", avisoCodificacao));

            var convertidos = adaptador.Converter(tabela, contexto);
            eventos.AddRange(convertidos);
            lacunas.AddRange(tabela.Erros.Select(e => new Lacuna(nome, e.Linha, "linha", e.Motivo)));
            lacunas.AddRange(contexto.Lacunas);

            resultados.Add(new ResultadoArquivo
            {
                Nome = nome,
                TamanhoBytes = arquivo.Tamanho,
                Sha256 = sha,
                Fonte = adaptador.Nome,
                RegistrosLidos = convertidos.Count,
                RegistrosRejeitados = tabela.Erros.Count,
                Lacunas = tabela.Erros.Count + contexto.Lacunas.Count,
            });
        }

        if (resultados.All(r => r.Fonte is null))
            lacunas.Add(new Lacuna(ArquivoDoLote, null, "lote",
                "nenhum arquivo foi reconhecido: não há dados para avaliar (isso NÃO significa ambiente sem problemas)"));

        return new Lote
        {
            Id = Guid.NewGuid().ToString("N"),
            CriadoEmUtc = relogio.GetUtcNow(),
            Arquivos = resultados,
            Eventos = eventos,
            Lacunas = lacunas,
        };
    }

    private static ResultadoArquivo Recusado(string nome, long tamanho, string? sha, string motivo) => new()
    {
        Nome = nome,
        TamanhoBytes = tamanho,
        Sha256 = sha,
        MotivoNaoProcessado = motivo,
    };

    /// <summary>Tira caminhos do nome (evita "..\..\arquivo") e limita o tamanho.</summary>
    public static string NomeSeguro(string? nome)
    {
        var limpo = Path.GetFileName((nome ?? "").Replace('\\', '/').Split('/').Last()).Trim();
        if (limpo.Length > 120)
            limpo = limpo[..120];
        return limpo.Length == 0 ? "(sem nome)" : limpo;
    }

    private static string? ValidarArquivo(string nome, ArquivoEnviado arquivo)
    {
        if (!nome.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return "extensão não permitida (só .csv)";
        if (arquivo.Tamanho > TamanhoMaximoBytes || arquivo.Conteudo is null)
            return $"arquivo maior que o limite de {TamanhoMaximoBytes / (1024 * 1024)} MB";
        if (arquivo.Conteudo.Length == 0)
            return "arquivo vazio";
        if (arquivo.Conteudo.AsSpan(0, Math.Min(arquivo.Conteudo.Length, 8192)).Contains((byte)0))
            return "conteúdo binário: não parece um CSV de texto";
        return null;
    }

    /// <summary>Tenta UTF-8; se não for, lê como Latin-1 (comum em CSV exportado pelo Excel) e avisa.</summary>
    private static string Decodificar(byte[] conteudo, out string? aviso)
    {
        aviso = null;
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(conteudo);
        }
        catch (DecoderFallbackException)
        {
            aviso = "arquivo não está em UTF-8; lido como Latin-1 (acentos podem ter sido trocados)";
            return Encoding.Latin1.GetString(conteudo);
        }
    }
}
