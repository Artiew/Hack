$ErrorActionPreference = 'Stop'
if (Get-NetTCPConnection -LocalPort 5443 -State Listen -ErrorAction SilentlyContinue) { throw 'Porta de teste 5443 ocupada; nenhum processo foi alterado.' }
$raiz = Split-Path $PSScriptRoot -Parent
$qa = Join-Path $env:TEMP ('piersec-ui-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $qa | Out-Null
$senhaTeste = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
$sal = [Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
$hash = [Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2([Text.Encoding]::UTF8.GetBytes($senhaTeste), $sal, 600000, [Security.Cryptography.HashAlgorithmName]::SHA256, 32)
$hashGravado = 'PBKDF2-SHA256$600000$' + [Convert]::ToBase64String($sal) + '$' + [Convert]::ToBase64String($hash)
@(@{Login='analista-teste';Papel='admin';HashSenha=$hashGravado;CriadoEmUtc=[DateTimeOffset]::UtcNow}, @{Login='executivo-teste';Papel='executivo';HashSenha=$hashGravado;CriadoEmUtc=[DateTimeOffset]::UtcNow}) | ConvertTo-Json | Set-Content (Join-Path $qa 'usuarios.json')
$env:DOTNET_ROOT = Join-Path $env:USERPROFILE '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:Piersec__PastaDados = $qa
$env:Piersec__PastaRegras = Join-Path $raiz 'regras'
$env:PIERSEC_ABRIR_NAVEGADOR = '0'
$dotnetTeste = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
$processo = Start-Process -FilePath $dotnetTeste -ArgumentList @('run','--project','src/Piersec.Web','--no-build','--no-launch-profile','--urls','https://localhost:5443') -WorkingDirectory $raiz -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $qa 'servidor.log') -RedirectStandardError (Join-Path $qa 'servidor-erro.log')
function AbrirSessao([string]$loginTeste) {
    $sessaoTeste = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $entrada = Invoke-WebRequest 'https://localhost:5443/Entrar' -WebSession $sessaoTeste -SkipCertificateCheck
    $tokenTeste = [regex]::Match($entrada.Content, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
    if (!$tokenTeste) { throw 'Sem token no login' }
    $null = Invoke-WebRequest 'https://localhost:5443/Entrar' -Method Post -Body @{Login=$loginTeste;Senha=$senhaTeste;__RequestVerificationToken=$tokenTeste} -WebSession $sessaoTeste -SkipCertificateCheck
    return $sessaoTeste
}
function CodigoTeste([string]$chave, [long]$passo) {
    $alfabeto = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'
    $bits = ($chave.Replace(' ','').ToCharArray() | ForEach-Object { [Convert]::ToString($alfabeto.IndexOf($_),2).PadLeft(5,'0') }) -join ''
    $bytes = [byte[]]::new([int]($bits.Length / 8))
    for($j=0;$j -lt $bytes.Length;$j++) { $bytes[$j] = [Convert]::ToByte($bits.Substring($j*8,8),2) }
    $contador = [BitConverter]::GetBytes($passo); [Array]::Reverse($contador)
    $h = [Security.Cryptography.HMACSHA1]::HashData($bytes,$contador)
    $o = $h[-1] -band 15
    $n = (([int]$h[$o] -band 127) -shl 24) -bor ([int]$h[$o+1] -shl 16) -bor ([int]$h[$o+2] -shl 8) -bor $h[$o+3]
    return ($n % 1000000).ToString('D6')
}
function Token([string]$html) { return [regex]::Match($html, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value }
function Formulario([string]$html,[string]$alvo) {
    $form = [regex]::Matches($html,'(?s)<form\b[^>]*>.*?</form>') | Where-Object { $_.Value -match $alvo } | Select-Object -First 1
    if (!$form) { throw "Formulário ausente: $alvo" }
    return $form.Value
}
function Enviar([string]$form,$campos,$sessao) {
    $destino = [Net.WebUtility]::HtmlDecode([regex]::Match($form, 'action="([^"]+)"').Groups[1].Value)
    $campos.__RequestVerificationToken = Token $form
    return Invoke-WebRequest ('https://localhost:5443' + $destino) -Method Post -Body $campos -WebSession $sessao -SkipCertificateCheck
}
try {
    for ($i=0; $i -lt 30; $i++) {
        try { $null = Invoke-WebRequest 'https://localhost:5443/Entrar' -SkipCertificateCheck; break } catch { Start-Sleep -Milliseconds 500 }
    }
    $sessao = AbrirSessao 'analista-teste'
    $cadastro = Invoke-WebRequest 'https://localhost:5443/DoisFatores' -WebSession $sessao -SkipCertificateCheck
    if ($cadastro.Content -match 'class="navegacao"') { throw 'Menu oferece destinos bloqueados durante cadastro obrigatório do 2FA.' }
    $chave = [regex]::Match($cadastro.Content,'class="chave-2fa">([^<]+)').Groups[1].Value
    if (!$chave) { throw 'Chave fictícia não encontrada' }
    $passo = [long][Math]::Floor([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()/30)
    $ativacao = Enviar (Formulario $cadastro.Content 'handler=Confirmar') @{Codigo=(CodigoTeste $chave $passo)} $sessao
    if ($ativacao.Content -notmatch 'Ativada') { Write-Output ('Destino: ' + $ativacao.BaseResponse.RequestMessage.RequestUri.AbsolutePath); Write-Output ([regex]::Match($ativacao.Content,'<p class="erro"[^>]*>([^<]+)').Groups[1].Value); throw 'Falhou ao ativar 2FA fictício' }
    if ($ativacao.Content -notmatch 'class="navegacao"') { throw 'Menu não liberado após ativar o 2FA.' }
    $automacao = Invoke-WebRequest 'https://localhost:5443/Automacao' -WebSession $sessao -SkipCertificateCheck
    if ($automacao.BaseResponse.RequestMessage.RequestUri.AbsolutePath -ne '/Automacao') { throw 'Automação não liberada após ativação.' }
    Write-Output 'OK: menu e Automação liberados após ativar 2FA.'
    $recuperacao = [regex]::Matches($ativacao.Content,'<li><code>([^<]+)</code></li>')[0].Groups[1].Value
    $null = Enviar (Formulario $ativacao.Content 'action="/Sair"') @{} $sessao
    $sessao = AbrirSessao 'analista-teste'
    $etapa = Invoke-WebRequest 'https://localhost:5443/SegundoFator' -WebSession $sessao -SkipCertificateCheck
    $incorreto = Enviar (Formulario $etapa.Content 'action="/SegundoFator') @{Codigo='invalido';ReturnUrl='/'} $sessao
    if ($incorreto.Content -notmatch 'Código inválido') { throw 'Código inválido sem mensagem visível' }
    Write-Output 'OK: código inválido retorna mensagem visível.'
    $entrada = Enviar (Formulario $incorreto.Content 'action="/SegundoFator') @{Codigo=(CodigoTeste $chave ($passo+1));ReturnUrl='/'} $sessao
    if ($entrada.Content -notmatch '<h1>Visão geral</h1>') { throw 'Código válido não abriu a visão geral' }
    Write-Output 'OK: código TOTP válido conclui o login.'
    $null = Enviar (Formulario $entrada.Content 'action="/Sair"') @{} $sessao
    $sessao = AbrirSessao 'analista-teste'
    $etapa = Invoke-WebRequest 'https://localhost:5443/SegundoFator' -WebSession $sessao -SkipCertificateCheck
    $entrada = Enviar (Formulario $etapa.Content 'action="/SegundoFator') @{Codigo=$recuperacao;ReturnUrl='/'} $sessao
    if ($entrada.Content -notmatch 'Você entrou com um código de recuperação') { throw 'Recuperação não concluiu login' }
    Write-Output 'OK: código de recuperação conclui o login.'
} finally {
    if (!$processo.HasExited) { Stop-Process -Id $processo.Id }
}
