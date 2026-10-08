<#
Preserva as análises locais antes de iniciar um novo histórico, quando as chaves antigas não abrem nesta máquina.
Não apaga arquivos e não altera usuários, senhas, 2FA, auditoria nem as chaves existentes.
Feche a plataforma antes. Uso: .\Reparar-AnalisesLocais.ps1
-PastaDados é usado pelos testes com armazenamento fictício separado.
#>
param([string]$PastaDados = (Join-Path $PSScriptRoot 'var'))
$ErrorActionPreference = 'Stop'
$raizDados = [IO.Path]::GetFullPath($PastaDados).TrimEnd([IO.Path]::DirectorySeparatorChar)
if (!(Test-Path -LiteralPath $raizDados -PathType Container)) { throw 'A pasta de dados não existe; nada foi alterado.' }
if ((Get-Item -LiteralPath $raizDados).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Pasta de dados é um link; nada foi alterado.' }
if (($raizDados -eq [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'var'))) -and (Get-NetTCPConnection -LocalPort 5001 -State Listen -ErrorAction SilentlyContinue)) { throw 'Feche a plataforma na porta 5001 antes do reparo; nada foi alterado.' }
$prefixoDados = $raizDados + [IO.Path]::DirectorySeparatorChar
$fontes = @('segredos','lotes','operacao','tmp') | ForEach-Object {
    $caminho = [IO.Path]::GetFullPath((Join-Path $raizDados $_))
    if (!$caminho.StartsWith($prefixoDados,[StringComparison]::OrdinalIgnoreCase)) { throw 'Caminho fora da pasta de dados.' }
    if (Test-Path -LiteralPath $caminho) {
        $item = Get-Item -LiteralPath $caminho
        if (!$item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Pasta de análise inesperada; nada foi alterado.' }
        $caminho
    }
}
if (!$fontes) { Write-Output 'Nenhuma análise existente; nada foi alterado.'; return }
$pastaPreservados = Join-Path $raizDados 'preservados'
if ((Test-Path -LiteralPath $pastaPreservados) -and ((Get-Item -LiteralPath $pastaPreservados).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Pasta de preservação é um link; nada foi alterado.' }
$destino = [IO.Path]::GetFullPath((Join-Path $pastaPreservados ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))))
if (!$destino.StartsWith($prefixoDados,[StringComparison]::OrdinalIgnoreCase)) { throw 'Destino fora da pasta de dados.' }
New-Item -ItemType Directory -Path $destino | Out-Null
foreach ($fonte in $fontes) {
    Move-Item -LiteralPath $fonte -Destination $destino -ErrorAction Stop
}
Write-Output ('Análises anteriores preservadas em: ' + $destino)
Write-Output 'Ao reabrir a plataforma, novos envios terão um novo histórico. As contas e suas chaves permanecem nas pastas originais.'
