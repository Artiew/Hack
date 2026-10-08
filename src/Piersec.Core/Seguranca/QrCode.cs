using System.Globalization;
using System.Text;

namespace Piersec.Core.Seguranca;

/// <summary>
/// Gerador de QR code escrito pela equipe (norma ISO/IEC 18004), para o cadastro do 2FA sem pacote externo (RS6).
/// Só o necessário: modo byte, correção de erro nível M (recupera ~15% de dano), versões 1 a 10 (até 213 bytes).
/// O endereço do 2FA tem uns 100 bytes e cabe na versão 6.
/// </summary>
public sealed class QrCode
{
    public const int VersaoMaxima = 10;

    // Nível M, por versão (índice = versão): bytes de correção por bloco e quantidade de blocos (tabelas 9 da norma).
    private static readonly int[] CorrecaoPorBloco = [0, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26];
    private static readonly int[] Blocos = [0, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5];

    public int Versao { get; }
    public int Tamanho { get; }
    public int Mascara { get; }
    private readonly bool[,] _escuro;   // [linha, coluna]
    private readonly bool[,] _funcao;   // módulos fixos (padrões de posição, tempo, formato), que a máscara não toca

    public bool Escuro(int linha, int coluna) => _escuro[linha, coluna];

    private QrCode(int versao, byte[] palavras, int? mascaraFixa)
    {
        Versao = versao;
        Tamanho = versao * 4 + 17;
        _escuro = new bool[Tamanho, Tamanho];
        _funcao = new bool[Tamanho, Tamanho];
        DesenharPadroesFixos();
        DesenharPalavras(palavras);

        // Escolhe a máscara com menos "penalidade" (manchas e listras que atrapalham a leitura pela câmera).
        var melhor = mascaraFixa ?? Enumerable.Range(0, 8).MinBy(m =>
        {
            AplicarMascara(m);
            DesenharFormato(m);
            var p = Penalidade();
            AplicarMascara(m); // desfaz (XOR)
            return p;
        });
        Mascara = melhor;
        AplicarMascara(melhor);
        DesenharFormato(melhor);
    }

    /// <summary>Gera o QR code do texto (UTF-8). Lança ArgumentException se não couber na versão 10.</summary>
    public static QrCode Gerar(string texto, int? mascara = null)
    {
        var dados = Encoding.UTF8.GetBytes(texto);
        for (var versao = 1; versao <= VersaoMaxima; versao++)
        {
            var bitsContagem = versao <= 9 ? 8 : 16;
            var capacidade = PalavrasDeDados(versao) * 8;
            if (4 + bitsContagem + dados.Length * 8 > capacidade)
                continue;

            var bits = new List<bool>();
            Acrescentar(bits, 0b0100, 4);                // modo byte
            Acrescentar(bits, dados.Length, bitsContagem);
            foreach (var b in dados)
                Acrescentar(bits, b, 8);
            Acrescentar(bits, 0, Math.Min(4, capacidade - bits.Count)); // terminador
            Acrescentar(bits, 0, (8 - bits.Count % 8) % 8);
            var palavras = new List<byte>();
            for (var i = 0; i < bits.Count; i += 8)
                palavras.Add((byte)Enumerable.Range(0, 8).Aggregate(0, (acc, j) => (acc << 1) | (bits[i + j] ? 1 : 0)));
            for (var preenchimento = 0xEC; palavras.Count < capacidade / 8; preenchimento ^= 0xEC ^ 0x11)
                palavras.Add((byte)preenchimento);

            return new QrCode(versao, IntercalarComCorrecao(versao, [.. palavras]), mascara);
        }
        throw new ArgumentException($"Texto grande demais para um QR code versão {VersaoMaxima}.", nameof(texto));
    }

