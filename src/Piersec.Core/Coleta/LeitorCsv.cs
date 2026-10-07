using Microsoft.VisualBasic.FileIO;
using Piersec.Core.Padronizacao;

namespace Piersec.Core.Coleta;

/// <summary>Uma linha de dados do CSV, com o número da linha no arquivo original (para evidência).</summary>
public sealed record LinhaCsv(long Numero, string[] Campos);

/// <summary>Linha que não pôde ser lida, com o motivo.</summary>
public sealed record ErroLeitura(long Linha, string Motivo);

/// <summary>Resultado da leitura de um CSV: cabeçalho, linhas válidas e linhas com erro.</summary>
public sealed class TabelaCsv
{
    private readonly Dictionary<string, int> _indice = new();

    public TabelaCsv(IReadOnlyList<string> cabecalhoOriginal, IReadOnlyList<LinhaCsv> linhas, IReadOnlyList<ErroLeitura> erros)
    {
        CabecalhoOriginal = cabecalhoOriginal;
        Cabecalho = cabecalhoOriginal.Select(Texto.NormalizarChave).ToArray();
        Linhas = linhas;
        Erros = erros;
        for (var i = 0; i < Cabecalho.Count; i++)
            _indice.TryAdd(Cabecalho[i], i); // coluna repetida: vale a primeira
    }

    /// <summary>Nomes das colunas como vieram no arquivo.</summary>
    public IReadOnlyList<string> CabecalhoOriginal { get; }

    /// <summary>Nomes das colunas normalizados (ex.: "Data Hora" → "data_hora").</summary>
    public IReadOnlyList<string> Cabecalho { get; }

    public IReadOnlyList<LinhaCsv> Linhas { get; }
    public IReadOnlyList<ErroLeitura> Erros { get; }

    /// <summary>Valor da coluna nesta linha; null se a coluna não existe ou o valor está em branco.</summary>
    public string? Valor(LinhaCsv linha, string colunaNormalizada) =>
        _indice.TryGetValue(colunaNormalizada, out var i) && i < linha.Campos.Length && !string.IsNullOrWhiteSpace(linha.Campos[i])
            ? linha.Campos[i].Trim()
            : null;
}

/// <summary>
/// Lê CSV com segurança: só interpreta texto (nunca executa nada), aceita campos entre aspas,
/// descobre se o separador é vírgula, ponto e vírgula ou tabulação, e limita o número de linhas.
/// Usa o TextFieldParser, que já vem no .NET (nenhuma biblioteca extra).
/// </summary>
public static class LeitorCsv
{
    public const int LimitePadraoLinhas = 200_000;

    public static TabelaCsv Ler(string conteudo, int limiteLinhas = LimitePadraoLinhas)
    {
        conteudo = conteudo.TrimStart('﻿'); // BOM: marca invisível que o Excel coloca no início
        var delimitador = DetectarDelimitador(conteudo);

        using var parser = new TextFieldParser(new StringReader(conteudo))
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true,
        };
        parser.SetDelimiters(delimitador.ToString());

        string[]? cabecalho = null;
        var linhas = new List<LinhaCsv>();
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
            if (linhas.Count >= limiteLinhas)
            {
                erros.Add(new ErroLeitura(numero, $"limite de {limiteLinhas} linhas atingido; o restante do arquivo não foi lido"));
                break;
            }
            if (campos.Length != cabecalho.Length)
            {
                erros.Add(new ErroLeitura(numero, $"linha com {campos.Length} colunas; o cabeçalho tem {cabecalho.Length}"));
                continue;
            }
            linhas.Add(new LinhaCsv(numero, campos));
        }

        return new TabelaCsv(cabecalho ?? [], linhas, erros);
    }

    /// <summary>Escolhe o separador que mais aparece na primeira linha (o cabeçalho).</summary>
    private static char DetectarDelimitador(string conteudo)
    {
        var fim = conteudo.IndexOfAny(['\r', '\n']);
        var primeira = fim < 0 ? conteudo : conteudo[..fim];
        char[] candidatos = [',', ';', '\t'];
        return candidatos.MaxBy(c => primeira.Count(x => x == c));
    }
}
