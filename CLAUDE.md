# Projeto Piersec — instruções para o Claude Code

Plataforma do Hackathon Piersec (Semana de TI 2026, Unisanta): recebe CSVs de segurança, padroniza, classifica,
correlaciona, calcula score 0–100, monta plano de ação e gera sozinha o relatório executivo (HTML/PDF).
O briefing completo está em **`docs/PROMPT.md`** — leia antes de qualquer tarefa, junto com `docs/REQUISITOS.md`,
`docs/FONTES.md` e `docs/TROUBLESHOOTING.md`.

## Quem é o usuário e como responder
- Vitor, estudante de ADS (2º semestre), grupo de 3 iniciantes; sabe C# básico.
- Responda em português. Para cada conceito, comando ou termo novo: 2–3 linhas simples (o que é, por que existe, o que vai fazer).
- Comece com 1–2 frases do que vai fazer; termine com **feito / evidência / próximo passo**. Se estiver adivinhando, diga.
- Antes de cada fase: plano em 5 linhas e o que ele vai conseguir mostrar no fim.
- Texto que o EXECUTIVO lê (página 1, tela do lote, frases-resumo): linguagem de diretoria — "problema" (não "achado"),
  cada item como ação ("Atualizar o programa X, que está em N máquinas"), efeito em faixa ("de alto para moderado") com
  números entre parênteses; sem número de regra, CVE ou "pontos do score" (isso vai em letra pequena ou nos anexos).
  Quando o número não sustenta a mensagem, diga com todas as letras (ex.: "o risco está espalhado"). O Vitor aprovou esse padrão.
- Plural sempre certo ("1 máquina", "3 máquinas"): use `Formatar.Qtd`, nunca "(s)".
- Formulário com POST: sempre com destino explícito (`asp-page` / `asp-page-handler`); nunca `action="/..."` escrito à mão
  (perde o token anti-CSRF — P-013) nem sem destino numa página com handlers (cai no handler errado — P-014).
- Teste de ponta a ponta: refaça o caminho como o usuário faria (token da própria página, endereço que o formulário indica).

## Regras inegociáveis
- **Nunca leia o conteúdo dos CSVs do desafio** (`dados/`, `var/`, arquivos `Eventos SIEM-*.csv`) nem os relatórios gerados a partir deles.
  Para entender os dados, escreva um script local que imprima **só contagens e "moldes" de formato** (dígito → 9, letra → A),
  mostre o script ao Vitor antes e rode. Testes usam só dados fictícios (`exemplos/` e os criados nos testes).
- **Nada de nuvem:** sem `git push`, sem GitHub, sem OneDrive/Drive. O projeto viaja por pen drive (ver abaixo).
- Sem credenciais/senhas/tokens no código, nos logs ou no relatório. Ações de "resposta" são sempre simuladas.
- Nunca maquiar resultado: o relatório declara limites e incertezas reais.
- Na dúvida se algo é permitido, trate como **não permitido** e pergunte ao Vitor (que consulta um mentor).
- Só marque ✅ em `docs/REQUISITOS.md` o que foi executado e provado.

## Estado atual
> **Mantenha esta seção em dia.** Ao fim de cada sessão (ou antes de gerar um bundle), atualize o que foi feito,
> o próximo passo e as pendências, e inclua no commit. É assim que a sessão no outro computador fica sabendo.

- **Fases 1, 2 e 3 concluídas** (esqueleto, fontes reais + correlação, score + plano + relatório para leigo).
- **Feito depois da Fase 3 (07/10/2026):**
  - aviso de **confiança baixa junto do número** na página 1 e na tela do lote; seção 2 explica por que mais fontes mudam o score;
  - aviso de **empresas/ambientes possivelmente diferentes** quando dois arquivos do mesmo tipo não têm nenhuma máquina em
    comum (`VerificadorAmbientes`). É aviso, não erro. Nos dados reais aparece para SIEM-1 × SIEM-2.
  - 144 testes passando.
