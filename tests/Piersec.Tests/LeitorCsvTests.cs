using Piersec.Core.Coleta;

namespace Piersec.Tests;

public class LeitorCsvTests
{
    [Fact]
    public void Campo_entre_aspas_com_virgula_fica_inteiro()
    {
        var tabela = LeitorCsv.Ler("a,b\n1,\"texto, com vírgula\"");

        Assert.Single(tabela.Linhas);
        Assert.Equal("texto, com vírgula", tabela.Valor(tabela.Linhas[0], "b"));
    }

    [Fact]
    public void Detecta_ponto_e_virgula_como_separador()
    {
        var tabela = LeitorCsv.Ler("a;b;c\n1;2;3");

        Assert.Equal(["a", "b", "c"], tabela.Cabecalho);
        Assert.Equal("3", tabela.Valor(tabela.Linhas[0], "c"));
    }

    [Fact]
    public void Remove_BOM_e_normaliza_nomes_das_colunas()
    {
        var tabela = LeitorCsv.Ler("﻿Data Hora,Ativo,Descrição\n1,2,3");

        Assert.Equal(["data_hora", "ativo", "descricao"], tabela.Cabecalho);
        Assert.Equal(["Data Hora", "Ativo", "Descrição"], tabela.CabecalhoOriginal);
    }

    [Fact]
    public void Linha_com_colunas_a_mais_fica_separada_e_nunca_e_lida_desalinhada()
    {
        var tabela = LeitorCsv.Ler("a,b\n1,2\n3,4,5\n6,7");

        Assert.Equal(2, tabela.Linhas.Count);
        var desalinhada = Assert.Single(tabela.Desalinhadas);
        Assert.Equal(3, desalinhada.Numero);
        Assert.Equal(3, desalinhada.Campos.Length);
    }

    [Fact]
    public void Arquivo_com_cada_linha_embrulhada_em_aspas_e_desembrulhado()
    {
        var tabela = LeitorCsv.Ler("\"a,b,c\"\n\"1,\"\"x, y\"\",3\"\n\"4,5,6\"");

        Assert.Equal(["a", "b", "c"], tabela.Cabecalho);
        Assert.Equal(2, tabela.Linhas.Count);
        Assert.Equal("x, y", tabela.Valor(tabela.Linhas[0], "b"));
        Assert.Contains(LeitorCsv.AvisoLinhasEmbrulhadas, tabela.Avisos);
    }

    [Fact]
    public void Cabecalho_normal_com_aspas_nao_e_confundido_com_linha_embrulhada()
    {
        var tabela = LeitorCsv.Ler("\"a\",\"b\"\n\"1\",\"2\"");

        Assert.Equal(["a", "b"], tabela.Cabecalho);
        Assert.Empty(tabela.Avisos);
    }

    [Fact]
    public void Reparo_aceita_so_linhas_que_ficam_com_o_numero_certo_de_colunas()
    {
        var tabela = LeitorCsv.Ler("a,b\n1,2\n3,4,5\n6,7,8,9");

        var reparada = tabela.ComReparos((_, c) => c.Length == 3 ? [c[0] + c[1], c[2]] : null, out var quantas);

        Assert.Equal(1, quantas);
        Assert.Equal([2L, 3L], reparada.Linhas.Select(l => l.Numero)); // ordem original mantida
        Assert.Equal("34", reparada.Valor(reparada.Linhas[1], "a"));
        Assert.Equal(4, Assert.Single(reparada.Desalinhadas).Numero);
    }

    [Fact]
    public void Numero_da_linha_e_o_do_arquivo_original()
    {
        var tabela = LeitorCsv.Ler("a,b\n1,2\n3,4");

        Assert.Equal([2L, 3L], tabela.Linhas.Select(l => l.Numero));
    }

    [Fact]
    public void Valor_em_branco_ou_coluna_inexistente_retorna_null()
    {
        var tabela = LeitorCsv.Ler("a,b\n1,   ");

        Assert.Null(tabela.Valor(tabela.Linhas[0], "b"));
        Assert.Null(tabela.Valor(tabela.Linhas[0], "nao_existe"));
    }

    [Fact]
    public void Respeita_limite_de_linhas_e_avisa()
    {
        var tabela = LeitorCsv.Ler("a\n1\n2\n3", limiteLinhas: 2);

        Assert.Equal(2, tabela.Linhas.Count);
        Assert.Contains(tabela.Erros, e => e.Motivo.Contains("limite"));
    }
}
