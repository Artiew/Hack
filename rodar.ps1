<#
  Comando unico da Plataforma Piersec.

  Uso:
    .\rodar.ps1                       compila, roda os testes, sobe o site e abre o navegador
    .\rodar.ps1 -DoZero               apaga var\ (lotes, relatorios, usuarios, chaves) e comeca limpo
    .\rodar.ps1 -SemTestes            pula os testes (so em emergencia na demo)
    .\rodar.ps1 -RedefinirSenhaAdmin  esqueceu a senha: gera uma senha temporaria nova para o admin

  Atalhos de dois cliques na pasta: "Abrir Plataforma.cmd" e "Esqueci a senha do admin.cmd".
  Se o PowerShell bloquear scripts: powershell -ExecutionPolicy Bypass -File .\rodar.ps1 (mesmos parametros).
#>
param(
    [switch]$DoZero,
    [switch]$SemTestes,
    [switch]$RedefinirSenhaAdmin
)

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
# O dotnet procura o global.json na pasta atual: sem isto, rodar de outra pasta usaria outras configuracoes.
Set-Location $raiz
$solucao = Join-Path $raiz 'Piersec.slnx'
$url = 'https://localhost:5001'

function Etapa([string]$texto) { Write-Host "`n==> $texto" -ForegroundColor Cyan }

# Site ja no ar (outra janela aberta): so abre o navegador, em vez de dar erro de porta ocupada.
$porta = ([uri]$url).Port
if (Get-NetTCPConnection -LocalPort $porta -State Listen -ErrorAction SilentlyContinue) {
    if ($RedefinirSenhaAdmin) {
        throw "O site ja esta aberto em outra janela. Feche aquela janela e rode de novo para redefinir a senha."
    }
    Write-Host "O site ja esta no ar em $url (aberto em outra janela). Abrindo o navegador." -ForegroundColor Yellow
    Start-Process $url
    return
}

function TemSdk10([string]$exe) {
    try { return ((& $exe --list-sdks) -join "`n") -match '(?m)^10\.' } catch { return $false }
}

Etapa '1/4 Verificando o SDK do .NET 10'
# Procura no PATH; depois instalado so para o usuario ($HOME\.dotnet) ou na raiz de um disco (pen drive), sem administrador.
$candidatos = @('dotnet', (Join-Path $HOME '.dotnet\dotnet.exe')) +
    @(Get-PSDrive -PSProvider FileSystem | ForEach-Object { Join-Path $_.Root '.dotnet\dotnet.exe' })
$dotnet = $candidatos | Where-Object { $_ -eq 'dotnet' -or (Test-Path $_) } | Where-Object { TemSdk10 $_ } | Select-Object -First 1
if (-not $dotnet) {
    throw 'SDK do .NET 10 nao encontrado. Instale em https://dot.net e rode de novo.'
}
if ($dotnet -ne 'dotnet') {
    $pastaDotnet = Split-Path $dotnet
    Write-Host "    usando $pastaDotnet"
    $env:Path = "$pastaDotnet;$env:Path"
    $env:DOTNET_ROOT = $pastaDotnet
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
}

if ($DoZero) {
    Etapa 'Do zero: apagando var\ (lotes, relatorios, usuarios, auditoria e chaves)'
    $var = Join-Path $raiz 'var'
    if (Test-Path $var) { Remove-Item -Recurse -Force $var }
}

# P-015: em Windows sem atualizacao, os .exe do .NET 10 param com "doesn't fully support CET". O mesmo programa
# chamado pelo dotnet.exe funciona; entao o compilador, os testes e o site passam a rodar por ele.
$exeDotnet = (Get-Command dotnet).Source
$sdk = Join-Path (Split-Path $exeDotnet) ('sdk\' + (& dotnet --version))
$csc = Join-Path $sdk 'Roslyn\bincore\csc.exe'
# (com o erro no stderr, o PowerShell 5.1 cai no catch: tambem conta como "precisa do contorno")
try { & $csc -version 2>$null | Out-Null; $contornoCet = $LASTEXITCODE -ne 0 } catch { $contornoCet = $true }
if ($contornoCet) {
    Write-Host '    Windows sem atualizacao (CET): rodando o compilador e os testes pelo dotnet.exe (P-015).' -ForegroundColor Yellow
    $pastaContorno = Join-Path $raiz 'var\contorno-cet'
    New-Item -ItemType Directory -Force $pastaContorno | Out-Null
    Set-Content -Encoding ascii (Join-Path $pastaContorno 'csc.cmd') "@`"$exeDotnet`" exec `"$(Join-Path $sdk 'Roslyn\bincore\csc.dll')`" %*"
    $env:CscToolPath = $pastaContorno
    $env:CscToolExe = 'csc.cmd'
    $env:UseSharedCompilation = 'false'
    $env:UseAppHost = 'false'
}

Etapa '2/4 Compilando'
& dotnet build $solucao -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilacao. O site NAO foi iniciado.' }

if ($SemTestes) {
    Write-Host '    (testes pulados por -SemTestes)' -ForegroundColor Yellow
} elseif ($contornoCet) {
    Etapa '3/4 Rodando os testes'
    # O dotnet test usa o Piersec.Tests.exe (o xUnit v3 sempre gera o .exe); pelo dotnet exec roda a mesma suite.
    Push-Location (Join-Path $raiz 'tests\Piersec.Tests\bin\Release\net10.0')
    try { & dotnet exec Piersec.Tests.dll } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw 'Testes falharam. O site NAO foi iniciado.' }
} else {
    Etapa '3/4 Rodando os testes'
    & dotnet test --solution $solucao -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Testes falharam. O site NAO foi iniciado.' }
}

Etapa "4/4 Subindo o site em $url"
Write-Host '    NAO feche esta janela enquanto usar o site: fechar (ou Ctrl+C) desliga o site.' -ForegroundColor Yellow
Write-Host '    Nao clique dentro desta janela: o Windows pausa o programa (aperte Esc para continuar).' -ForegroundColor Yellow
if ($RedefinirSenhaAdmin) {
    Write-Host '    Uma senha temporaria nova do admin vai aparecer logo abaixo: anote antes de entrar.' -ForegroundColor Yellow
}
$env:Piersec__PastaDados = Join-Path $raiz 'var'
$env:PIERSEC_ABRIR_NAVEGADOR = '1'
$env:PIERSEC_REDEFINIR_ADMIN = if ($RedefinirSenhaAdmin) { '1' } else { $null }
try {
    & dotnet run --project (Join-Path $raiz 'src\Piersec.Web') -c Release --no-build --no-launch-profile --urls $url
}
finally {
    # Nao deixar a "redefinicao" ligada nesta janela: a proxima execucao nao deve trocar a senha de novo.
    Remove-Item Env:PIERSEC_REDEFINIR_ADMIN -ErrorAction SilentlyContinue
}