    /// <summary>SVG pronto para pôr na página (quadrados pretos sobre fundo branco, com a margem de 4 módulos da norma).</summary>
    public string Svg(string descricao)
    {
        const int margem = 4;
        var lado = Tamanho + margem * 2;
        var caminho = new StringBuilder();
        for (var y = 0; y < Tamanho; y++)
            for (var x = 0; x < Tamanho; x++)
                if (_escuro[y, x])
                    caminho.Append(CultureInfo.InvariantCulture, $"M{x + margem},{y + margem}h1v1h-1z");
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {lado} {lado}\" role=\"img\" aria-label=\"{System.Net.WebUtility.HtmlEncode(descricao)}\" " +
               $"shape-rendering=\"crispEdges\"><rect width=\"{lado}\" height=\"{lado}\" fill=\"#fff\"/><path d=\"{caminho}\" fill=\"#000\"/></svg>";
    }

    // ---------------- capacidade e correção de erro ----------------

    /// <summary>Módulos disponíveis para dados + correção (tudo menos os padrões fixos), em bits.</summary>
    private static int ModulosDeDados(int versao)
    {
        var resultado = (16 * versao + 128) * versao + 64;
        if (versao >= 2)
        {
            var alinhamentos = versao / 7 + 2;
            resultado -= (25 * alinhamentos - 10) * alinhamentos - 55;
            if (versao >= 7)
                resultado -= 36; // informação de versão
        }
        return resultado;
    }

    private static int PalavrasDeDados(int versao) => ModulosDeDados(versao) / 8 - CorrecaoPorBloco[versao] * Blocos[versao];

    /// <summary>Divide os dados em blocos, calcula a correção (Reed-Solomon) de cada um e intercala tudo, como a norma manda.</summary>
    private static byte[] IntercalarComCorrecao(int versao, byte[] dados)
    {
        var nBlocos = Blocos[versao];
        var nCorrecao = CorrecaoPorBloco[versao];
        var totalPalavras = ModulosDeDados(versao) / 8;
        var curtos = nBlocos - totalPalavras % nBlocos;
        var tamanhoCurto = totalPalavras / nBlocos;
        var divisor = ReedSolomon.Divisor(nCorrecao);

        var blocosDados = new List<byte[]>();
        var blocosCorrecao = new List<byte[]>();
        for (int i = 0, k = 0; i < nBlocos; i++)
        {
            var tamanho = tamanhoCurto - nCorrecao + (i < curtos ? 0 : 1);
            var bloco = dados[k..(k + tamanho)];
            k += tamanho;
            blocosDados.Add(bloco);
            blocosCorrecao.Add(ReedSolomon.Resto(bloco, divisor));
        }

        var resultado = new List<byte>(totalPalavras);
        for (var i = 0; i < blocosDados.Max(b => b.Length); i++)
            foreach (var bloco in blocosDados.Where(b => i < b.Length))
                resultado.Add(bloco[i]);
        for (var i = 0; i < nCorrecao; i++)
            foreach (var bloco in blocosCorrecao)
                resultado.Add(bloco[i]);
        return [.. resultado];
    }

    // ---------------- desenho ----------------

    private void Marcar(int x, int y, bool escuro)
    {
        _escuro[y, x] = escuro;
        _funcao[y, x] = true;
    }

    private void DesenharPadroesFixos()
    {
        for (var i = 0; i < Tamanho; i++)
        {
            Marcar(6, i, i % 2 == 0); // padrões de tempo
            Marcar(i, 6, i % 2 == 0);
        }
        // Três quadrados de posição (com a borda clara de separação).
        foreach (var (cx, cy) in new[] { (3, 3), (Tamanho - 4, 3), (3, Tamanho - 4) })
            for (var dy = -4; dy <= 4; dy++)
                for (var dx = -4; dx <= 4; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x >= 0 && x < Tamanho && y >= 0 && y < Tamanho)
                    {
                        var distancia = Math.Max(Math.Abs(dx), Math.Abs(dy));
                        Marcar(x, y, distancia != 2 && distancia != 4);
                    }
                }
        // Padrões de alinhamento (menos onde cairiam em cima dos quadrados de posição).
        var posicoes = PosicoesAlinhamento();
        for (var i = 0; i < posicoes.Length; i++)
            for (var j = 0; j < posicoes.Length; j++)
            {
                var ultimo = posicoes.Length - 1;
                if ((i == 0 && j == 0) || (i == 0 && j == ultimo) || (i == ultimo && j == 0))
                    continue;
                for (var dy = -2; dy <= 2; dy++)
                    for (var dx = -2; dx <= 2; dx++)
                        Marcar(posicoes[i] + dx, posicoes[j] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
            }
        DesenharFormato(0); // reserva o lugar; o valor certo entra depois de escolher a máscara
        DesenharVersao();
    }

