# Troubleshooting

Um problema por vez, no formato do `docs/PROMPT.md` (seção 6).

## P-001 · `dotnet test` recusa o xUnit v3 no .NET 10
Requisito afetado: base de testes (todos) · Impacto: bloqueia demo (o `rodar` não sobe o site sem testes)
- **Sintoma × Esperado:** `error : Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK` × suíte rodando.
- **Reprodução:** `dotnet test Piersec.slnx` com `xunit.v3` 4.0.1 e SDK 10.0.401, sem configuração de runner.
- **Isolamento:** infraestrutura de testes (nenhuma etapa do produto envolvida).
- **Hipóteses:** (1) o SDK 10 exige optar pelo novo runner (a mensagem de erro diz isso) — confirmada.
- **Teste que falha antes:** o próprio `dotnet test`.
- **Correção mínima:** `global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }` e usar `dotnet test --solution`.
- **Verificação:** 49/49 ✔ · ciclo do zero ✔.
- **Aprendizado:** o .NET 10 trocou o "motor" que executa testes; projetos novos precisam dizer qual usam.

## P-002 · Selo de confidencialidade "sumia" do HTML bruto
Requisito afetado: S4 · Impacto: degrada nota (teste falhava; visualmente estava certo)
- **Sintoma × Esperado:** teste `Tem_selo_de_confidencialidade` falha: HTML tinha `&#224;` no lugar de `à` × texto legível.
- **Reprodução:** `[System.Net.WebUtility]::HtmlEncode("à")` → `&#224;`.
- **Isolamento:** etapa = relatório.
- **Hipóteses:** (1) `WebUtility.HtmlEncode` troca letras acentuadas por códigos — confirmada pela reprodução.
- **Teste que falha antes:** `RelatorioTests.Tem_selo_de_confidencialidade`.
- **Correção mínima:** `HtmlEncoder.Create(UnicodeRanges.All)`: continua escapando `< > & "` e mantém acentos.
- **Verificação:** teste ✔ · suíte 49/49 ✔ · escape do `<script>` continua provado ✔.
- **Aprendizado:** escapar HTML é obrigatório, mas o "como" muda o resultado; o teste pegou a diferença.

## P-003 · `rodar.cmd` chamado de outra pasta ignorava o `global.json`
Requisito afetado: comando único (demo) · Impacto: bloqueia demo
- **Sintoma × Esperado:** `MSB1001: Opção desconhecida --solution` e o site não sobe × site no ar.
- **Reprodução:** de qualquer pasta fora do projeto: `D:\projetos\piersec\rodar.cmd -DoZero`.
- **Isolamento:** script de execução (o produto não chegou a rodar).
- **Hipóteses:** (1) o `dotnet` procura o `global.json` na pasta **atual**; fora do projeto ele cai no runner antigo — confirmada (rodando de dentro da pasta funciona).
- **Correção mínima:** `Set-Location $PSScriptRoot` no início do `rodar.ps1`.
- **Verificação:** rodado de fora da pasta: compila ✔ · 49/49 ✔ · site no ar ✔ · envio de CSV + PDF ✔.
- **Aprendizado:** script de demo tem que funcionar de qualquer lugar; a trava "teste falhou → não sobe" funcionou.

## P-004 · Coluna "Id" espremida no PDF
Requisito afetado: RG6 / clareza · Impacto: cosmético
- **Sintoma × Esperado:** `EV-00007` quebrado uma letra por linha × Id em uma linha.
- **Isolamento:** etapa = relatório (CSS `word-break: break-all` em `code`).
- **Correção mínima:** `white-space: nowrap` no Id; quebra só onde der na coluna Origem.
- **Verificação:** PDF regenerado do zero e conferido visualmente ✔ · suíte 49/49 ✔.

