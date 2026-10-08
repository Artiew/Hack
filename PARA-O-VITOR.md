# Para o Vitor — Plataforma Piersec

Preparado em 08/10/2026 por Vitor Calazans, para integração local do projeto do hackathon da Unisanta.

## Entrega e origem

**Caminho A — git bundle.** O código é um repositório Git do projeto original; não foi necessário inicializar outro repositório nem criar ZIP. Enviar somente `piersec-amigo.bundle` e este `PARA-O-VITOR.md` pelo WhatsApp.

- Repositório de origem: `C:\Users\pop\Desktop\piersec\piersec`.
- Saída: `C:\Users\pop\Desktop\piersec-para-o-vitor`.
- Branch: `main`.
- Primeiro commit: `47127bb398137b5489d3e8d5e558a849eaa62e8c` — Fase 1: esqueleto ponta a ponta.
- Ponto em comum: `742499aa264ceb1b3eb3af9019ced0c621b0f828`.
- `f4f5a35` não existe neste repositório; `742499a` e `d4fa492` existem. `742499a` é descendente e mais recente que `d4fa492`.
- A divergência com a frase final do roteiro foi apresentada ao usuário, que autorizou usar `742499a` e continuar.
- HEAD da entrega: `7f5f2be34a9128c699ac7fb98466a7931fa5bed8`.
- Estado final: `nothing to commit, working tree clean`.

O bundle inclui o histórico completo alcançável por HEAD/main. Nenhum commit antigo foi alterado; nenhum rebase, amend, reset, exclusão de branch ou push foi feito. O aviso do Git sugerindo push foi apenas texto do próprio status; não foi seguido.

## Commits depois do ponto em comum

Lista exata de `git log --oneline --reverse 742499a..HEAD`:

```text
9e8aa68 Trabalho do Vitor Calazans: melhorar visual da Automacao
f9faff2 Trabalho do Vitor Calazans: corrigir sessao e orientacoes do 2FA
44674d3 Trabalho do Vitor Calazans: documentar testes e pendencias para integracao
bf9f76e Trabalho do Vitor Calazans: recuperar envio preservando analises antigas
235164e Trabalho do Vitor Calazans: recuperar cadastro pendente de 2FA
c044186 Trabalho do Vitor Calazans: renovar visao executiva, temas e menu
7f5f2be Trabalho do Vitor Calazans: registrar validacao e limites da entrega
```

1. **9e8aa68 — Automação:** reorganiza indicadores, fila, chamados e decisões, com detalhes recolhíveis. Mantém os destinos POST, permissões e regras da operação simulada.
2. **f9faff2 — Sessão e 2FA:** mostra orientação para código vazio sem consumir tentativa; restringe navegação durante etapas obrigatórias. Atualiza a identidade da resposta junto ao cookie para manter os tokens de formulários consistentes após ativação.
3. **44674d3 — Documentação anterior:** registra validações, problemas e pendências para continuidade em outro computador.
4. **bf9f76e — Envio de CSV:** fixa destino do formulário de envio, inclusive sem JavaScript; melhora diagnóstico da chave de anonimização inacessível e inclui reparo que preserva análises antigas, acompanhado de testes fictícios.
5. **235164e — Cadastro pendente de 2FA:** recria somente configuração ainda não ativada cuja proteção/Base64 não possa ser aberta; informa novo QR e exige confirmação. Não substitui 2FA ativo; seis casos novos de regressão.
6. **c044186 — Visão executiva, temas e conta:** cria painel por seções, simulador, histórico, temas claro/escuro e menu inferior esquerdo. Adiciona testes do simulador e roteiro fictício de navegador; atualiza o marcador de sucesso do teste de envio.
7. **7f5f2be — Documentação da entrega:** consolida resultados, limitações e orientação de integração, incluindo os casos de 2FA e HTTPS ainda não confirmados no uso real.

Os quatro primeiros já existiam antes desta preparação. Os três últimos foram criados agora. Todos aparecem no intervalo solicitado; commits idênticos já presentes no computador do Vitor são reconhecidos pelo Git pelo hash.

## Arquivos que mudaram

Saída de `git diff --stat 742499a..HEAD`:

