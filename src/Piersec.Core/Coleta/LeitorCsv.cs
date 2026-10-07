using Microsoft.VisualBasic.FileIO;
using Piersec.Core.Padronizacao;

namespace Piersec.Core.Coleta;

/// <summary>Uma linha de dados do CSV, com o número da linha no arquivo original (para evidência).</summary>
public sealed record LinhaCsv(long Numero, string[] Campos);

/// <summary>Linha que não pôde ser lida, com o motivo.</summary>
public sealed record ErroLeitura(long Linha, string Motivo);

/// <summary>
/// Resultado da leitura de um CSV: cabeçalho, linhas válidas, linhas com número errado de colunas
/// (que o adaptador da fonte pode tentar consertar) e linhas com erro.
/// </summary>
public sealed class TabelaCsv
{
    private readonly Dictionary<string, int> _indice = new();

    public TabelaCsv(
        IReadOnlyList<string> cabecalhoOriginal,
        IReadOnlyList<LinhaCsv> linhas,
        IReadOnlyList<ErroLeitura> erros,
        IReadOnlyList<LinhaCsv>? desalinhadas = null,
        IReadOnlyList<string>? avisos = null)
    {
        CabecalhoOriginal = cabecalhoOriginal;
        Cabecalho = cabecalhoOriginal.Select(Texto.NormalizarChave).ToArray();
        Linhas = linhas;
        Erros = erros;
        Desalinhadas = desalinhadas ?? [];
        Avisos = avisos ?? [];
        for (var i = 0; i < Cabecalho.Count; i++)
            _indice.TryAdd(Cabecalho[i], i); // coluna repetida: vale a primeira
    }

    /// <summary>Nomes das colunas como vieram no arquivo.</summary>
    public IReadOnlyList<string> CabecalhoOriginal { get; }

    /// <summary>Nomes das colunas normalizados (ex.: "Data Hora" → "data_hora").</summary>
    public IReadOnlyList<string> Cabecalho { get; }

    public IReadOnlyList<LinhaCsv> Linhas { get; }
    public IReadOnlyList<ErroLeitura> Erros { get; }

    /// <summary>Linhas com número de colunas diferente do cabeçalho (ainda não descartadas).</summary>
    public IReadOnlyList<LinhaCsv> Desalinhadas { get; }

    /// <summary>Ajustes feitos durante a leitura, para declarar no relatório.</summary>
    public IReadOnlyList<string> Avisos { get; }

    /// <summary>Valor da coluna nesta linha; null se a coluna não existe ou o valor está em branco.</summary>
    public string? Valor(LinhaCsv linha, string colunaNormalizada) =>
        _indice.TryGetValue(colunaNormalizada, out var i) && i < linha.Campos.Length && !string.IsNullOrWhiteSpace(linha.Campos[i])
            ? linha.Campos[i].Trim()
            : null;

    /// <summary>
    /// Passa cada linha desalinhada pela função de reparo do adaptador. As consertadas (com o número certo de
    /// colunas) entram nas linhas válidas, na ordem original; as outras continuam desalinhadas.
    /// </summary>
    public TabelaCsv ComReparos(Func<IReadOnlyList<string>, string[], string[]?> reparar, out int reparadas)
    {
        var validas = new List<LinhaCsv>(Linhas);
        var restantes = new List<LinhaCsv>();
        reparadas = 0;
        foreach (var linha in Desalinhadas)
        {
            var consertada = reparar(Cabecalho, linha.Campos);
            if (consertada is not null && consertada.Length == Cabecalho.Count)
            {
                validas.Add(linha with { Campos = consertada });
                reparadas++;
            }
            else
            {
                restantes.Add(linha);
            }
        }
        if (reparadas > 0)
            validas.Sort((a, b) => a.Numero.CompareTo(b.Numero));
        return new TabelaCsv(CabecalhoOriginal, validas, Erros, restantes, Avisos);
    }
}

/// <summary>
/// Lê CSV com segurança: só interpreta texto (nunca executa nada), aceita campos entre aspas,
/// descobre o separador (vírgula, ponto e vírgula ou tabulação), desembrulha arquivos em que cada
/// linha inteira vem entre aspas e limita o número de linhas.
/// Usa o TextFieldParser, que já vem no .NET (nenhuma biblioteca extra).
/// </summary>
public static class LeitorCsv
{
    public const int LimitePadraoLinhas = 200_000;

