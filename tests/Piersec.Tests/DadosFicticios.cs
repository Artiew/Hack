namespace Piersec.Tests;

/// <summary>
/// CSVs FICTÍCIOS usados pelos testes, escritos aqui no código como os demais testes já fazem (nenhum arquivo .csv
/// na entrega). Criados pela equipe com as mesmas colunas das fontes; nenhum dado veio dos arquivos oficiais do desafio.
/// Cada linha termina em \n, como nos arquivos originais: número de linha e SHA-256 continuam iguais.
/// </summary>
internal static class DadosFicticios
{
    public static IReadOnlyDictionary<string, string> Arquivos { get; } = new Dictionary<string, string>
    {
        ["alertas-corrompido-ficticio.csv"] = Linhas(
            "\"timestamp,agent.name,rule.description,data.devname,rule.level,rule.id\"",
            "\"Oct 7,2026 @ 08:00:00.000,coletor-fict,Fortigate: acesso bloqueado (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2099 @ 08:00:00.000,coletor-fict,Fortigate: data propositalmente no futuro (ficticio),FW-FICT-01,12,99002\"",
            "\"Oct 7,2026 @ 08:05:00.000,coletor-fict,Fortigate: credencial vazada senha=Ficticia123 (ficticio),FW-FICT-01,7,99003\""),
        ["alertas-ficticio.csv"] = Linhas(
            "\"timestamp,agent.name,rule.description,data.devname,rule.level,rule.id\"",
            "\"Oct 7,2026 @ 08:00:00.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:00:20.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:00:40.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:01:00.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:01:20.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:01:40.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:02:00.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:02:20.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:02:40.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:03:00.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:03:20.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:03:40.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:04:00.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:04:20.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:04:40.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:05:00.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:05:20.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:05:40.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:06:00.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:06:20.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:06:40.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 08:07:00.000,coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001\"",
            "\"Oct 7,2026 @ 09:00:00.000,coletor-fict,\"\"Fortigate: ataque detectado, sessao encerrada (ficticio)\"\",FW-FICT-01,12,99002\"",
            "\"Oct 7,2026 @ 09:30:00.000,coletor-fict,Fortigate: assinatura de ataque grave (ficticio),FW-FICT-01,15,99003\"",
            "\"Oct 7,2026 @ 10:00:00.000,coletor-fict,Fortigate: nivel propositalmente invalido (ficticio),FW-FICT-01,x,99004\"",
            "\"Oct 7,2026 @ 10:30:00.000,coletor-fict,Fortigate: linha com coluna a mais (ficticio),FW-FICT-01,7,99005,extra\""),
        ["exposicao-internet-ficticio.csv"] = Linhas(
            "asset.name,asset.os,internet.exposed,observed_at,environment.id",
            "SRV-DEMO-01,Windows Server 2022,true,2026-10-07T09:00:00-03:00,EMPRESA-FICTICIA",
            "PC-DEMO-01,Windows 11,false,2026-10-07T09:00:00-03:00,EMPRESA-FICTICIA",
            "PC-DEMO-02,Windows 11,false,2026-10-07T09:00:00-03:00,EMPRESA-FICTICIA"),
        ["fonte-desconhecida.csv"] = Linhas(
            "coluna_a,coluna_b",
            "x,y"),
        ["operacao-normal-ficticio.csv"] = Linhas(
            "timestamp,agent.name,rule.description,data.devname,rule.level,rule.id",
            "2026-10-07T08:04:00-03:00,COLETOR-FICTICIO,Login ficticio de administrador bem-sucedido,FW-FICTICIO,1,91001",
            "2026-10-07T08:08:00-03:00,COLETOR-FICTICIO,Sincronizacao ficticia de horario concluida,FW-FICTICIO,0,91002",
            "2026-10-07T08:12:00-03:00,COLETOR-FICTICIO,Login ficticio de administrador bem-sucedido,FW-FICTICIO,1,91003",
            "2026-10-07T08:16:00-03:00,COLETOR-FICTICIO,Sincronizacao ficticia de horario concluida,FW-FICTICIO,0,91004",
            "2026-10-07T08:20:00-03:00,COLETOR-FICTICIO,Login ficticio de administrador bem-sucedido,FW-FICTICIO,1,91005",
            "2026-10-07T08:24:00-03:00,COLETOR-FICTICIO,Sincronizacao ficticia de horario concluida,FW-FICTICIO,0,91006",
            "2026-10-07T08:28:00-03:00,COLETOR-FICTICIO,Login ficticio de administrador bem-sucedido,FW-FICTICIO,1,91007",
            "2026-10-07T08:32:00-03:00,COLETOR-FICTICIO,Sincronizacao ficticia de horario concluida,FW-FICTICIO,0,91008",
            "2026-10-07T08:36:00-03:00,COLETOR-FICTICIO,Login ficticio de administrador bem-sucedido,FW-FICTICIO,1,91009",
            "2026-10-07T08:40:00-03:00,COLETOR-FICTICIO,Sincronizacao ficticia de horario concluida,FW-FICTICIO,0,91010",
            "2026-10-07T08:44:00-03:00,COLETOR-FICTICIO,Login ficticio de administrador bem-sucedido,FW-FICTICIO,1,91011",
            "2026-10-07T08:48:00-03:00,COLETOR-FICTICIO,Sincronizacao ficticia de horario concluida,FW-FICTICIO,0,91012"),
        ["tres-fontes-alertas-ficticio.csv"] = Linhas(
            "timestamp,agent.name,rule.description,data.devname,rule.level,rule.id,destination.asset,environment.id",
            "2026-10-07T09:05:00-03:00,COLETOR-DEMO,Tentativa simulada de exploracao de aplicacao web,FW-DEMO,12,99002,SRV-DEMO-01,EMPRESA-FICTICIA"),
        ["tres-fontes-vulnerabilidades-ficticio.csv"] = Linhas(
            "agent.name,host.os.name,package.name,package.version,vulnerability.id,vulnerability.description,vulnerability.severity,vulnerability.score.base,vulnerability.reference,environment.id",
            "SRV-DEMO-01,Windows Server 2022,servidor-web-ficticio,2.4.1,CVE-2099-4001,Falha ficticia de demonstracao no servidor web,Critical,9.8,referencia-ficticia,EMPRESA-FICTICIA",
            "PC-DEMO-01,Windows 11,leitor-pdf-ficticio,11.0,CVE-2099-4002,Falha ficticia de demonstracao no leitor de PDF,High,7.8,referencia-ficticia,EMPRESA-FICTICIA",
            "PC-DEMO-02,Windows 11,leitor-pdf-ficticio,11.0,CVE-2099-4002,Falha ficticia de demonstracao no leitor de PDF,High,7.8,referencia-ficticia,EMPRESA-FICTICIA"),
        ["vulnerabilidades-ficticio.csv"] = Linhas(
            "agent.name,host.os.name,package.name,package.version,vulnerability.id,vulnerability.description,vulnerability.severity,vulnerability.score.base,vulnerability.reference",
            "FICT-SRV-01,Microsoft Windows Server 2019 (ficticio),pacote-alfa,1.0.2,CVE-2099-0001,\"Falha ficticia no pacote-alfa, permite execucao remota\",Critical,9.8,https://exemplo.invalid/CVE-2099-0001",
            "FICT-SRV-02,Microsoft Windows Server 2019 (ficticio),pacote-alfa,1.0.2,CVE-2099-0001,\"Falha ficticia no pacote-alfa, permite execucao remota\",Critical,9.8,https://exemplo.invalid/CVE-2099-0001",
            "FICT-SRV-03,Microsoft Windows Server 2019 (ficticio),pacote-alfa,1.0.2,CVE-2099-0001,\"Falha ficticia no pacote-alfa, permite execucao remota\",Critical,9.8,https://exemplo.invalid/CVE-2099-0001",
            "FICT-SRV-04,Ubuntu 22.04 LTS (ficticio),pacote-alfa,1.0.2,CVE-2099-0001,\"Falha ficticia no pacote-alfa, permite execucao remota\",Critical,9.8,https://exemplo.invalid/CVE-2099-0001",
            "FICT-SRV-05,Microsoft Windows 10 Pro (ficticio),pacote-alfa,1.0.2,CVE-2099-0001,\"Falha ficticia no pacote-alfa, permite execucao remota\",Critical,9.8,https://exemplo.invalid/CVE-2099-0001",
            "FICT-SRV-06,Microsoft Windows 10 Pro (ficticio),pacote-alfa,1.0.2,CVE-2099-0001,\"Falha ficticia no pacote-alfa, permite execucao remota\",Critical,9.8,https://exemplo.invalid/CVE-2099-0001",
            "FICT-SRV-07,Microsoft Windows 10 Pro (ficticio),pacote-alfa,1.0.2,CVE-2099-0001,\"Falha ficticia no pacote-alfa, permite execucao remota\",Critical,9.8,https://exemplo.invalid/CVE-2099-0001",
            "FICT-SRV-08,Microsoft Windows 10 Pro (ficticio),pacote-alfa,1.0.2,CVE-2099-0001,\"Falha ficticia no pacote-alfa, permite execucao remota\",Critical,9.8,https://exemplo.invalid/CVE-2099-0001",
            "FICT-SRV-09,Microsoft Windows 10 Pro (ficticio),pacote-alfa,1.0.2,CVE-2099-0001,\"Falha ficticia no pacote-alfa, permite execucao remota\",Critical,9.8,https://exemplo.invalid/CVE-2099-0001",
            "FICT-SRV-10,Microsoft Windows 10 Pro (ficticio),pacote-alfa,1.0.2,CVE-2099-0001,\"Falha ficticia no pacote-alfa, permite execucao remota\",Critical,9.8,https://exemplo.invalid/CVE-2099-0001",
            "FICT-SRV-11,Microsoft Windows 10 Pro (ficticio),pacote-alfa,1.0.2,CVE-2099-0001,\"Falha ficticia no pacote-alfa, permite execucao remota\",Critical,9.8,https://exemplo.invalid/CVE-2099-0001",
            "FICT-SRV-12,Microsoft Windows 10 Pro (ficticio),pacote-alfa,1.0.2,CVE-2099-0001,\"Falha ficticia no pacote-alfa, permite execucao remota\",Critical,9.8,https://exemplo.invalid/CVE-2099-0001",
            "FICT-SRV-01,Microsoft Windows Server 2019 (ficticio),pacote-beta,2.3,CVE-2099-0101,Falha ficticia 1 no pacote-beta,Critical,9.1,https://exemplo.invalid/CVE-2099-0101",
            "FICT-SRV-01,Microsoft Windows Server 2019 (ficticio),pacote-beta,2.3,CVE-2099-0102,Falha ficticia 2 no pacote-beta,Critical,9.4,https://exemplo.invalid/CVE-2099-0102",
            "FICT-SRV-01,Microsoft Windows Server 2019 (ficticio),pacote-beta,2.3,CVE-2099-0103,Falha ficticia 3 no pacote-beta,Critical,9.0,https://exemplo.invalid/CVE-2099-0103",
            "FICT-SRV-01,Microsoft Windows Server 2019 (ficticio),pacote-beta,2.3,CVE-2099-0104,Falha ficticia 4 no pacote-beta,Critical,9.9,https://exemplo.invalid/CVE-2099-0104",
            "FICT-SRV-01,Microsoft Windows Server 2019 (ficticio),pacote-beta,2.3,CVE-2099-0105,Falha ficticia 5 no pacote-beta,Critical,10,https://exemplo.invalid/CVE-2099-0105",
            "FICT-SRV-02,Microsoft Windows Server 2019 (ficticio),pacote-gama,3.1,CVE-2099-0201,Falha ficticia de nota alta,Critical,\"7,5\",https://exemplo.invalid/CVE-2099-0201",
            "FICT-SRV-03,Microsoft Windows Server 2019 (ficticio),pacote-gama,3.1,CVE-2099-0202,Falha ficticia de nota media,Critical,5.3,https://exemplo.invalid/CVE-2099-0202",
            "FICT-SRV-04,Ubuntu 22.04 LTS (ficticio),pacote-delta,0.9,CVE-2099-0203,Nota propositalmente invalida,Critical,n/a,https://exemplo.invalid/CVE-2099-0203",
            ",Microsoft Windows 10 Pro (ficticio),pacote-delta,0.9,CVE-2099-0204,Maquina propositalmente em branco,Critical,6.1,https://exemplo.invalid/CVE-2099-0204",
            "FICT-SRV-05,Microsoft Windows 10 Pro (ficticio),pacote-delta,0.9,CVE-2099-0205,\"Texto malicioso de teste: <script>alert('xss')</script>\",Critical,4.2,https://exemplo.invalid/CVE-2099-0205"),
    };

    private static string Linhas(params string[] linhas) => string.Concat(linhas.Select(l => l + "\n"));
}
