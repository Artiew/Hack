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
    public void Linha_com_colunas_a_mais_vira_erro_com_o_numero_da_linha()
    {
        var tabela = LeitorCsv.Ler("a,b\n1,2\n3,4,5\n6,7");

        Assert.Equal(2, tabela.Linhas.Count);
        var erro = Assert.Single(tabela.Erros);
        Assert.Equal(3, erro.Linha);
        Assert.Contains("3 colunas", erro.Motivo);
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