## P-005 · Leitura dos alertas desalinhava as colunas sem dar erro
Requisito afetado: RF1/RF2 · Impacto: bloqueia demo (dados errados em silêncio)
- **Sintoma × Esperado:** 10.000 alertas com `data.devname` vazio e "9.766 máquinas" × 1 firewall e colunas corretas.
- **Reprodução:** ler `Eventos SIEM-3(...).csv` com um leitor CSV comum.
- **Isolamento:** etapa = coleta (o arquivo, não a padronização).
- **Hipóteses (verificadas só com contagens, sem ler dados):** (1) cada linha vem inteira entre aspas → confirmada (10.001/10.001 linhas); (2) a data `Oct 7,2026 @ ...` tem vírgula sem aspas → confirmada (7 campos para 6 colunas em 100% das linhas; 2º campo com formato de hora).
- **Correção:** `LeitorCsv` desembrulha linhas embrulhadas; linhas com colunas a mais vão para `Desalinhadas` e só voltam se o adaptador souber consertar (`AdaptadorAlertasSiem.RepararLinha` junta só a data). O resto vira lacuna. Ajustes declarados no relatório.
- **Verificação:** testes `Arquivo_com_cada_linha_embrulhada...`, `Alertas_embrulhados_com_data_partida...`, `Coluna_a_mais_que_nao_e_a_data_nao_e_consertada_a_forca` ✔ · dados reais: 10.000 lidos, 0 rejeitados, 10.000 com data ✔.
- **Aprendizado:** número que "não faz sentido" (1.329 firewalls) é sintoma; conferir o formato antes de confiar no dado.

## P-006 · Justificativa cortada escondia o "por que não é Crítica"
Requisito afetado: RF4 / RG4 · Impacto: degrada nota
- **Sintoma × Esperado:** teste do relatório não achava "Não é Crítica porque..." × frase visível.
- **Isolamento:** etapa = relatório (corte em 220 caracteres) + severidade (texto longo demais).
- **Correção mínima:** justificativa mais curta (teste exige ≤ 200 caracteres) e relatório não corta justificativas (só descrições vindas dos dados).
- **Verificação:** teste ✔ · suíte 105/105 ✔ · PDF dos exemplos conferido ✔.

## P-007 · Arquivos de vulnerabilidade não estão em UTF-8 (símbolos virando invisíveis)
Requisito afetado: RF2 · Impacto: degrada nota (texto corrompido em silêncio)
- **Sintoma × Esperado:** lacuna "não está em UTF-8; lido como Latin-1" nos 2 arquivos × texto fiel.
- **Isolamento:** etapa = coleta (decodificação).
- **Hipóteses (contagens de bytes):** 100% dos bytes não-ASCII são inválidos em UTF-8 → codificação de 1 byte; 101 bytes na faixa 0x80–0x9F, onde Latin-1 tem caracteres invisíveis e Windows-1252 tem aspas curvas/travessão → Windows-1252.
- **Teste que falha antes:** `Arquivo_em_Windows1252_mantem_aspas_curvas_e_acentos` (saía `"Ação teste  ok"`).
- **Correção mínima:** plano B passa de Latin-1 para Windows-1252 (já vem no .NET).
- **Verificação:** teste ✔ · suíte 105/105 ✔ · dados reais: 0 eventos com caractere invisível ✔.

## P-008 · Score dizia "risco baixo" com 100% das máquinas com falha crítica
Requisito afetado: RF5 / clareza para o executivo · Impacto: degrada nota (contradição na página 1)
- **Sintoma × Esperado:** exemplos fictícios: "15 de 100 — risco baixo — poucos problemas" ao lado de "falhas graves em 100% das máquinas" × score coerente com os riscos.
- **Isolamento:** etapa = score (fórmula do fator "Falhas nos programas").
- **Causa:** cada falha crítica valia 10 pontos de 100 por máquina; uma máquina com 1 falha crítica (já invadível) ficava em 10.
- **Correção:** a máquina recebe a nota da sua **pior** falha (crítica 70, alta 50, média 25, baixa 10) + 3 por falha a mais, até 100 (`regras/score.json`).
- **Teste que trava:** `Score_e_coerente_com_os_riscos_quando_todas_as_maquinas_tem_falha_critica`, `Uma_falha_critica_ja_deixa_a_maquina_em_risco_alto`.
- **Verificação:** exemplos: 50 (Elevado) · dados reais: 74 (Alto) · suíte 135/135 ✔.
- **Aprendizado:** conferir o relatório como o leitor leria; testes de unidade não pegam contradição entre seções.