```text
 CLAUDE.md                                          |  67 ++++
 Reparar-AnalisesLocais.ps1                         |  33 ++
 docs/REQUISITOS.md                                 |  35 +-
 docs/TROUBLESHOOTING.md                            |  51 +++
 src/Piersec.Core/Analise/SimuladorRemediacao.cs    |  36 +++
 src/Piersec.Core/Padronizacao/Anonimizador.cs      |   2 +-
 src/Piersec.Core/Seguranca/ServicoAutenticacao.cs  |  34 +-
 src/Piersec.Web/Pages/Automacao.cshtml             | 105 +++---
 src/Piersec.Web/Pages/DoisFatores.cshtml           |   4 +-
 src/Piersec.Web/Pages/DoisFatores.cshtml.cs        |   7 +-
 src/Piersec.Web/Pages/Index.cshtml                 |   2 +-
 src/Piersec.Web/Pages/Lote.cshtml                  | 205 +-----------
 src/Piersec.Web/Pages/Lote.cshtml.cs               |  84 +++--
 src/Piersec.Web/Pages/SegundoFator.cshtml          |   7 +-
 src/Piersec.Web/Pages/SegundoFator.cshtml.cs       |   8 +
 src/Piersec.Web/Pages/Shared/_CartaoChamado.cshtml |  50 ++-
 src/Piersec.Web/Pages/Shared/_Layout.cshtml        |  42 ++-
 .../Pages/Shared/_PainelExecutivo.cshtml           | 217 +++++++++++++
 src/Piersec.Web/Seguranca/Sessao.cs                |  11 +-
 src/Piersec.Web/wwwroot/css/site.css               | 351 +++++++++++++++++++--
 src/Piersec.Web/wwwroot/js/conta.js                |  15 +
 src/Piersec.Web/wwwroot/js/executivo.js            | 175 ++++++++++
 src/Piersec.Web/wwwroot/js/tema.js                 |  30 ++
 tests/Piersec.Tests/CifragemTests.cs               |  15 +
 tests/Piersec.Tests/DoisFatoresTests.cs            |  83 +++++
 tests/Piersec.Tests/Piersec.Tests.csproj           |   5 +-
 tests/Piersec.Tests/SegundoFatorPaginaTests.cs     |  53 ++++
 tests/Piersec.Tests/SessaoTests.cs                 |  54 ++++
 tests/Piersec.Tests/SimuladorRemediacaoTests.cs    |  69 ++++
 tests/verificar-2fa.ps1                            |  82 +++++
 tests/verificar-envio.ps1                          |  88 ++++++
 tests/verificar-executivo.cjs                      | 273 ++++++++++++++++
 32 files changed, 1937 insertions(+), 356 deletions(-)
```

## Arquivos com maior chance de conflito

Comparação sempre contra `742499a`, não contra uma pasta copiada por RAR.

| Arquivo | Mudança e intenção |
|---|---|
| `src/Piersec.Core/Analise/PlanejadorAvaliacao.cs` | Sem alteração; planejamento e priorização originais preservados. |
| `src/Piersec.Core/Relatorio/GeradorRelatorioHtml.cs` | Sem alteração; novo layout executivo aplica-se ao site, não ao relatório exportado. |
| `src/Piersec.Core/Modelo/Avaliacao.cs` | Sem alteração; usa avaliação e ações existentes. |
| `src/Piersec.Core/Analise/RegrasAvaliacao.cs` | Sem alteração. |
| `src/Piersec.Core/Analise/ConjuntoRegras.cs` | Sem alteração; mantém carregamento e validação de regras. |
| `src/Piersec.Core/Analise/CalculadoraScore.cs` | Sem alteração; simulador chama a calculadora existente e preserva a população de máquinas. |
| `src/Piersec.Web/Pages/Lote.cshtml` | Substitui apresentação executiva extensa pelo partial `_PainelExecutivo`; mantém evidências técnicas restritas e inclui o script de navegação. Integrar junto ao model, partial, scripts e CSS. |
| `regras/score.json` | Sem alteração de nenhum valor. |
| `regras/plano.json` | Sem alteração de nenhum valor. |
| `CLAUDE.md` | Acrescenta continuidade e estado desta entrega; nome CLAUDE.md restaurado anteriormente a pedido do usuário. Não criar novamente aaaa.md. Notas antigas de “sem commit” foram superadas pela seção final da entrega. |
| `docs/REQUISITOS.md` | Atualiza evidências do painel, temas, navegação, simulador e validação consolidada. Mantém limites explícitos em acessibilidade, orçamento e dados ausentes. |

