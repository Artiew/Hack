# Uso: .\exemplos\gerar-exemplos.ps1   (regrava os CSVs ficticios desta pasta)
param([string]$Destino = $PSScriptRoot)
# Gera CSVs FICTÍCIOS no mesmo formato das fontes do desafio (inclusive os defeitos de exportação).
$utf8 = New-Object System.Text.UTF8Encoding($false)

# ---------- Vulnerabilidades ----------
$v = [System.Collections.Generic.List[string]]::new()
$v.Add('agent.name,host.os.name,package.name,package.version,vulnerability.id,vulnerability.description,vulnerability.severity,vulnerability.score.base,vulnerability.reference')
# Sistemas no formato real, marcados como fictícios: 01-03 servidores Windows, 04 Linux, demais computadores de funcionários
function SO([int]$n) { if ($n -le 3) { 'Microsoft Windows Server 2019 (ficticio)' } elseif ($n -eq 4) { 'Ubuntu 22.04 LTS (ficticio)' } else { 'Microsoft Windows 10 Pro (ficticio)' } }
# Mesma falha em 12 máquinas (correlação "falha espalhada")
1..12 | ForEach-Object { $v.Add(("FICT-SRV-{0:00},{1},pacote-alfa,1.0.2,CVE-2099-0001,""Falha ficticia no pacote-alfa, permite execucao remota"",Critical,9.8,https://exemplo.invalid/CVE-2099-0001" -f $_, (SO $_))) }
# Um pacote com 5 falhas diferentes na mesma máquina (pacote concentra + máquina acumula)
$notas = '9.1', '9.4', '9.0', '9.9', '10'
0..4 | ForEach-Object { $v.Add(("FICT-SRV-01,{2},pacote-beta,2.3,CVE-2099-010{0},Falha ficticia {0} no pacote-beta,Critical,{1},https://exemplo.invalid/CVE-2099-010{0}" -f ($_ + 1), $notas[$_], (SO 1))) }
# Fonte diz Critical, mas a nota é 7,5 (com vírgula) e 5.3: a plataforma deve rebaixar e justificar
$v.Add("FICT-SRV-02,$(SO 2),pacote-gama,3.1,CVE-2099-0201,Falha ficticia de nota alta,Critical,""7,5"",https://exemplo.invalid/CVE-2099-0201")
$v.Add("FICT-SRV-03,$(SO 3),pacote-gama,3.1,CVE-2099-0202,Falha ficticia de nota media,Critical,5.3,https://exemplo.invalid/CVE-2099-0202")
# Erros de propósito: nota inválida, máquina em branco, texto malicioso
$v.Add("FICT-SRV-04,$(SO 4),pacote-delta,0.9,CVE-2099-0203,Nota propositalmente invalida,Critical,n/a,https://exemplo.invalid/CVE-2099-0203")
$v.Add(",$(SO 5),pacote-delta,0.9,CVE-2099-0204,Maquina propositalmente em branco,Critical,6.1,https://exemplo.invalid/CVE-2099-0204")
$v.Add("FICT-SRV-05,$(SO 5),pacote-delta,0.9,CVE-2099-0205,""Texto malicioso de teste: <script>alert('xss')</script>"",Critical,4.2,https://exemplo.invalid/CVE-2099-0205")
[IO.File]::WriteAllText((Join-Path $Destino 'vulnerabilidades-ficticio.csv'), ($v -join "`n") + "`n", $utf8)

# ---------- Alertas (cada linha embrulhada em aspas; data com vírgula) ----------
function Embrulhar([string]$linha) { '"' + $linha.Replace('"', '""') + '"' }
$a = [System.Collections.Generic.List[string]]::new()
$a.Add((Embrulhar 'timestamp,agent.name,rule.description,data.devname,rule.level,rule.id'))
# 22 alertas da mesma regra em ~7 minutos (correlação "pico de alertas")
0..21 | ForEach-Object {
    $t = ([datetime]'2026-10-07T08:00:00').AddSeconds($_ * 20)
    $a.Add((Embrulhar ("{0},coletor-fict,Fortigate: tentativa de acesso bloqueada (ficticio),FW-FICT-01,5,99001" -f $t.ToString('MMM d,yyyy @ HH:mm:ss.fff', [Globalization.CultureInfo]::InvariantCulture))))
}
$a.Add((Embrulhar 'Oct 7,2026 @ 09:00:00.000,coletor-fict,"Fortigate: ataque detectado, sessao encerrada (ficticio)",FW-FICT-01,12,99002'))
$a.Add((Embrulhar 'Oct 7,2026 @ 09:30:00.000,coletor-fict,Fortigate: assinatura de ataque grave (ficticio),FW-FICT-01,15,99003'))
$a.Add((Embrulhar 'Oct 7,2026 @ 10:00:00.000,coletor-fict,Fortigate: nivel propositalmente invalido (ficticio),FW-FICT-01,x,99004'))
# Linha com coluna a mais que NÃO é a data: não pode ser "consertada" à força
$a.Add((Embrulhar 'Oct 7,2026 @ 10:30:00.000,coletor-fict,Fortigate: linha com coluna a mais (ficticio),FW-FICT-01,7,99005,extra'))
[IO.File]::WriteAllText((Join-Path $Destino 'alertas-ficticio.csv'), ($a -join "`n") + "`n", $utf8)

# ---------- Alertas CORROMPIDOS de propósito (para a demo da validação) ----------
$c = [System.Collections.Generic.List[string]]::new()
$c.Add((Embrulhar 'timestamp,agent.name,rule.description,data.devname,rule.level,rule.id'))
$c.Add((Embrulhar 'Oct 7,2026 @ 08:00:00.000,coletor-fict,Fortigate: acesso bloqueado (ficticio),FW-FICT-01,5,99001'))
$c.Add((Embrulhar 'Oct 7,2099 @ 08:00:00.000,coletor-fict,Fortigate: data propositalmente no futuro (ficticio),FW-FICT-01,12,99002'))
$c.Add((Embrulhar 'Oct 7,2026 @ 08:05:00.000,coletor-fict,Fortigate: credencial vazada senha=Ficticia123 (ficticio),FW-FICT-01,7,99003'))
[IO.File]::WriteAllText((Join-Path $Destino 'alertas-corrompido-ficticio.csv'), ($c -join "`n") + "`n", $utf8)