    private int[] PosicoesAlinhamento()
    {
        if (Versao == 1)
            return [];
        var quantidade = Versao / 7 + 2;
        var passo = (Versao * 4 + quantidade * 2 + 1) / (quantidade * 2 - 2) * 2;
        var resultado = new int[quantidade];
        resultado[0] = 6;
        for (int i = quantidade - 1, pos = Tamanho - 7; i >= 1; i--, pos -= passo)
            resultado[i] = pos;
        return resultado;
    }

    /// <summary>15 bits: nível de correção (M = 00) + máscara, protegidos por BCH, gravados em dois lugares.</summary>
    public static int BitsFormato(int mascara)
    {
        var dados = (0b00 << 3) | mascara;
        var resto = dados;
        for (var i = 0; i < 10; i++)
            resto = (resto << 1) ^ ((resto >> 9) * 0x537);
        return ((dados << 10) | resto) ^ 0x5412;
    }

    private void DesenharFormato(int mascara)
    {
        var bits = BitsFormato(mascara);
        bool Bit(int i) => ((bits >> i) & 1) != 0;
        for (var i = 0; i <= 5; i++)
            Marcar(8, i, Bit(i));
        Marcar(8, 7, Bit(6));
        Marcar(8, 8, Bit(7));
        Marcar(7, 8, Bit(8));
        for (var i = 9; i < 15; i++)
            Marcar(14 - i, 8, Bit(i));
        for (var i = 0; i < 8; i++)
            Marcar(Tamanho - 1 - i, 8, Bit(i));
        for (var i = 8; i < 15; i++)
            Marcar(8, Tamanho - 15 + i, Bit(i));
        Marcar(8, Tamanho - 8, true); // módulo sempre escuro
    }

    /// <summary>18 bits com o número da versão (só da 7 em diante), protegidos por BCH.</summary>
    public static int BitsVersao(int versao)
    {
        var resto = versao;
        for (var i = 0; i < 12; i++)
            resto = (resto << 1) ^ ((resto >> 11) * 0x1F25);
        return (versao << 12) | resto;
    }

    private void DesenharVersao()
    {
        if (Versao < 7)
            return;
        var bits = BitsVersao(Versao);
        for (var i = 0; i < 18; i++)
        {
            var bit = ((bits >> i) & 1) != 0;
            int a = Tamanho - 11 + i % 3, b = i / 3;
            Marcar(a, b, bit);
            Marcar(b, a, bit);
        }
    }

    /// <summary>Preenche em zigue-zague, de baixo para cima e de cima para baixo, em colunas de 2, pulando a coluna 6.</summary>
    private void DesenharPalavras(byte[] palavras)
    {
        var i = 0;
        for (var direita = Tamanho - 1; direita >= 1; direita -= 2)
        {
            if (direita == 6)
                direita = 5;
            for (var vertical = 0; vertical < Tamanho; vertical++)
                for (var j = 0; j < 2; j++)
                {
                    var x = direita - j;
                    var subindo = ((direita + 1) & 2) == 0;
                    var y = subindo ? Tamanho - 1 - vertical : vertical;
                    if (!_funcao[y, x] && i < palavras.Length * 8)
                    {
                        _escuro[y, x] = ((palavras[i >> 3] >> (7 - (i & 7))) & 1) != 0;
                        i++;
                    }
                }
        }
    }