## P-009 · Projeção "o score cairia para X" ignorava máquinas corrigidas
Requisito afetado: plano de remediação · Impacto: degrada nota (número enganoso)
- **Sintoma × Esperado:** ao simular as correções, máquinas sem falhas restantes sumiam da média (podendo até subir o score) × contar como 0.
- **Correção:** `CalculadoraScore.Calcular(..., maquinasConhecidas)` mantém a população original na projeção.
- **Teste:** `Na_projecao_maquina_corrigida_conta_como_zero_e_nao_some_da_media` ✔.

## P-010 · Teste de ponta a ponta mostrou texto antigo depois de corrigido
Requisito afetado: nenhum (processo de teste) · Impacto: atrasa (falso alarme)
- **Sintoma × Esperado:** o relatório do site ainda dizia "0" em vez de "0 ações" depois da correção × texto novo.
- **Isolamento:** etapa = build. O `dotnet test` recompila os testes e o núcleo, **mas não o site**; o script subia o site com `--no-build`, usando a versão antiga.
- **Correção:** rodar `dotnet build Piersec.slnx` antes de subir o site com `--no-build` (o `rodar.cmd` já compila tudo, então o usuário não é afetado).
- **Verificação:** depois do build, o ponta a ponta mostrou "0 ações" ✔.
- **Aprendizado:** quando o resultado não muda depois de uma correção, primeiro confirmar que o programa rodando é o novo.

## P-011 · Sem a chave, o estado da automação dava erro confuso
Requisito afetado: RS3 · Impacto: atrasa (mensagem ruim)
- **Sintoma × Esperado:** abrir `var/operacao/estado.json.cifrado` sem a chave certa gerava `JsonException` × mensagem clara.
- **Causa:** sem a chave, os bytes cifrados chegam como "lixo" ao leitor de JSON, que falha antes da decifragem reclamar.
- **Correção:** tratar `CryptographicException` e `JsonException` do mesmo jeito: erro claro e **nada é apagado** (começar do zero duplicaria chamados).
- **Teste:** `Chamados_ficam_cifrados_no_disco_e_sao_lidos_de_volta` (falhou antes, passa depois) ✔.

## P-012 · Pacote do pen drive (bundle) abria uma pasta vazia
Requisito afetado: nenhum (transporte do projeto) · Impacto: bloqueia o trabalho na faculdade
- **Sintoma × Esperado:** `git clone piersec.bundle piersec` → "remote HEAD refers to nonexistent ref" e pasta sem arquivos × projeto completo.
- **Causa:** o bundle foi criado com `git bundle create ... main`, que leva só o ramo `main`, sem o `HEAD` (qual ramo abrir). O clone não sabe o que colocar na pasta.
- **Correção:** `git bundle create <PEN>:\piersec.bundle HEAD main` (aqui, no `CLAUDE.md` e nas instruções do pen drive). `git pull <bundle> main` já funcionava, porque diz o ramo.
- **Verificação:** clone em pasta nova seguindo as instruções → ramo `main`, último commit, 248/248 testes ✔.
- **Aprendizado:** testar o pacote do jeito que ele vai ser usado (clone do zero), não só com `git bundle verify`.

## P-013 · Botão "Sair" dava erro 400 (sem token anti-CSRF)
Requisito afetado: RS1/RS2 · Impacto: degrada nota (sair não funcionava; a sessão só caía sozinha em 30 min)
- **Sintoma × Esperado:** clicar em "Sair" → 400 × voltar para "Entrar". Achado ao testar o envio como executivo: a página tinha 0 tokens.
- **Causa:** `<form method="post" action="/Sair">` — com `action` escrito à mão, o ASP.NET **não** coloca o token anti-CSRF no formulário; o servidor recusa o POST sem token (como deve).
- **Por que os testes não pegaram:** o teste de "sair só por POST" usava o token tirado de outro formulário da página (o de envio), não o do próprio botão.
- **Correção:** `asp-page="/Sair"` (o ASP.NET gera a URL e o token). Conferidos os outros formulários com `action`: são buscas por GET, que não precisam de token.
- **Verificação:** ponta a ponta como executivo, com o token da PRÓPRIA página: Sair → 302 /Entrar; depois disso, `/` → /Entrar ✔.
- **Aprendizado:** testar cada botão com o que a página dele oferece, como um usuário faria.