- **Fase 4 concluída** (validação, login + auditoria + proteções do site, dados cifrados em repouso):
  - **Parte 1 feita:** validação RF8 (`Validacao/ValidadorLote`, 12 verificações, selo no relatório, "NÃO VALIDADO" se
    reprovar, relatório retido se sobrar credencial). Demo: `exemplos/alertas-corrompido-ficticio.csv` reprova a V04.
    Exemplos fictícios são gerados por `exemplos/gerar-exemplos.ps1`.
  - **Parte 2 feita:** login RS1 (`Core/Seguranca`: PBKDF2, bloqueio, troca obrigatória, papéis), auditoria RS4
    (`var/auditoria.jsonl`, página /Auditoria), cabeçalhos/CSP/cookie `__Host-`, limite de 10 logins/min, páginas
    Entrar/TrocarSenha/Sair/Usuarios/Auditoria/Negado. Executivo não envia e não vê tabelas técnicas.
    Para testar com senha conhecida: `$env:ADMIN_SENHA_INICIAL='8chars!!'` antes do `rodar.cmd -DoZero`.
  - **Parte 3 feita:** lotes, resumos, relatórios e chave de anonimização gravados cifrados (`ICifrador`, Web usa Data
    Protection com chaves em `var/chaves` + DPAPI). PDF gerado em `var/tmp` e apagado. 192 testes passando.
- **Feito depois da Fase 4:** senha mínima de **8** (NIST; 12 estava errado, pedido do Vitor); site e relatório na tela com
  letra maior e mais espaço (PDF continua compacto); atalhos de dois cliques `Abrir Plataforma.cmd` e
  `Esqueci a senha do admin.cmd` (sem senha guardada em atalho).
- **Fase 5 concluída (07/10/2026) — operação automatizada (SOC simulado):**
  - `regras/automacao.json`: nível de autonomia 0–3 (hoje 2), `modoEnsaio` (dry-run), `limiteAcoesPorDia` (20),
    `tiposCriticos` (servidor, rede) e 5 playbooks: atualizar programa (chamado), corrigir máquina (impacto médio),
    isolar máquina (alto impacto, sempre com aprovação), investigar pico de alertas, medir fator (só sugestão).
  - `Core/Operacao`: `Gatilhos` (problemas com chave estável), `MotorDecisao` (travas em ordem fixa + V13 que confere
    as travas por outro caminho), `ServicoOperacao` (idempotente; fecha sozinho quando some, reabre quando volta; sem
    dados do tipo, NÃO fecha; respeita rejeitar/desfazer humano; artefato .txt), `RepositorioChamados`
    (`var/operacao/estado.json.cifrado`). Roda dentro do `CicloAnalise`, depois da validação.
  - Tela **/Automacao** (fila de aprovação, sugestões, abertos com desfazer, último lote, ensaio por nível, log de
    decisão); `/automacao/CH-NNNN.txt`; resumo na tela do lote; seção 4 do relatório (limites viraram seção 5).
    Executivo só vê; admin/analista decidem; tudo na auditoria. 212 testes passando.
- **Depois da Fase 5:** linhas repetidas contam uma vez só (lacuna com a linha); "faltou o arquivo de alertas/vulnerabilidades"
  (RF7); "3 achados explicam X% do risco" (ou "risco espalhado"); `.txt` do chamado sem "Eventos: 0". 224 testes.
- **Fase 6 concluída (07/10/2026):**
  - `regras/servicos.json`: máquina → serviço (nome, depois tipo; PREMISSA declarada) e faixa de prejuízo por serviço.
    Prejuízo potencial = soma dos serviços em risco (nota ≥ 40). Página 1 ganhou uma linha; seção 2 nova (as outras
    foram renumeradas: 3 score, 4 plano, 5 automação, 6 limites). Impressão compacta: resumo da diretoria cabe em 1 folha.
  - Visão técnica: `/Lote/{id}/Eventos` (filtros) e `/Lote/{id}/Evento/{ev}` (arquivo, linha, SHA-256); números da tela
    do lote clicáveis; tabela por ambiente; política "Tecnico" (admin/analista); `LotesEmMemoria` (cache 3 lotes/10 min).
  - Discordar da severidade: no detalhe do evento; cria REVISÃO (`ProcessadorLote.Revisar`, `CicloAnalise.RevisarAsync`),
    o original não muda; aviso na página 1 + anexo I; V14 (serviços) e V15 (ajustes). 248 testes.
  - Texto para executivo: ação + faixa, sem jargão (ver memória/feedback do Vitor).