    private void AplicarMascara(int mascara)
    {
        for (var y = 0; y < Tamanho; y++)
            for (var x = 0; x < Tamanho; x++)
            {
                if (_funcao[y, x])
                    continue;
                var inverter = mascara switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => (x / 3 + y / 2) % 2 == 0,
                    5 => x * y % 2 + x * y % 3 == 0,
                    6 => (x * y % 2 + x * y % 3) % 2 == 0,
                    _ => ((x + y) % 2 + x * y % 3) % 2 == 0,
                };
                _escuro[y, x] ^= inverter;
            }
    }

    /// <summary>Regras de penalidade da norma: sequências longas da mesma cor, blocos 2×2, padrões parecidos com os de posição e desequilíbrio claro/escuro.</summary>
    private int Penalidade()
    {
        var total = 0;
        for (var linha = 0; linha < 2; linha++) // 0 = linhas, 1 = colunas
            for (var a = 0; a < Tamanho; a++)
            {
                var sequencia = 0;
                var anterior = false;
                var linhaBits = new bool[Tamanho];
                for (var b = 0; b < Tamanho; b++)
                {
                    var atual = linha == 0 ? _escuro[a, b] : _escuro[b, a];
                    linhaBits[b] = atual;
                    if (b > 0 && atual == anterior)
                        sequencia++;
                    else
                    {
                        if (sequencia >= 5)
                            total += sequencia - 2;
                        sequencia = 1;
                    }
                    anterior = atual;
                }
                if (sequencia >= 5)
                    total += sequencia - 2;
                // 1:1:3:1:1 com 4 claros de um lado (parece um quadrado de posição)
                for (var b = 0; b + 11 <= Tamanho; b++)
                {
                    bool[] padrao = [true, false, true, true, true, false, true];
                    var bate = Enumerable.Range(0, 7).All(k => linhaBits[b + k] == padrao[k]);
                    if (!bate)
                        continue;
                    var clarosDepois = b + 11 <= Tamanho && Enumerable.Range(7, 4).All(k => !linhaBits[b + k]);
                    var clarosAntes = b >= 4 && Enumerable.Range(b - 4, 4).All(k => !linhaBits[k]);
                    if (clarosDepois || clarosAntes)
                        total += 40;
                }
            }
        for (var y = 0; y < Tamanho - 1; y++)
            for (var x = 0; x < Tamanho - 1; x++)
            {
                var c = _escuro[y, x];
                if (c == _escuro[y, x + 1] && c == _escuro[y + 1, x] && c == _escuro[y + 1, x + 1])
                    total += 3;
            }
        var escuros = 0;
        foreach (var m in _escuro)
            if (m)
                escuros++;
        var percentual = escuros * 100 / (Tamanho * Tamanho);
        total += Math.Abs(percentual - 50) / 5 * 10;
        return total;
    }

    private static void Acrescentar(List<bool> bits, int valor, int quantidade)
    {
        for (var i = quantidade - 1; i >= 0; i--)
            bits.Add(((valor >> i) & 1) != 0);
    }
}

/// <summary>Correção de erro Reed-Solomon no corpo GF(256) da norma (polinômio 0x11D).</summary>
public static class ReedSolomon
{
    public static byte[] Divisor(int grau)
    {
        var resultado = new byte[grau];
        resultado[grau - 1] = 1;
        var raiz = 1;
        for (var i = 0; i < grau; i++)
        {
            for (var j = 0; j < grau; j++)
            {
                resultado[j] = Multiplicar(resultado[j], raiz);
                if (j + 1 < grau)
                    resultado[j] ^= resultado[j + 1];
            }
            raiz = Multiplicar(raiz, 0x02);
        }
        return resultado;
    }

    public static byte[] Resto(byte[] dados, byte[] divisor)
    {
        var resultado = new byte[divisor.Length];
        foreach (var b in dados)
        {
            var fator = b ^ resultado[0];
            Array.Copy(resultado, 1, resultado, 0, resultado.Length - 1);
            resultado[^1] = 0;
            for (var i = 0; i < resultado.Length; i++)
                resultado[i] ^= Multiplicar(divisor[i], fator);
        }
        return resultado;
    }

    private static byte Multiplicar(int x, int y)
    {
        var z = 0;
        for (var i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }
        return (byte)z;
    }
}
