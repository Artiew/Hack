using Piersec.Core.Analise;
using Piersec.Core.Modelo;
using Piersec.Core.Relatorio;

namespace Piersec.Tests;

/// <summary>Mapa máquina × serviço da empresa (premissa declarada) e prejuízo potencial em faixas.</summary>
public class ServicosTests
{
    private const string CabecalhoVuln =
        "agent.name,host.os.name,package.name,package.version,vulnerability.id,vulnerability.description,vulnerability.severity,vulnerability.score.base,vulnerability.reference";
    private const string CabecalhoAlertas = "\"timestamp,agent.name,rule.description,data.devname,rule.level,rule.id\"";

    private static Lote LoteVuln(params string[] linhas)
    {
        var lote = Fabrica.ProcessadorReal().Processar([Fabrica.Csv("vuln.csv", [CabecalhoVuln, .. linhas])]);
        return lote with { Validacao = Fabrica.Validador().Validar(lote) };
    }

    [Fact]
    public void Maquina_e_ligada_ao_servico_pelo_nome_e_se_o_nome_nao_disser_nada_pelo_tipo()
    {
        var lote = Fabrica.ProcessadorReal().Processar([
            Fabrica.Csv("vuln.csv", CabecalhoVuln,
                "DB-CLIENTES-01,Windows Server 2019,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref",
                "Banco-Dados,Ubuntu 22.04,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref",       // sem acento, sem maiúscula
                "WEBMAIL-01,Windows Server 2019,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref",
                "SRV-01,Windows Server 2019,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref",
                "PC-01,Windows 10,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref",
                "XYZ,,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref"),                          // sem sistema: tipo desconhecido
            Fabrica.Csv("alertas.csv", CabecalhoAlertas, "\"Oct 7,2026 @ 08:00:00.000,coletor,desc,FW-01,12,100\"")]);

        string ServicoDe(string ativo) => lote.Eventos.First(e => e.Ativo == ativo).Servico!;
        Assert.Equal("dados-clientes", ServicoDe("DB-CLIENTES-01"));
        Assert.Equal("dados-clientes", ServicoDe("BANCO-DADOS"));
        Assert.Equal("email", ServicoDe("WEBMAIL-01"));                 // "mail" vem antes de "web" na lista: primeira regra vence
        Assert.Equal("sistemas-internos", ServicoDe("SRV-01"));
        Assert.Equal("estacoes", ServicoDe("PC-01"));
        Assert.Equal("nao-identificado", ServicoDe("XYZ"));
        Assert.Equal("rede", ServicoDe("FW-01"));
    }

    [Fact]
    public void Servico_em_risco_entra_no_prejuizo_potencial_e_o_que_esta_bem_nao_entra()
    {
        var lote = LoteVuln(
            "DB-01,Windows Server 2019,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref",       // nota 70: em risco
            "PC-01,Windows 10,zoom,5.0,CVE-2099-0002,d,Low,2.0,ref");                       // nota 10: fora

        var dados = lote.Servicos.Single(s => s.Id == "dados-clientes");
        Assert.True(dados.EmRisco);
        Assert.Equal(70, dados.Nota);
        Assert.Equal("Alto", dados.Faixa);
        Assert.Equal(1, dados.MaquinasComFalhaGrave);
        Assert.False(lote.Servicos.Single(s => s.Id == "estacoes").EmRisco);
        Assert.Equal("dados-clientes", lote.Servicos[0].Id);                                 // em risco aparece primeiro

        Assert.Equal(new ResumoImpacto(500_000, 5_000_000, 1), lote.Impacto);
        Assert.Equal("Se esse serviço for atingido, o prejuízo pode ficar entre R$ 500 mil e R$ 5 milhões.", GeradorRelatorioHtml.FrasePrejuizo(lote));
        Assert.Equal("Dados de clientes (alto)", GeradorRelatorioHtml.ServicosEmRisco(lote));
    }

    [Fact]
    public void Sem_servico_em_risco_nao_ha_prejuizo_inventado()
    {
        var lote = LoteVuln("PC-01,Windows 10,zoom,5.0,CVE-2099-0002,d,Low,2.0,ref");

        Assert.Null(lote.Impacto);
        Assert.Null(GeradorRelatorioHtml.FrasePrejuizo(lote));
        Assert.Contains("Nenhum serviço está em risco elevado ou pior", Fabrica.Gerador().Gerar(lote));
    }

    [Fact]
    public void Relatorio_mostra_servicos_na_pagina_1_e_a_secao_2_com_as_premissas()
    {
        var lote = LoteVuln("DB-01,Windows Server 2019,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref");

        var html = Fabrica.Gerador().Gerar(lote);
        var pagina1 = html[html.IndexOf("Resumo para a diretoria", StringComparison.Ordinal)..html.IndexOf("2. Serviços da empresa", StringComparison.Ordinal)];

        Assert.Contains("Serviços em risco:</strong> Dados de clientes (alto).", pagina1);
        Assert.Contains("o prejuízo pode ficar entre R$ 500 mil e R$ 5 milhões", pagina1);
        Assert.Contains("PREMISSA", html);                                                    // a suposição está escrita
        Assert.Contains("não números da empresa", html);
    }

    [Fact]
    public void V14_confere_servicos_e_prejuizo_e_reprova_se_algo_nao_bate()
    {
        var lote = LoteVuln("DB-01,Windows Server 2019,openssl,1.0,CVE-2099-0001,d,Critical,9.8,ref");
        Assert.True(lote.Validacao!.Verificacoes.Single(v => v.Id == "V14").Aprovada);

        var semServico = lote with { Eventos = [.. lote.Eventos.Select(e => e with { Servico = null })] };
        Assert.False(Fabrica.Validador().Validar(semServico).Verificacoes.Single(v => v.Id == "V14").Aprovada);

        var prejuizoErrado = lote with { Impacto = lote.Impacto! with { Maximo = 1 } };
        var v14 = Fabrica.Validador().Validar(prejuizoErrado).Verificacoes.Single(v => v.Id == "V14");
        Assert.False(v14.Aprovada);
        Assert.Contains("prejuízo potencial não bate", v14.Detalhe);
    }

    [Fact]
    public void Regras_de_servicos_invalidas_sao_apontadas()
    {
        var ok = Fabrica.Regras.Servicos;
        Assert.Empty(ok.Problemas(Fabrica.Regras.Ativos));

        var semEstacoes = ok with { Servicos = [.. ok.Servicos.Where(s => s.Id != "estacoes")] };
        Assert.Contains(semEstacoes.Problemas(Fabrica.Regras.Ativos), p => p.Contains("'estacao'"));

        var faixaInvertida = ok with { Servicos = [ok.Servicos[0] with { Impacto = new ImpactoServico(10, 1, "x") }, .. ok.Servicos.Skip(1)] };
        Assert.Contains(faixaInvertida.Problemas(Fabrica.Regras.Ativos), p => p.Contains("máximo ≥ mínimo"));
    }

    [Theory]
    [InlineData(800, "R$ 800")]
    [InlineData(500_000, "R$ 500 mil")]
    [InlineData(1_000_000, "R$ 1 milhão")]
    [InlineData(1_200_000, "R$ 1,2 milhão")]
    [InlineData(2_000_000, "R$ 2 milhões")]
    [InlineData(12_000_000, "R$ 12 milhões")]
    public void Dinheiro_aparece_arredondado_para_leitura(int valor, string esperado) =>
        Assert.Equal(esperado, Formatar.Dinheiro(valor));
}