    public const string AvisoLinhasEmbrulhadas =
        "cada linha do arquivo vinha inteira entre aspas; as aspas externas foram removidas antes de separar as colunas";

    public static TabelaCsv Ler(string conteudo, int limiteLinhas = LimitePadraoLinhas)
    {
        conteudo = conteudo.TrimStart('﻿'); // BOM: marca invisível que o Excel coloca no início
        var avisos = new List<string>();
        if (LinhasEmbrulhadas(conteudo))
        {
            conteudo = Desembrulhar(conteudo);
            avisos.Add(AvisoLinhasEmbrulhadas);
        }
        var delimitador = DetectarDelimitador(conteudo);

        using var parser = NovoParser(conteudo, delimitador);
        string[]? cabecalho = null;
        var linhas = new List<LinhaCsv>();
        var desalinhadas = new List<LinhaCsv>();
        var erros = new List<ErroLeitura>();

        while (!parser.EndOfData)
        {
            var numero = parser.LineNumber;
            string[]? campos;
            try
            {
                campos = parser.ReadFields();
            }
            catch (MalformedLineException ex)
            {
                erros.Add(new ErroLeitura(ex.LineNumber, "linha mal formatada (aspas sem fechar?)"));
                continue;
            }
            if (campos is null)
                continue;

            if (cabecalho is null)
            {
                cabecalho = campos;
                continue;
            }
            if (linhas.Count + desalinhadas.Count >= limiteLinhas)
            {
                erros.Add(new ErroLeitura(numero, $"limite de {limiteLinhas} linhas atingido; o restante do arquivo não foi lido"));
                break;
            }
            if (campos.Length != cabecalho.Length)
            {
                desalinhadas.Add(new LinhaCsv(numero, campos));
                continue;
            }
            linhas.Add(new LinhaCsv(numero, campos));
        }

        return new TabelaCsv(cabecalho ?? [], linhas, erros, desalinhadas, avisos);
    }

    /// <summary>
    /// O arquivo está "embrulhado" quando o cabeçalho inteiro é um único campo entre aspas que,
    /// por dentro, tem separadores. Ex.: <c>"timestamp,agent.name,rule.id"</c>.
    /// </summary>
    private static bool LinhasEmbrulhadas(string conteudo)
    {
        var primeira = PrimeiraLinha(conteudo).Trim();
        if (primeira.Length < 2 || primeira[0] != '"' || primeira[^1] != '"')
            return false;

        using var parser = NovoParser(primeira, ',');
        string[]? campos;
        try { campos = parser.ReadFields(); }
        catch (MalformedLineException) { return false; }
        return campos is { Length: 1 } && campos[0].IndexOfAny([',', ';', '\t']) >= 0;
    }

    /// <summary>Remove as aspas externas de cada linha e desfaz o escape interno ("" → ").</summary>
    private static string Desembrulhar(string conteudo)
    {
        var linhas = conteudo.Split('\n');
        for (var i = 0; i < linhas.Length; i++)
        {
            var linha = linhas[i].TrimEnd('\r');
            if (linha.Length >= 2 && linha[0] == '"' && linha[^1] == '"')
                linha = linha[1..^1].Replace("\"\"", "\"");
            linhas[i] = linha;
        }
        return string.Join('\n', linhas);
    }

    private static TextFieldParser NovoParser(string conteudo, char delimitador)
    {
        var parser = new TextFieldParser(new StringReader(conteudo))
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true,
        };
        parser.SetDelimiters(delimitador.ToString());
        return parser;
    }

    private static string PrimeiraLinha(string conteudo)
    {
        var fim = conteudo.IndexOfAny(['\r', '\n']);
        return fim < 0 ? conteudo : conteudo[..fim];
    }

    /// <summary>Escolhe o separador que mais aparece na primeira linha (o cabeçalho).</summary>
    private static char DetectarDelimitador(string conteudo)
    {
        var primeira = PrimeiraLinha(conteudo);
        char[] candidatos = [',', ';', '\t'];
        return candidatos.MaxBy(c => primeira.Count(x => x == c));
    }
}