## P-014 · "Usuário não encontrado" ao CRIAR usuário logo depois de excluir
Requisito afetado: RS1 (gestão de usuários) · Impacto: bloqueia (não dava para criar usuário depois de excluir)
- **Sintoma × Esperado:** depois de excluir, criar um usuário novo mostrava "Usuário não encontrado." × criar normalmente. Relatado pelo Vitor.
- **Causa:** a exclusão respondia na própria página, e o endereço ficava `/Usuarios?handler=Excluir`. O formulário de criar não dizia para onde enviar, então ia para o mesmo endereço, e o servidor tratava o "criar" como um "excluir" sem nome.
- **Correção:** (1) o formulário de criar aponta explicitamente para `/Usuarios` (`asp-page`); (2) depois de excluir, redireciona para `/Usuarios` limpo (padrão "POST → redireciona → GET"), com a mensagem em `TempData`. Bônus: F5 não tenta excluir de novo.
- **Por que os testes não pegaram:** o teste de ponta a ponta excluía, mas não criava depois. Novo teste refaz o caminho do Vitor usando o endereço que o próprio formulário indica.
- **Verificação:** criar → excluir → criar → criar com a confirmação aberta → login inválido: todos certos ✔.
- **Aprendizado:** formulário sem destino explícito vai para o endereço atual, e ele pode mudar depois de outra ação. Sempre dizer o destino (`asp-page` / `asp-page-handler`).

## P-015 · PC da faculdade: "Your Windows doesn't fully support CET" (não compila nem testa)
Requisito afetado: nenhum (ambiente) · Impacto: bloqueia o trabalho na faculdade
- **Sintoma × Esperado:** `rodar.cmd` → `Fatal error. Your Windows doesn't fully support CET. Please install all available Windows updates.` na compilação × compilar, testar e subir.
- **Ambiente:** Windows 10 22H2 build 19045.2193 (sem atualizações desde 2022), sem administrador; .NET 10.0.401 instalado só para o usuário.
- **Isolamento:** infraestrutura (o código não chegou a rodar). Só falham os **.exe próprios** do .NET (apphost: `csc.exe`, `Piersec.Tests.exe`); o mesmo programa chamado por `dotnet exec arquivo.dll` funciona (`csc.exe -version` falha, `dotnet exec csc.dll -version` funciona).
- **Hipóteses:** (1) os .exe novos do .NET pedem a proteção CET do processador, que esse Windows sem atualização não suporta por completo, e o `dotnet.exe` não pede: confirmada pela comparação acima. (2) `UseSharedCompilation=false` sozinho resolveria: descartada (o `csc.exe` continua sendo chamado).
- **Contorno (sem mudar o projeto, só naquela janela do PowerShell):**
  1. criar `csc.cmd` fora do projeto com `@"<dotnet>\dotnet.exe" exec "<dotnet>\sdk\10.0.401\Roslyn\bincore\csc.dll" %*`;
  2. `$env:CscToolPath='<pasta do csc.cmd>'; $env:CscToolExe='csc.cmd'; $env:UseSharedCompilation='false'; $env:UseAppHost='false'`;
  3. `dotnet build Piersec.slnx -c Release`;
  4. testes: `dotnet exec tests\Piersec.Tests\bin\Release\net10.0\Piersec.Tests.dll` (o `dotnet test` usa o `Piersec.Tests.exe`, que o xUnit v3 gera mesmo com `UseAppHost=false`);
  5. site: o mesmo `dotnet run` do `rodar.ps1` (com `UseAppHost=false` o site roda por `dotnet`).
