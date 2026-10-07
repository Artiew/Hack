using System.Security.Cryptography;
using System.Text;
using Piersec.Core.Analise;
using Piersec.Core.Coleta;
using Piersec.Core.Modelo;
using Piersec.Core.Padronizacao;
using Piersec.Core.Relatorio;

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
    ConjuntoRegras regras,
    TimeProvider relogio)
{
    public const long TamanhoMaximoBytes = 10 * 1024 * 1024;
    public const int MaximoArquivos = 20;
    public const string ArquivoDoLote = "(lote)";

    private readonly IReadOnlyList<IAdaptadorFonte> _adaptadores = adaptadores.ToList();
    private readonly ClassificadorSeveridade _classificador = new(regras.Severidade);
    private readonly ClassificadorAtivos _tiposDeAtivo = new(regras.Ativos);
    private readonly MotorCorrelacao _motor = new();
    private readonly PlanejadorAvaliacao _planejador = new(regras, new CalculadoraScore(regras.Score, regras.Ativos));

    /// <summary>
    /// Etapas: coleta e padronização (por arquivo) → severidade e tipo de máquina → correlação →
    /// score, riscos principais e plano de ação (no lote inteiro).
    /// </summary>
    public Lote Processar(IReadOnlyList<ArquivoEnviado> arquivos)
    {
        var coletado = Coletar(arquivos);

        var lacunas = new List<Lacuna>(coletado.Lacunas);
        var eventos = new List<EventoSeguranca>(coletado.Eventos.Count);
        foreach (var bruto in coletado.Eventos)
        {
            var (evento, lacuna) = _classificador.Classificar(bruto);
            eventos.Add(evento with { TipoAtivo = _tiposDeAtivo.Classificar(evento) });
            if (lacuna is not null)
                lacunas.Add(lacuna);
        }

        var (correlacoes, avaliadas) = _motor.Correlacionar(eventos, regras.Correlacoes);
        var tiposMedidos = coletado.Arquivos.Where(a => a.TipoEvento is not null).Select(a => a.TipoEvento!).ToHashSet();
        var avaliacao = _planejador.Avaliar(eventos, correlacoes, tiposMedidos);
        var avisosAmbiente = VerificadorAmbientes.Verificar(eventos, coletado.Arquivos);
        lacunas.AddRange(avisosAmbiente.Select(a => new Lacuna(ArquivoDoLote, null, "ambiente", a)));
        var fontesAusentes = FontesAusentes(coletado.Arquivos);
        lacunas.AddRange(fontesAusentes.Select(f => new Lacuna(ArquivoDoLote, null, "fonte", f)));

        return new Lote
        {
            Id = Guid.NewGuid().ToString("N"),
            CriadoEmUtc = relogio.GetUtcNow(),
            Arquivos = coletado.Arquivos,
            Eventos = eventos,
            Lacunas = lacunas,
            Correlacoes = correlacoes,
            RegrasAvaliadas = avaliadas,
            Avaliacao = avaliacao,
            AvisosAmbiente = avisosAmbiente,
            FontesAusentes = fontesAusentes,
        };
    }

    private sealed record Coleta(List<ResultadoArquivo> Arquivos, List<EventoSeguranca> Eventos, List<Lacuna> Lacunas);

    private Coleta Coletar(IReadOnlyList<ArquivoEnviado> arquivos)
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

            var avisos = new List<string>(tabela.Avisos);
            var linhasDeDados = tabela.Linhas.Count + tabela.Desalinhadas.Count + tabela.Erros.Count;
            tabela = tabela.ComReparos(adaptador.RepararLinha, out var reparadas);
            if (reparadas > 0)
                avisos.Add($"{Formatar.Numero(reparadas)} linha(s) corrigida(s): {adaptador.DescricaoReparo ?? "colunas a mais remontadas pelo adaptador da fonte"}");

            var lacunasArquivo = new List<Lacuna>();
            if (avisoCodificacao is not null)
                lacunasArquivo.Add(new Lacuna(nome, null, "arquivo", avisoCodificacao));
            lacunasArquivo.AddRange(adaptador.LimitacoesDaFonte.Select(l => new Lacuna(nome, null, "fonte", l)));
            if (linhasDeDados == 0)
                lacunasArquivo.Add(new Lacuna(nome, null, "arquivo",
                    "arquivo sem nenhum registro: isso NÃO significa ausência de problemas, e sim ausência de dados"));
            if (linhasDeDados >= 1000 && linhasDeDados % 1000 == 0)
                lacunasArquivo.Add(new Lacuna(nome, null, "arquivo",
                    $"o arquivo tem exatamente {Formatar.Numero(linhasDeDados)} linhas de dados: número redondo pode indicar que a exportação foi cortada no limite da ferramenta"));

            var contexto = new ContextoConversao(nome, sha!, Ambiente(nome), anonimizador, datas, () => $"EV-{++sequencia:00000}");
            var (convertidos, repetidos) = SemRepetidos(adaptador.Converter(tabela, contexto));
            eventos.AddRange(convertidos);
            if (repetidos.Count > 0)
            {
                var (repetida, primeira) = repetidos[0];
                avisos.Add($"{Formatar.Qtd(repetidos.Count, "linha repetida foi contada", "linhas repetidas foram contadas")} uma vez só " +
                           $"(ex.: a linha {repetida.Evidencia.Linha} é idêntica à linha {primeira.Evidencia.Linha})");
                lacunasArquivo.AddRange(repetidos.Select(r => new Lacuna(nome, r.Repetido.Evidencia.Linha, "linha",
                    MotivoLinhaRepetida)));
            }

            lacunasArquivo.AddRange(tabela.Erros.Select(e => new Lacuna(nome, e.Linha, "linha", e.Motivo)));
            lacunasArquivo.AddRange(tabela.Desalinhadas.Select(l => new Lacuna(nome, l.Numero, "linha",
                $"linha com {l.Campos.Length} colunas; o cabeçalho tem {tabela.Cabecalho.Count} (não lida para não desalinhar os dados)")));
            lacunasArquivo.AddRange(contexto.Lacunas);
            lacunas.AddRange(lacunasArquivo);

            resultados.Add(new ResultadoArquivo
            {
                Nome = nome,
                TamanhoBytes = arquivo.Tamanho,
                Sha256 = sha,
                Fonte = adaptador.Nome,
                TipoEvento = adaptador.TipoDeEvento,
                RegistrosLidos = convertidos.Count,
                RegistrosRejeitados = tabela.Erros.Count + tabela.Desalinhadas.Count,
                RegistrosRepetidos = repetidos.Count,
                Lacunas = lacunasArquivo.Count,
                Avisos = avisos,
            });
        }

        if (resultados.All(r => r.Fonte is null))
            lacunas.Add(new Lacuna(ArquivoDoLote, null, "lote",
                "nenhum arquivo foi reconhecido: não há dados para avaliar (isso NÃO significa ambiente sem problemas)"));

        return new Coleta(resultados, eventos, lacunas);
    }

    /// <summary>
    /// Fonte esperada ausente (RF7): a plataforma sabe ler este tipo de arquivo, mas nenhum veio no lote.
    /// Sem isso, "nenhum alerta" poderia ser lido como "nenhum ataque", quando na verdade ninguém olhou.
    /// Só avisa se ALGUM arquivo foi reconhecido (lote todo sem dados já tem a sua própria lacuna).
    /// </summary>
    private List<string> FontesAusentes(IReadOnlyList<ResultadoArquivo> arquivos)
    {
        var recebidos = arquivos.Where(a => a.Fonte is not null && a.TipoEvento is not null).Select(a => a.TipoEvento!).ToHashSet();
        if (recebidos.Count == 0)
            return [];
        return _adaptadores.Where(a => a.TipoDeEvento is not null && !recebidos.Contains(a.TipoDeEvento))
            .GroupBy(a => a.TipoDeEvento!)
            .Select(g =>
            {
                var fator = regras.Score.Fatores.FirstOrDefault(f => f.TipoDeEvento == g.Key);
                var pergunta = fator is null ? "" : $" Sem ele, a análise não responde: \"{fator.Pergunta}\"";
                return $"Faltou o arquivo de {TiposEvento.Descricao(g.Key)}.{pergunta} " +
                       "Arquivo que não veio NÃO significa que não há problemas.";
            }).ToList();
    }

    public const string MotivoLinhaRepetida =
        "linha idêntica a uma linha anterior do mesmo arquivo: contada uma vez só para não inflar os números";

    private static readonly Evidencia SemEvidencia = new("", "", 0);

    /// <summary>
    /// Duplicidade: duas linhas do mesmo arquivo que viram eventos idênticos em TUDO (máquina, falha, programa, versão,
    /// nota, data, regra, descrição...) são o mesmo registro exportado duas vezes. Fica a primeira; as outras viram lacuna.
    /// Linhas parecidas mas com qualquer campo diferente (ex.: outra data) continuam sendo eventos separados.
    /// </summary>
    public static (List<EventoSeguranca> Unicos, List<(EventoSeguranca Repetido, EventoSeguranca Original)> Repetidos)
        SemRepetidos(IReadOnlyList<EventoSeguranca> eventos)
    {
        var unicos = new List<EventoSeguranca>(eventos.Count);
        var repetidos = new List<(EventoSeguranca, EventoSeguranca)>();
        var vistos = new Dictionary<EventoSeguranca, EventoSeguranca>();
        foreach (var e in eventos)
        {
            var conteudo = e with { Id = "", Evidencia = SemEvidencia }; // compara só o conteúdo, não a posição no arquivo
            if (vistos.TryGetValue(conteudo, out var original))
                repetidos.Add((e, original));
            else
            {
                vistos[conteudo] = e;
                unicos.Add(e);
            }
        }
        return (unicos, repetidos);
    }

    /// <summary>Premissa da Fase 2: os dados não trazem cliente/ambiente, então o ambiente é o nome do arquivo.</summary>
    public static string Ambiente(string nomeArquivo) => Path.GetFileNameWithoutExtension(nomeArquivo);

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

    // Windows-1252 é o padrão do Excel/Windows em português. Diferente do Latin-1, ele tem aspas curvas, travessão etc.
    private static readonly Encoding Windows1252 = CarregarWindows1252();

    private static Encoding CarregarWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    /// <summary>Tenta UTF-8; se não for, lê como Windows-1252 (CSV exportado no Windows) e avisa.</summary>
    private static string Decodificar(byte[] conteudo, out string? aviso)
    {
        aviso = null;
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(conteudo);
        }
        catch (DecoderFallbackException)
        {
            aviso = "arquivo não está em UTF-8; lido como Windows-1252, o padrão do Windows (confira acentos e símbolos)";
            return Windows1252.GetString(conteudo);
        }
    }
}
