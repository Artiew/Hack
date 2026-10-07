<#
  Comando unico da Plataforma Piersec.

  Uso:
    .\rodar.ps1                       compila, roda os testes, sobe o site e abre o navegador
    .\rodar.ps1 -DoZero               apaga var\ (lotes, relatorios, usuarios, chaves) e comeca limpo
    .\rodar.ps1 -SemTestes            pula os testes (so em emergencia na demo)
    .\rodar.ps1 -RedefinirSenhaAdmin  esqueceu a senha: gera uma senha temporaria nova para o admin

  Atalhos de dois cliques na pasta: "Abrir Plataforma.cmd" e "Esqueci a senha do admin.cmd".
  Se o PowerShell bloquear scripts, use o rodar.cmd (mesmos parametros).
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

Etapa '1/4 Verificando o SDK do .NET 10'
$sdks = (& dotnet --list-sdks) -join "`n"
if ($sdks -notmatch '(?m)^10\.') {
    throw 'SDK do .NET 10 nao encontrado. Instale em https://dot.net e rode de novo.'
}

if ($DoZero) {
    Etapa 'Do zero: apagando var\ (lotes, relatorios, usuarios, auditoria e chaves)'
    $var = Join-Path $raiz 'var'
    if (Test-Path $var) { Remove-Item -Recurse -Force $var }
}

Etapa '2/4 Compilando'
& dotnet build $solucao -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilacao. O site NAO foi iniciado.' }

if ($SemTestes) {
    Write-Host '    (testes pulados por -SemTestes)' -ForegroundColor Yellow
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