- **Verificação:** build ✔ · 251/251 testes ✔ · site no ar em https://localhost:5001 ✔.
- **Depois: contorno automático no `rodar.ps1`** (o `Abrir Plataforma.cmd` falhava no PC da faculdade). O script agora:
  (a) acha o .NET 10 fora do PATH (`$HOME\.dotnet` ou `<disco>:\.dotnet`, como no pen drive), porque o PATH do PC só tinha o .NET 6;
  (b) testa o `csc.exe`; se falhar, liga os passos 1–4 acima sozinho (o `csc.cmd` fica em `var\contorno-cet`); em casa nada muda;
  (c) se o site já está no ar em outra janela, só abre o navegador (antes dava erro de porta ocupada).
  Verificação: `rodar.cmd` num PowerShell limpo → .NET achado, contorno ligado, 252/252 testes, site no ar ✔; segunda execução → "já está no ar" ✔.
- **Correção de verdade:** instalar as atualizações do Windows (precisa de administrador, não é nosso PC). Em casa o problema não aparece.
- **Aprendizado:** quando só o `.exe` falha e o `.dll` funciona, o problema é a "casca" do programa e o Windows, não o código.

## P-016 · SDK 10 ausente no PATH da máquina de casa
- **Sintoma × esperado:** `dotnet test --solution Piersec.slnx` para antes de executar: SDK 10.0.401 solicitado, apenas 9.0.312 instalado no PATH.
- **Isolamento:** ambiente; o código dos testes não chegou a rodar.
- **Contorno verificado (08/10/2026):** SDK oficial 10.0.401 instalado por `dotnet-install.ps1` em `%TEMP%\piersec-dotnet-10`; DOTNET_ROOT e PATH definidos apenas no processo de conferência. `global.json` preservado.
- **Verificação:** `dotnet build Piersec.slnx` sem avisos/erros; `dotnet test --solution Piersec.slnx --no-build`: 272 aprovados, 0 falhas, 0 ignorados.
- **Resolvido (08/10/2026):** SDK oficial estável 10.0.401 instalado em `C:\Users\pop\.dotnet`, local permanente já reconhecido pelo `rodar.ps1`. Conferência usando essa instalação: versão 10.0.401, compilação sem avisos/erros, 272/272 testes aprovados. `global.json` preservado.

## P-017 · Confirmação do 2FA sem orientação quando o campo está vazio
- **Relato:** clicar em Confirmar aparenta não fazer nada. A captura não mostrava mensagem de erro.
- **Isolamento:** em instância temporária, com conta fictícia, código TOTP válido e recuperação concluíram o login; código inválido exibiu erro. Não foi reproduzida falha com código válido preenchido.
- **Problema identificado:** o atributo `required` deixa o navegador bloquear o envio vazio antes de chegar ao servidor. Não havia mensagem própria da página para esse caso; envio vazio direto ao servidor ainda consumia tentativa.
- **Teste antes da correção:** três casos de `SegundoFatorPaginaTests` (null, vazio, espaços) falharam; os 272 testes existentes passaram.
- **Correção:** formulário usa validação no servidor (`novalidate`); presença do código é conferida antes da autenticação, com erro explícito sem consumir tentativa. Ajuda e placeholder esclarecem que é preciso digitar o código do aplicativo. Proteção anti-CSRF, exigência de 2FA, expiração, limite de requisições e validação dos códigos preservados.
- **Verificação:** compilação sem avisos/erros; 275/275 testes aprovados; fluxo HTTP isolado com código inválido, válido e recuperação aprovado. Nenhuma conta real foi lida ou alterada.
- **Limite:** esta correção cobre o envio vazio. Se o sintoma persistir com código preenchido na versão recompilada, ainda é necessário observar a requisição no navegador do usuário; não atribuir esse caso à validação de presença sem evidência.