Outros pontos de atenção: `_Layout.cshtml` agora contém menu de conta inferior esquerdo; `site.css` usa paleta neutra/âmbar e inclui estados claros/escuros; `Lote.cshtml.cs` recebe `ConjuntoRegras` e implementa GET de simulação. Ao resolver conflitos, manter esses arquivos em conjunto e preservar o destino explícito/token dos formulários POST.

## Regras JSON — valor antigo e novo

**Não houve mudanças em `regras/*.json`.** Não existem parâmetros com valor antigo/novo diferente neste intervalo. Conferência individual dos arquivos presentes:

| Arquivo | Antigo em 742499a → novo em HEAD |
|---|---|
| `regras/ativos.json` | Idêntico, todos os valores preservados. |
| `regras/automacao.json` | Idêntico, todos os valores preservados. |
| `regras/correlacao.json` | Idêntico, todos os valores preservados. |
| `regras/frameworks.json` | Idêntico, todos os valores preservados. |
| `regras/plano.json` | Idêntico, todos os valores preservados. |
| `regras/score.json` | Idêntico, todos os valores preservados. |
| `regras/servicos.json` | Idêntico, todos os valores preservados. |
| `regras/severidade.json` | Idêntico, todos os valores preservados. |

## Comportamento da visão executiva

Abre somente Panorama; títulos/setas alternam Serviços em risco, Plano de ação e Saúde das fontes. Evidências técnicas continuam disponíveis somente a admin/analista. Panorama destaca prejuízo potencial existente, gasto “A estimar”, fator predominante medido e primeira ação recomendada. Composição e histórico ficam recolhidos; nenhum orçamento financeiro foi inventado.

O simulador calcula em memória a união de falhas corrigidas, sem contar uma falha duas vezes nem remover da população máquinas corrigidas. Mantém fatores sem dados como não medidos; não grava lote, relatório ou operação. Não promete economia em dinheiro. Navegação funciona com/sem JavaScript; temas persistem apenas no navegador. Menu da conta abre Duas etapas, Tema, Trocar senha e Sair no rodapé esquerdo.

## Testes executados nesta preparação

- SDK: .NET 10.0.401 em instalação local do usuário.
- Comando obrigatório: `dotnet test --solution Piersec.slnx`.
- **289 passaram / 0 falharam / 0 ignorados.** Não há nomes de testes falhos; nenhum foi desligado ou ocultado.
- `dotnet build src/Piersec.Web/Piersec.Web.csproj --no-restore`: aprovado, 0 avisos e 0 erros.
- `tests/verificar-2fa.ps1`: aprovado — cadastro obrigatório, liberação de navegação, saída, erro visível, login TOTP e recuperação.
- `tests/verificar-envio.ps1`: aprovado — reparo fictício preserva bytes e contas; envio normal/assíncrono, arquivo desconhecido/vazio, extensão recusada, 10 mil registros fictícios e formulário sem JS com handler na URL.
- `tests/verificar-executivo.cjs`: aprovado — executivo com cadastro pendente ilegível, aviso/novo QR estável/ativação TOTP, seções, temas, apresentação, teclado, menu, permissões, histórico e simulador, com/sem JS e em 390 px.

Roteiros HTTP/navegador rodaram com contas e armazenamento temporários; apenas exemplos fictícios foram enviados. Usam exceção de validação TLS para isolar os testes da instalação do certificado local; a validação HTTPS sem exceção foi conferida separadamente durante a sessão. Testes e builds geram seus artefatos locais habituais; esses artefatos não entram na entrega.

Para repetir os roteiros, compilar o site Debug primeiro; usar PowerShell 7 nos `.ps1`. O `.cjs` precisa de Node, Playwright e Chrome local, ou caminho de navegador em `PIERSEC_NAVEGADOR`. Nenhuma dessas ferramentas/binários foi empacotada no bundle. Portas fictícias: 5443/5444/5445; instância real não foi parada nesta preparação.

## Pendências e limites honestos