- **Fase 7 em andamento (07/10/2026):**
  - Feito: moldura com barra lateral (busca, ícones SVG próprios), tela do lote em cards (riscos, achados com chips,
    card selecionado azul, coluna direita com score/próximas ações/aprovações), severidade = cor + ícone + texto
    (`Visual.Selo`), envio com arrastar-e-soltar e progresso por etapa (`ServicoEnvios` + `wwwroot/js/envio.js`; sem JS
    o formulário normal funciona). P-013: o botão Sair não tinha token (corrigido com `asp-page`).
  - Usuários: admin **exclui** usuários (confirmação; não exclui a si mesmo nem o último admin); sessão do excluído cai
    no próximo clique (`OnValidatePrincipal` no cookie). P-014: criar depois de excluir caía no handler errado (corrigido).
    O Vitor NÃO quer botão de "nova senha temporária" (pediu só excluir). 251 testes.
  - **Relatório da diretoria (pedido do Vitor, na faculdade):** o executivo recebe só Resumo, 2. Serviços e prejuízo,
    3. Score, 4. Plano completo e Glossário (`VersaoRelatorio.Diretoria`, arquivos `relatorio-diretoria.*`). Admin e
    analista continuam com o relatório completo + botão "Versão da diretoria". Lotes antigos não têm essa versão: o
    executivo vê um aviso para reenviar. 252 testes.
  - PC da faculdade: Windows sem atualização quebra os .exe do .NET 10 (P-015); o `rodar.ps1` liga o contorno sozinho.
  - **2FA (pedido do Vitor, na faculdade):** código do app de autenticação (TOTP), obrigatório para admin, opcional para
    os outros (decisão do Vitor); QR code próprio (`Core/Seguranca/QrCode.cs`, sem pacote); páginas `/SegundoFator` e
    `/DoisFatores`; recuperação por 8 códigos de uso único ou pelo "Esqueci a senha do admin". 272 testes.
    **Pendente: o Vitor ler o QR code com o celular de verdade** (a estrutura foi conferida em imagem, e o código
    calculado por fora bate com o do site). Admin zerar o 2FA de outro usuário (celular perdido) ainda não existe.
  - **Sessão na faculdade (07/10/2026), para o Claude de casa:** 5 commits depois de `f57c016` (P-015/CET, relatório da
    diretoria, docs, `rodar.ps1` com contorno automático, 2FA). Em casa: `git pull`, rodar os testes (272), abrir o site
    e conferir. **Atenção:** em casa o admin já existente vai cair direto no cadastro do 2FA no próximo login (obrigatório
    para admin): ter o celular à mão. Os 3 lotes antigos de casa não têm a versão da diretoria (reenviar para gerar).
    O pen drive também tem uma cópia de trabalho em `<PEN>:\piersec` (com `var\` próprio) e o .NET 10 em `<PEN>:\.dotnet`.
  - **Falta, nesta ordem (recomendada ao Vitor):** 1) Automação no visual de cards/chips (vale 30% da nota);
    2) conferir o PDF página por página; 3) revisão de linguagem; 4) visão técnica (chips na lista de eventos,
    detalhe em cards); 5) telas Entrar/Trocar senha. Estimativa: 2 a 3 horas.
  - Prints de conferência: o Edge headless não fica mais estreito que ~500 px (print de 390 px corta; use 500).
    PDF → imagem: script PowerShell com `Windows.Data.Pdf` (não há poppler na máquina).
- Pendentes pequenos: linha do tempo por máquina (ordenar por data), severidade usando a criticidade do serviço (RF4),
  rodar com os 3 arquivos reais e conferir contagens. Pen drive: gerar com `git bundle create <PEN>:\piersec.bundle HEAD main`.
- Atenção: `rodar.cmd -DoZero` apaga também usuários e chaves; na 1ª execução a senha do admin aparece UMA vez no terminal.
- Ideia aprovada para a Fase 6: filtro por ambiente (cada arquivo separado) sem precisar reenviar.
- Perguntas ao mentor ainda abertas: fim de `docs/FONTES.md` (inclui: SIEM-1 e SIEM-2 são empresas diferentes?).

## Como rodar e testar
- Tudo a partir da raiz do projeto (o `global.json` daqui define o SDK 10 e o runner de testes).
- `.\rodar.cmd` (ou `.\rodar.cmd -DoZero` para apagar `var/`): compila, roda os testes e sobe `https://localhost:5001`.
  O site fica no ar enquanto o processo roda — rode em segundo plano e não feche a janela.
- Só testes: `dotnet test --solution Piersec.slnx`.
- Regras de negócio editáveis em `regras/*.json` (validadas ao subir; erro impede a plataforma de iniciar).
- Windows PowerShell 5.1: mensagem de commit com aspas → escreva num arquivo e use `git commit -F arquivo`.
- Commits terminam com a linha de co-autoria indicada pelo ambiente.

## Trabalhar em dois computadores (pen drive)
- **Levar daqui:** `git bundle create <PEN>:\piersec.bundle HEAD main` (commit tudo antes; o bundle leva só o que foi commitado).
- **Na faculdade:** o Vitor abre o Claude Code e pede para ler `<PEN>:\CLAUDE-FACULDADE.md`.
- **Trazer da faculdade:** `git pull <PEN>:\piersec-faculdade.bundle main`, depois rode os testes, leia `git log` dos commits
  novos e a seção "Estado atual" acima, e resuma ao Vitor o que mudou.
- Os CSVs do desafio **não** vão no pen drive sem autorização do mentor.

## Continuidade — 08/10/2026 (Automação)
- Instruções inicialmente encontradas em `aaaa.md`, com `CLAUDE.md` excluído localmente. Para a entrega ao Vitor, o usuário autorizou restaurar o nome `CLAUDE.md`, preservando o conteúdo atualizado; `aaaa.md` deixou de existir.
- Automação: resumo em cartões, atalhos por fila com contagem, cartões para aprovação/sugestões/abertos, estados com ícone + texto + cor, detalhes expansíveis e estado vazio com navegação.
- Formulários de Aprovar/Rejeitar/Desfazer apontam explicitamente para `/Automacao`; permissões e regras de negócio preservadas.
- Verificação: compilação da solução sem avisos/erros; 272/272 testes aprovados. Instância temporária com exemplos fictícios: estado vazio, 13 cartões, tokens nos POSTs, aprovação e botão Desfazer, executivo sem botões de decisão. Prévia do HTML renderizado conferida no navegador em claro/escuro e 500 px, sem transbordamento horizontal.
- SDK desta máquina: PATH tinha só 9.0.312. Instalado 10.0.401 em `%TEMP%\piersec-dotnet-10`, sem alterar `global.json`. Para esta sessão, adicionar essa pasta ao PATH e definir DOTNET_ROOT para ela antes de compilar/rodar. Atualização: SDK 10.0.401 instalado permanentemente em C:\Users\pop\.dotnet em 08/10/2026, local já reconhecido pelo rodar.ps1. A instalação temporária não é mais necessária.
- Nenhum CSV real, conteúdo de `var/` ou relatório real foi lido. A conferência usou armazenamento temporário separado e o servidor temporário foi encerrado.
- Próximo passo: conferir PDF com exemplos fictícios; revisar linguagem e depois visão técnica. Rejeitar e Desfazer continuam cobertos pela suíte de operação, mas não foram executados pelo navegador nesta sessão.

- 2FA (08/10/2026): mensagem explícita ao confirmar sem código, sem consumir tentativas; instrução do campo mais clara. 275/275 testes, fluxo HTTP fictício válido/inválido/recuperação aprovado. P-017 registra o limite: falha com código preenchido ainda não reproduzida. Reiniciar pelo atalho para recompilar a versão Release usada normalmente.

- Navegação/2FA (08/10/2026): P-018 corrigido. Menu só aparece após concluir as etapas obrigatórias; orientação e saída disponíveis no cadastro. Sessao.Gravar atualiza também HttpContext.User para não gerar formulários com identidade anterior após ativar 2FA. Build aprovado, 277 testes e roteiro HTTP tests/verificar-2fa.ps1 aprovado. Reiniciar pelo atalho para carregar a correção.

## Entrega para integração — 08/10/2026
- Repositório original confirmado: raiz `47127bb`; branch `main`; ponto em comum `742499a`. `d4fa492` existe; `f4f5a35` não existe nesta cópia.
- Autoria dos novos commits: Vitor Calazans, conforme autorização do usuário para manter a configuração existente.
- Conferência desta entrega: `dotnet test --solution Piersec.slnx` com SDK 10.0.401 permanente: 277 aprovados, 0 falhas e 0 ignorados.
- **Pendência não resolvida:** o usuário relatou “Algo deu errado” e impossibilidade de enviar CSV na Visão geral da instalação atual. O envio funcionou em instância isolada com exemplos fictícios. A exceção do terminal da instalação real não foi obtida; a causa ainda não foi identificada. Não considerar esse relato corrigido nem rodar `-DoZero` como solução.
- A conta local criada nesta sessão não será transportada: usuários, senhas, chaves, lotes e relatórios ficam fora do Git e do bundle.
- O pacote inclui as alterações de navegação/2FA discutidas nesta sessão; o usuário contestou a ampliação do escopo. Revisar essas alterações durante a integração, preservando a exigência original de 2FA para admin.