## P-018 · Menu oferecia destinos bloqueados durante cadastro obrigatório do 2FA
- **Sintoma:** admin novo em `/DoisFatores`; ao clicar no menu, volta à mesma página.
- **Causa confirmada:** `ExigirTrocaDeSenha` exige concluir o cadastro, mas `_Layout` oferecia navegação como se o acesso já estivesse completo.
- **Antes:** roteiro com admin fictício falhou por encontrar menu durante o cadastro obrigatório.
- **Correção:** navegação somente após todas as etapas obrigatórias; cabeçalho restrito com orientação e Sair; texto do cadastro explica Ativar → guardar recuperação → continuar.
- **Segundo defeito reproduzido:** Sair imediatamente após ativar 2FA retornava 400. A resposta ainda usava a identidade anterior para gerar tokens, diferente da identidade gravada no cookie.
- **Correção da sessão:** depois de `SignInAsync`, `HttpContext.User` recebe o mesmo principal gravado, mantendo os formulários da resposta consistentes com o cookie.
- **Evidência:** build sem avisos/erros; 277 testes aprovados, incluindo `SessaoTests`; `tests/verificar-2fa.ps1` com armazenamento temporário confirma ausência de menu antes, menu e Automação liberados depois, saída imediata, código inválido, TOTP válido e recuperação. O roteiro requer build Debug e porta 5443 livre; não usa contas nem dados reais.
- **Aplicação:** fechar o terminal atual e abrir `Abrir Plataforma.cmd` para compilar a versão Release e reiniciar. O cadastro do 2FA do admin continua obrigatório.

## P-019 · Visão geral falhava antes de mostrar o envio — reparo local aplicado
- **Sintoma confirmado (08/10/2026):** a página inicial já mostrava “Algo deu errado”; o formulário nem aparecia. Não era uma falha de parsing do CSV.
- **Causa:** diagnóstico temporário filtrado registrou `InvalidOperationException` em `ChaveAnonimizacao.Obter`. A variável de chave alternativa não estava definida; o ramo de leitura da chave cifrada existente falhou. Esta execução não conseguia decifrar a chave de anonimização antiga. Não foi determinado por que a chave anterior deixou de abrir.
- **Segurança:** diagnóstico registrou somente tipos de exceção e nomes de métodos. Não leu conteúdos de CSVs, relatórios ou arquivos reais de dados para o agente. O código temporário foi removido.
- **Reparo:** `Reparar-AnalisesLocais.ps1` move somente `segredos/`, `lotes/`, `operacao/` e `tmp/` para `var/preservados/<data-id>/`. Não apaga nada, nem altera usuários, senhas, 2FA, auditoria ou as chaves Data Protection existentes. A nova execução cria uma chave de anonimização local e inicia novo histórico de análises. Não há comparação com os lotes preservados; recuperar seu conteúdo depende da chave original válida. Não rodar a rotina indiscriminadamente em instalações que funcionam.
- **Aplicação nesta máquina:** servidor parado; pastas preservadas em `var/preservados/20261008-132715-89119317`; Release recompilado e plataforma reiniciada em `https://localhost:5001/`. Contas mantidas. A restauração exige parar a plataforma e preservar também qualquer análise nova antes de devolver as pastas originais; não sobrescrever nem misturar históricos.
- **Verificação fictícia:** roteiro `tests/verificar-envio.ps1` prova que os arquivos inacessíveis foram preservados byte a byte e as contas permaneceram iguais. Envio normal e em segundo plano, CSV vazio, desconhecido, extensão recusada e CSV inventado de 10 mil registros concluíram e abriram o resultado. Suíte: 278 aprovados, 0 falhas, 0 ignorados.
- **Confirmação do usuário (08/10/2026):** “agora foi possivel enviar o csv”. Fluxo real confirmado pelo usuário após o reparo; o agente não acessou os CSVs nem os relatórios reais.
- **Defeito adicional reproduzido:** sem JavaScript, abrir `/?handler=Iniciar` e enviar pelo formulário sem destino explícito acionava o handler assíncrono e devolvia JSON em vez do lote. `asp-page="/Index" asp-page-handler=""` fixa o destino; o mesmo roteiro falhou antes e passou depois.
- **Teste de segurança:** `Chave_inacessivel_e_preservada_e_nao_orienta_apagar_os_dados` garante que uma chave fictícia alterada não é sobrescrita e a orientação não recomenda `-DoZero`.