- O usuário relatou falha ao entrar com 2FA do admin real e depois cancelou a investigação. Não foi diagnosticado nem corrigido esse relato; não houve redefinição da conta. O login do admin fictício passa. A correção de chave pendente não recupera chave de 2FA já ativo inacessível.
- O novo painel visual não foi aplicado ao HTML/PDF exportado; eles mantêm a estrutura existente.
- Não há orçamento real de correção; gasto permanece “A estimar”. Faixas de prejuízo são premissas existentes do exercício, não perdas comprovadas.
- Auditoria completa de acessibilidade/contraste de todos os componentes ainda pendente. Paleta principal já foi medida; isso não equivale a auditoria integral.
- Não foram criados catálogo MITRE, indicadores KEV/EPSS ou percentuais de conformidade sem base. Adaptações completas CEO/CISO/auditoria e grafo completo de evidências não foram acrescentados nesta atualização.
- Análises antigas com proteção inacessível foram preservadas localmente; recuperá-las depende da chave original válida. **Não executar `Reparar-AnalisesLocais.ps1` automaticamente no computador do Vitor.** Não usar `-DoZero` para integrar código; o reparo modifica apenas a instalação local afetada e não é passo de merge.
- HTTPS já usa `https://localhost:5001`. Certificado confiável foi configurado apenas nesta máquina; Brave temporário mostrou conexão segura. O desaparecimento do aviso no perfil original após reinício ainda não foi confirmado pelo usuário. No destino, configurar o próprio certificado com `dotnet dev-certs https --trust`; nenhum certificado/chave é enviado.

## Segurança da entrega

`.gitignore` preservado integralmente. Conferidos nomes dos arquivos novos/modificados e de todos os objetos alcançáveis por HEAD/main: não foram encontrados caminhos de dados, relatórios gerados, chaves, `.env`, binários de build ou CSVs fora de exemplos/tests. Revisados diff e arquivos novos; nenhum segredo de uso identificado.

Varredura histórica por padrões de credenciais apontou apenas fixtures fictícias preexistentes de `ValidacaoTests.cs`, usadas para testar mascaramento; não são credenciais funcionais. A presença desses marcadores não foi escondida nem levou à reescrita do histórico. Nenhum CSV ou relatório real foi lido nesta preparação. Contas, hashes de senha, segredos 2FA, certificados, chaves DPAPI/Data Protection e armazenamento local não estão no bundle.

## Diferenças do roteiro e comandos com falha

1. Projeto está na subpasta `piersec` da pasta aberta; comandos Git/.NET executados no repositório correto.
2. `git cat-file -t f4f5a35` falhou com `fatal: Not a valid object name f4f5a35`. Os outros dois commits existem; o usuário autorizou `742499a` como ponto em comum.
3. A pasta de saída não existia no início desta preparação; uma listagem com `-ErrorAction SilentlyContinue` não produziu arquivos e retornou código 1. A pasta foi criada; não houve substituição de entrega anterior neste caminho.
4. Acesso ao shell limitado ao sandbox falhou em etapas anteriores desta sessão por `helper_unknown_error`; comandos necessários foram executados após autorização de acesso local ampliado. Nenhum acesso de rede/nuvem foi usado nesta preparação.
5. Além da suíte obrigatória, foi feito build Web e três roteiros fictícios. Todos passaram nesta preparação. Não foram repetidos testes anteriores ao conserto só para gerar falhas novamente.
6. `git add -A` e inspeção do índice realizados. Foram usados dois commits com `--only` para separar 2FA e painel, seguidos de commit dos documentos restantes; histórico antigo preservado.
7. Git avisou conversão de LF para CRLF nos arquivos de texto, conforme configuração local; `git diff --check` passou. Isso não foi falha de build/teste.
8. Não houve push, download/upload para serviço de nuvem, alteração de regras JSON, leitura de CSV/relatório real, `git init` nem ZIP.

## Verificação do bundle

Criado com `git bundle create <saída>\piersec-amigo.bundle HEAD main` e aprovado por `git bundle verify`:

```text
piersec-amigo.bundle is okay
The bundle contains these 2 refs:
7f5f2be34a9128c699ac7fb98466a7931fa5bed8 HEAD
7f5f2be34a9128c699ac7fb98466a7931fa5bed8 refs/heads/main
The bundle records a complete history.
The bundle uses this hash algorithm: sha1
```

SHA-256 do arquivo para conferir após o WhatsApp: `6a7c728ce345f71fcced07f4653a2adc8ac8d4e4fa742d43da3abd0345e17a61`.

## Ao receber

Guardar os dois arquivos fora do projeto, por exemplo em `Downloads\do-amigo`, e passar o caminho para quem fará a integração. Preservar as alterações locais numa branch antes de juntar; conferir o bundle e o histórico comum, integrar sem reescrever commits e rodar os testes. Não copiar dados, contas nem chaves desta máquina. Este documento prepara a entrega; nenhuma integração no computador do Vitor foi executada aqui.
