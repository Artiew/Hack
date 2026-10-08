# Regressão HTTP do envio: somente CSVs fictícios e armazenamento temporário separado.
# Requer PowerShell 7, SDK 10 e build Debug da solução.
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$urlTeste = 'https://localhost:5444'
if (Get-NetTCPConnection -LocalPort 5444 -State Listen -ErrorAction SilentlyContinue) { throw 'Porta 5444 ocupada; nenhum processo foi alterado.' }
$qa = Join-Path $env:TEMP ('piersec-envio-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $qa | Out-Null
$senhaTeste = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
$sal = [Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
$hash = [Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2([Text.Encoding]::UTF8.GetBytes($senhaTeste), $sal, 600000, [Security.Cryptography.HashAlgorithmName]::SHA256, 32)
$hashGravado = 'PBKDF2-SHA256$600000$' + [Convert]::ToBase64String($sal) + '$' + [Convert]::ToBase64String($hash)
@(@{Login='admin-teste';Papel='admin';HashSenha=$hashGravado;CriadoEmUtc=[DateTimeOffset]::UtcNow}, @{Login='analista-teste';Papel='analista';HashSenha=$hashGravado;CriadoEmUtc=[DateTimeOffset]::UtcNow}) | ConvertTo-Json | Set-Content (Join-Path $qa 'usuarios.json')
# Simula uma análise trazida de outra máquina; os bytes abaixo são inventados.
New-Item -ItemType Directory -Path (Join-Path $qa 'segredos'),(Join-Path $qa 'operacao'),(Join-Path $qa 'lotes'),(Join-Path $qa 'tmp') | Out-Null
$chaveInacessivel = [Security.Cryptography.RandomNumberGenerator]::GetBytes(132)
[IO.File]::WriteAllBytes((Join-Path $qa 'segredos/chave-anonimizacao.cifrado'), $chaveInacessivel)
[IO.File]::WriteAllBytes((Join-Path $qa 'operacao/estado.json.cifrado'), [byte[]](1,2,3))
$usuariosAntes = [IO.File]::ReadAllBytes((Join-Path $qa 'usuarios.json'))
# A rotina só move pastas; não lê nenhum arquivo de análise.
& (Join-Path $raiz 'Reparar-AnalisesLocais.ps1') -PastaDados $qa | Out-Null
$preservado = Get-ChildItem -LiteralPath (Join-Path $qa 'preservados') -Directory
if (!$preservado) { throw 'Histórico não foi preservado.' }
if ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $preservado.FullName 'segredos/chave-anonimizacao.cifrado'))) -ne [Convert]::ToBase64String($chaveInacessivel)) { throw 'Arquivo fictício foi alterado durante preservação.' }
if ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $qa 'usuarios.json'))) -ne [Convert]::ToBase64String($usuariosAntes)) { throw 'Contas foram alteradas.' }
Write-Output 'OK: análises inacessíveis preservadas byte a byte; contas intactas.'
$env:DOTNET_ROOT = Join-Path $env:USERPROFILE '.dotnet'
$env:Piersec__PastaDados = $qa
$env:Piersec__PastaRegras = Join-Path $raiz 'regras'
$env:PIERSEC_ABRIR_NAVEGADOR = '0'
$env:PIERSEC_REDEFINIR_ADMIN = '0'
$processo = Start-Process -FilePath (Join-Path $env:DOTNET_ROOT 'dotnet.exe') -ArgumentList @('run','--project','src/Piersec.Web','--no-build','--no-launch-profile','--urls',$urlTeste) -WorkingDirectory $raiz -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $qa 'servidor.log') -RedirectStandardError (Join-Path $qa 'servidor-erro.log')
function Token([string]$html) { return [regex]::Match($html, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value }
function Envio([string]$nome,[string]$csv,[bool]$assincrono,[string]$pagina = '/') {
    $visao = Invoke-WebRequest ($urlTeste + $pagina) -WebSession $sessao -SkipCertificateCheck
    $formulario = [regex]::Match($visao.Content,'(?s)<form\b[^>]*id="form-envio"[^>]*>.*?</form>').Value
    if (!$formulario) { throw 'Formulário de envio ausente.' }
    $destino = [Net.WebUtility]::HtmlDecode([regex]::Match($formulario,'action="([^"]*)"').Groups[1].Value)
    if (!$destino) { $destino = $visao.BaseResponse.RequestMessage.RequestUri.PathAndQuery }
    if ($assincrono) { $destino = '/?handler=Iniciar' }
    $corpo = [Net.Http.MultipartFormDataContent]::new()
    $arquivo = [Net.Http.ByteArrayContent]::new([Text.Encoding]::UTF8.GetBytes($csv))
    $corpo.Add($arquivo,'arquivos',$nome)
    $corpo.Add([Net.Http.StringContent]::new((Token $formulario)),'__RequestVerificationToken')
    try {
        $resposta = Invoke-WebRequest ($urlTeste + $destino) -Method Post -Body $corpo -WebSession $sessao -SkipCertificateCheck
        if ($assincrono) {
            $id = ($resposta.Content | ConvertFrom-Json).id
            if (!$id) { throw 'Envio não retornou acompanhamento.' }
            $estado = $null
            for ($i=0; $i -lt 240; $i++) {
                $estado = Invoke-RestMethod ($urlTeste + '/?handler=Estado&id=' + $id) -WebSession $sessao -SkipCertificateCheck
                if ($estado.erro) { throw ('Falha na análise fictícia: ' + $estado.erro) }
                if ($estado.concluido) { break }
                Start-Sleep -Milliseconds 500
            }
            if (!$estado.loteId -or !$estado.concluido) { throw 'Análise não terminou no prazo.' }
            $resposta = Invoke-WebRequest ($urlTeste + '/Lote/' + $estado.loteId) -WebSession $sessao -SkipCertificateCheck
        }
        if ($resposta.Content -notmatch '<h1>Resumo do lote ') { throw ('Envio não abriu o resultado: ' + $resposta.BaseResponse.RequestMessage.RequestUri.PathAndQuery) }
        Write-Output ('OK: ' + $nome + ' / segundo plano=' + $assincrono + ' / origem=' + $pagina)
    } finally { $corpo.Dispose() }
}
try {
    for ($i=0; $i -lt 30; $i++) {
        try { $entrada = Invoke-WebRequest ($urlTeste + '/Entrar') -SkipCertificateCheck; break } catch { Start-Sleep -Milliseconds 500 }
    }
    $sessao = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $entrada = Invoke-WebRequest ($urlTeste + '/Entrar') -WebSession $sessao -SkipCertificateCheck
    $null = Invoke-WebRequest ($urlTeste + '/Entrar') -Method Post -Body @{Login='analista-teste';Senha=$senhaTeste;__RequestVerificationToken=(Token $entrada.Content)} -WebSession $sessao -SkipCertificateCheck
    $cabecalho = 'agent.name,host.os.name,package.name,package.version,vulnerability.id,vulnerability.description,vulnerability.severity,vulnerability.score.base,vulnerability.reference'
    $csv = $cabecalho + "`nPC-FICTICIO,Windows 11,Programa Ficticio,1,CVE-2026-9999,Falha ficticia,Critical,9.8,https://example.invalid"
    Envio 'valido-ficticio.csv' $csv $false
    Envio 'valido-ficticio.csv' $csv $true
    Envio 'desconhecido-ficticio.csv' "coluna`nx" $false
    Envio 'vazio-ficticio.csv' '' $false
    Envio 'recusado-ficticio.txt' 'texto ficticio' $false
    $grande = [Text.StringBuilder]::new($cabecalho)
    for($i=0;$i -lt 10000;$i++) { $null = $grande.Append("`nPC-FICTICIO-$($i % 200),Windows 11,Programa-$($i % 17),1,CVE-2026-$('{0:D5}' -f $i),Falha ficticia,Critical,9.8,https://example.invalid") }
    Envio 'volume-ficticio.csv' $grande.ToString() $true
    Envio 'valido-ficticio.csv' $csv $false '/?handler=Iniciar'
} catch {
    # O log é exclusivamente desta instância fictícia; nunca usar com o var real.
    Get-Content -LiteralPath (Join-Path $qa 'servidor.log') -Tail 70
    throw
} finally {
    if (!$processo.HasExited) { Stop-Process -Id $processo.Id }
}
