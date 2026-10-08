# Projeto Piersec — instruções para o Claude Code

Plataforma do Hackathon Piersec (Semana de TI 2026, Unisanta): recebe CSVs de segurança, padroniza, classifica,
correlaciona, calcula score 0–100, monta plano de ação e gera sozinha o relatório executivo (HTML/PDF).
O briefing completo está em **`docs/PROMPT.md`** — leia antes de qualquer tarefa, junto com `docs/REQUISITOS.md`,
`docs/FONTES.md` e `docs/TROUBLESHOOTING.md`.

> ## ⚠ LEIA ANTES DE CONTINUAR — código NÃO VERIFICADO na máquina do Ruan (08/10/2026)
>
> As alterações do commit **"Relatório em duas colunas…"** e seguintes foram **escritas e compiladas, mas NÃO
> foram executadas nem testadas**. O Windows da máquina do Ruan bloqueou os binários recém-compilados
> (Smart App Control — ver **P-020** mais abaixo), e ele pediu para parar de tentar executar ali.
>
> **PASSO 1 JÁ FEITO (08/10/2026, máquina `unisanta` da faculdade).** A suíte rodou pela primeira vez desde
> aquele commit: **303 testes, 1 falha** — exatamente o risco previsto aqui, e no arquivo previsto. O commit
> `ec2566f` renomeou o título "O que fazer agora" para "Como resolver, em ordem" e, com isso, a página 1 deixou
> de responder a terceira pergunta do manual. Corrigido no gerador (não no teste): **P-022**. Agora **303/303,
> 0 falhas**, compilação sem avisos nem erros. **Passo 3 (relatório HTML/PDF) também feito** nesta máquina, com
> 5 defeitos achados e corrigidos (P-023, P-024; ver "Continuidade — máquina da faculdade"). **Continua pendente:**
> o passo 2 no navegador — animação do score (`executivo.js`) e entrada do executivo com 0, 1 e 2 lotes.
>
> **Na próxima máquina, antes de qualquer coisa:**
> 1. `dotnet test --solution Piersec.slnx` — esperado **303 testes, 0 falhas**. Se falhar, a causa está nas
>    alterações listadas abaixo, não no ambiente.
> 2. **Leia o código com atenção** nos pontos abaixo, porque nenhum deles passou por execução:
>    - `src/Piersec.Core/Relatorio/GeradorRelatorioHtml.cs` — grid de duas colunas na página 1
>      (`.duas-colunas`), ícones nos títulos (sprite `Sprite` + helper `Ic`), "Como resolver, em ordem"
>      (lista `.passos`, que substituiu a tabela de ações), cartões de risco redesenhados e faixa de
>      indicadores (`FaixaIndicadores`). **Risco maior: HTML malformado ou texto escapado errado.**
>    - `src/Piersec.Web/wwwroot/js/executivo.js` — animação do score na simulação (`animarPara`,
>      `atualizarQueda`). **Risco maior: erro de JavaScript deixando o simulador mudo.** Conferir no
>      navegador com o console aberto, marcando e desmarcando as caixas.
>    - `src/Piersec.Web/Pages/Shared/_PainelExecutivo.cshtml` — o bloco do score grande passou para ANTES
>      do formulário, com `data-score-atual`, `data-delta-simulado` e `data-barra-simulada`.
>    - `src/Piersec.Web/Pages/Index.cshtml.cs` — **executivo agora é redirecionado** para o painel do lote
>      mais recente em vez de ver a lista. **Risco maior: quebrar teste de permissão ou criar laço de
>      redirecionamento** quando não houver lote. Conferir entrando como executivo com 0, 1 e 2 lotes.
> 3. Conferir o relatório gerado (HTML **e** PDF) com os exemplos fictícios: a página 1 deve ter as duas
>    colunas lado a lado, ícone em cada título e os passos numerados com prazo, efeito e custo.
>
> O que **foi** verificado antes do bloqueio (303 testes, 0 falhas, e print conferido): MITRE ATT&CK,
> custo de correção, gráfico de peso dos problemas e a faixa de quatro indicadores.
>
> **Revisão por leitura (08/10/2026), já que não dava para executar.** Encontrados e corrigidos 6 problemas:
> 1. **Grave (layout):** o bloco "SE FIZER SÓ UMA COISA" estava DENTRO de `.duas-colunas`, virando uma terceira
>    célula do grid e empurrando "Como resolver" para a linha de baixo — ou seja, o lado a lado não aconteceria.
>    Movido para antes do grid.
> 2. **JS:** `mostrado` só era atualizado no fim da animação; marcando duas caixas rápido o número pulava para
>    trás. Agora guarda a cada quadro o valor que está na tela.
> 3. **JS:** animação em andamento continuava escrevendo por cima do "—" quando dava erro. Criado `pararAnimacao()`.
> 4. **JS:** `alvoDelta` sem proteção contra ausência no HTML.
> 5. **Razor:** sem nada marcado, "nada marcado ainda" saía com o destaque verde de melhora, porque o atributo
>    `data-sem-mudanca` só era posto pelo JavaScript. Agora sai já do servidor.
> 6. **Entrada do executivo:** se o lote mais recente estivesse ilegível (cenário do P-019), ele cairia numa tela
>    de erro logo após o login, sem lista para onde voltar. `Index` agora confere se o lote abre, tenta os
>    seguintes e, se nenhum abrir, mantém a lista.
>
> Também conferido por leitura, sem achar problema: equilíbrio de `<div>`/`<section>`/`<table>` no HTML gerado,
> chaves do CSS do relatório e do `site.css`, e chaves/parênteses do `executivo.js`.
> **Nada disso substitui rodar.** A lista de conferência acima continua valendo.

## As duas telas e para que serve cada uma (definido pelo Ruan, 08/10/2026)

O projeto tem **duas saídas** e elas NÃO são a mesma coisa. Misturar as duas é o erro a evitar.

| | **Visão geral** (tela, `/Lote/{id}`) | **Relatório** (HTML e PDF) |
|---|---|---|
| Quem lê | o executivo, assim que entra no sistema | quem receber o arquivo |
| Para quê | bater o olho e **decidir o que fazer** | ler com calma e entender |
| Peso | **muito visual**, o mínimo de texto | **menos visual, bem estruturado** |
| Texto | só o necessário; pode usar **analogia** e comparação do dia a dia | pode ter mais texto, desde que organizado |
| Regra | se não ajuda a decidir em segundos, é ruído | quem bater o olho tem que entender a estrutura na hora |

Os dois servem à mesma ideia do projeto: **pegar a visão técnica e traduzir para um leigo**, alguém sem nenhum
conhecimento de segurança. O executivo não precisa aprender a área para decidir.

**Não é para fazer:** one-pager (tudo numa folha só) — o Ruan recusou; nem copiar layout de painel de mercado
(o `leiame.txt` do easter egg proíbe e avisa que painel só-técnico reprova no desafio); nem quebrar o que já
está funcionando a pretexto de embelezar.

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
- Conferência desta entrega: `dotnet test --solution Piersec.slnx` com SDK 10.0.401 permanente: 278 aprovados, 0 falhas e 0 ignorados.
- **P-019 diagnosticado e reparo local aplicado:** a Visão geral falhava ao carregar a chave de anonimização antiga, antes de exibir o formulário. As análises anteriores foram preservadas em `var/preservados/20261008-132715-89119317`; usuários, senhas, 2FA e chaves Data Protection mantidos. Plataforma Release reiniciada com novo histórico de análises. O usuário confirmou: “agora foi possivel enviar o csv”. Não transportar `var/` e não usar `-DoZero`. Ver procedimento e limites em `docs/TROUBLESHOOTING.md`.
- A conta local criada nesta sessão não será transportada: usuários, senhas, chaves, lotes e relatórios ficam fora do Git e do bundle.
- O pacote inclui as alterações de navegação/2FA discutidas nesta sessão; o usuário contestou a ampliação do escopo. Revisar essas alterações durante a integração, preservando a exigência original de 2FA para admin.

- Envio (08/10/2026): formulário com destino explícito para evitar handler herdado da URL. Roteiro HTTP fictício de preservação + 7 cenários de envio aprovado; suíte atual: 278 aprovados, 0 falhas, 0 ignorados. Reparar-AnalisesLocais.ps1 só deve ser usado após diagnóstico de dados inacessíveis e com a plataforma fechada; inicia novo histórico e conserva o anterior localmente.

## Continuidade — 08/10/2026 (visão executiva e temas)
- Pedido: usar os exemplos de painéis apenas como inspiração, priorizar a visão executiva e evitar azul como cor principal. Interface em neutros quentes/âmbar e escuro grafite; cores de severidade preservam seu significado.
- `/Lote/{id}`: panorama e prioridade, quatro indicadores (risco, prejuízo em faixa, tendência e confiança), composição do score, histórico de até seis análises, serviços com premissas expansíveis, plano, decisões pendentes e resumo da operação, saúde das fontes e glossário. Visão técnica preservada em seção expansível, restrita aos papéis originais.
- Seletor claro/escuro ao lado de Duas etapas; preferência salva somente no navegador. Sem escolha, acompanha o tema do sistema. Scripts locais, sem CDN, sem alterar CSP.
- Simulador "E se corrigirmos?": cálculo somente em memória, selecionando correções de programas/máquinas já propostas pelo plano. Une eventos para evitar dupla contagem, preserva população de máquinas e cobertura. Investigar ou coletar dados não promete redução. Exibe esforço e maior prazo individual; não é prazo total. Não modifica lotes, relatórios, chamados ou regras. Faixas financeiras não são recalculadas nem apresentadas como economia garantida.
- Apresentação: oculta a moldura, navega por setas esquerda/direita e sai com Esc. Evidências abrem a seção técnica correspondente para analista/admin.
- Evidência: build sem avisos/erros; 283 testes unitários aprovados (cinco novos do simulador). `tests/verificar-envio.ps1` mantém sete cenários de envio e preservação. `tests/verificar-executivo.cjs` usa Chrome/Playwright local, usuários aleatórios, exemplos fictícios e armazenamento temporário: envio, histórico, HTTP/JS/sem JS, seleção inválida, falha de rede, comparação dos arquivos antes/depois, tema persistente, apresentação/teclado, hash técnico, 390 px e permissão do executivo aprovados. Prévia clara/escura/celular conferida visualmente.
- Como repetir o novo roteiro: build Debug, Node com pacote `playwright` acessível, Chrome instalado ou `PIERSEC_NAVEGADOR` definido; `node tests/verificar-executivo.cjs`. Porta 5445 livre; nunca usa `var/` real. `PIERSEC_QA_PREVIAS` é opcional para screenshots dos exemplos fictícios.
- Limites: valores financeiros continuam as premissas existentes do exercício; não foi criado catálogo MITRE, métrica KEV nem percentual de conformidade sem dados. Relatórios HTML/PDF mantêm sua estrutura anterior. Não houve leitura de CSVs ou relatórios reais.
- Estas alterações ainda estão sem commit. O bundle anterior, em `Desktop/piersec-para-o-vitor`, não contém esta atualização visual. Gerar nova entrega somente após salvar os novos commits e conferir as exclusões.
- Aplicação local desta atualização: Release recompilado sem avisos/erros e servidor reiniciado em `https://localhost:5001/`; somente `/Entrar` e o script estático de tema foram consultados na instância real. Usuários, 2FA e análises preservados. Atualizar o navegador e abrir um lote pela Visão geral.
- Contraste calculado pela luminância relativa WCAG para texto, texto secundário, destaque e severidades sobre os dois fundos da paleta: mínimo 5,12:1 no claro e 7,49:1 no escuro. Isso confere a paleta principal; não substitui auditoria completa de todos os componentes/estados.

## Continuidade — 08/10/2026 (seções e leitura executiva)
- Ajuste solicitado: não mostrar todas as informações de uma vez. O lote abre apenas Panorama; títulos e botões ←/→ alternam Serviços em risco, Plano de ação, Saúde das fontes e, somente para analista/admin, Evidências técnicas. Uma seção visível por vez; aba ativa sublinhada, posição e limites das setas. Composição/histórico ficam recolhidos no Panorama.
- Leitura imediata: quatro cartões primeiro — prejuízo potencial em faixa, gasto para corrigir, principal causa de risco e como arrumar. Score, tendência, confiança e situação passam a apoio. Os exemplos foram inspiração; paleta continua neutra/âmbar.
- Gasto financeiro: NÃO há orçamento de correção no modelo ou nas regras existentes. Exibe "A estimar", deixando explícito que não é custo zero; mostra esforço da primeira ação como informação separada. Não foi criada estimativa em reais nem alterada regra financeira.
- Causa: fator medido com maior contribuição, somente se responde por mais da metade da nota; caso contrário, "Risco distribuído". Sem dados, "Dados insuficientes". Não afirma causa raiz de incidente. Como arrumar mostra a primeira ação priorizada, efeito calculado e prazo individual; plano completo continua na aba própria.
- Navegação: troca sem recarregar preserva a seleção do simulador; URL acompanha a aba e Voltar/Avançar funciona. Setas do teclado, Home/End nas abas, foco e relações ARIA. No modo apresentação, as setas trocam de seção. Links de premissas/cobertura abrem a aba certa; composição e hashes técnicos abrem detalhes. Sem JavaScript, os títulos têm destinos GET reais e o simulador volta para Plano de ação.
- Verificação fictícia: roteiro de navegador ampliado passou para seção única, setas/títulos/teclado, histórico do navegador, seleção preservada, atalhos dos quatro cartões, navegação sem JS, temas, apresentação, permissão de executivo, 390 px e lote sem fontes reconhecidas. Nenhum conteúdo real foi acessado. Não altera estrutura do relatório exportado.
- Resultado final desta alteração: 283 testes aprovados, 0 falhas e 0 ignorados; roteiro HTTP/navegador fictício aprovado. Release compilado sem avisos/erros e plataforma local reiniciada. Conferência na instância real limitada ao login anônimo e JavaScript estático; nenhum CSV ou relatório real lido. Atualizar com Ctrl+F5 e abrir um lote. Alterações continuam sem commit e fora do bundle anterior.

## Continuidade — 08/10/2026 (menu da conta)
- Pedido atualizado pelo exemplo do usuário: menu de conta expansível no canto inferior esquerdo. O botão mostra inicial, nome e papel; abre Duas etapas, Tema claro/escuro, Trocar senha e Sair. Esses controles deixaram o cabeçalho. São apenas opções já existentes no Piersec.
- Menu acima do botão no desktop; em celular ou pouca altura usa o fluxo normal para não cortar opções. Fechamento por Esc devolve foco ao botão; clique fora fecha. `details/summary` permite abrir e usar os links/Sair mesmo sem JavaScript. Exigências de senha/2FA e permissões não mudaram; POST de saída mantém destino explícito e token.
- Evidência: build Debug sem avisos/erros; roteiro fictício de navegador aprovado com abertura por Enter, Esc/foco, clique fora, posição, tema persistente, links e indicador da página ativa, saída com token e ausência do menu após logout, uso sem JS e 390 px sem transbordamento. Prévia clara/escura conferida. Nenhuma conta ou análise real foi lida.
- Release recompilado sem avisos/erros e plataforma local reiniciada após o ajuste. Conferência real limitada ao login anônimo e ao script estático do menu; dados e contas preservados. Atualizar o navegador com Ctrl+F5. Alterações seguem sem commit e fora do bundle anterior.

## Continuidade — 08/10/2026 (erro em Duas etapas do executivo)
- Causa confirmada por diagnóstico local somente de leitura: executivo sem 2FA ativo, com cadastro pendente que lança `CryptographicException` ao abrir a chave nesta instalação. O diagnóstico mostrou apenas estado e tipo de exceção; não exibiu login, senha, hash ou chave. Nenhum CSV ou relatório real foi lido.
- Correção: `ServicoAutenticacao.Iniciar2fa` preserva a chave pendente legível e recria somente cadastro ainda NÃO ativado cuja cifra/Base64 não possa ser aberta. A página avisa que há um novo QR code, pede adicioná-lo no app e confirmar com TOTP; registra `2fa_cadastro_pendente_reiniciado` na auditoria. GET posterior mantém o QR novo. Não redefine usuários, senhas, chaves de proteção ou fatores ativos.
- Confirmação com cadastro ilegível devolve erro orientando usar novo QR, sem ativação. Iniciar/confirmar cadastro de quem já tem 2FA ativo é recusado; segredo ativo, último passo e códigos de recuperação ficam preservados. Obrigatoriedade de admin continua valendo. Falhas de escrita/geração de chaves não são ocultadas.
- Verificação: 289 testes aprovados, 0 falhas, 0 ignorados (seis novos casos); build Debug/Release sem avisos ou erros. Chrome com conta executiva fictícia reproduziu cadastro pendente ilegível, mostrou aviso/QR, preservou a chave ao recarregar e concluiu ativação TOTP. Roteiro fictício de admin aprovou ativação, erro visível, login TOTP e recuperação. Permissões, navegação e simulador fictícios também aprovados.
- Plataforma local recompilada e reiniciada em `https://localhost:5001/`; conferência real após reinício limitada ao login anônimo (HTTP 200). Abrir Duas etapas novamente, atualizar com Ctrl+F5 e cadastrar o NOVO QR no app. Alterações continuam sem commit e não estão no bundle anterior.

## Entrega para integração — 08/10/2026
- Nova preparação autorizada por Vitor Calazans: caminho A (bundle), branch `main`, raiz `47127bb398137b5489d3e8d5e558a849eaa62e8c`. `f4f5a35` ausente; `742499a` e `d4fa492` presentes. O usuário autorizou expressamente usar `742499a` como ponto em comum após a divergência do roteiro. Nenhum histórico reescrito.
- Validação desta entrega: `dotnet test --solution Piersec.slnx` com SDK 10.0.401: 289 aprovados, 0 falhas, 0 ignorados. Build Web Debug sem avisos/erros. Roteiros locais `tests/verificar-2fa.ps1`, `tests/verificar-envio.ps1` e `tests/verificar-executivo.cjs` aprovados exclusivamente com contas, arquivos e armazenamento fictícios.
- As notas anteriores de alterações “sem commit” descrevem o estado antes desta entrega. Agora o código de recuperação do cadastro pendente de 2FA, painel executivo, simulador, temas, menu e documentação são salvos em commits separados para integração. O ponto exato e a lista dos commits ficam no `PARA-O-VITOR.md` externo.
- Limite confirmado: o usuário relatou falha ao entrar com 2FA do admin real e depois cancelou essa investigação. Nenhuma conta foi redefinida nessa etapa; não há causa confirmada nem correção validada para esse relato. O teste fictício de login admin funciona. A recuperação automática implementada trata apenas cadastro pendente ainda não ativado.
- HTTPS: certificado de desenvolvimento de `localhost` passou a ser confiável no usuário Windows desta máquina; requisição HTTPS sem ignorar validação retornou HTTP 200. Brave em perfil temporário reconheceu conexão `secure`. O Brave original estava em execução desde antes da confiança; orientado `brave://restart`. Não houve confirmação posterior do usuário sobre o desaparecimento do aviso. Certificados e confiança são locais, não viajam no bundle; na máquina de destino usar `dotnet dev-certs https --trust`.
- Pendências mantidas: orçamento de correção ausente (“A estimar”); novo layout não está no HTML/PDF exportado; auditoria completa de acessibilidade/contraste ainda não feita; catálogo MITRE/KEV/conformidade não inventado. Regras JSON e arquivos centrais de avaliação/relatório não alterados em relação a `742499a`.
- Revisão da entrega: `.gitignore` intacto; nenhum caminho proibido nos arquivos novos/modificados nem nos objetos alcançáveis por HEAD/main. Varredura por padrões de segredos encontrou somente fixtures fictícias preexistentes do teste `Filtro_encontra_e_mascara_credenciais`; nenhum segredo de uso identificado. Nenhum CSV ou relatório real lido. Entrega somente `.bundle` e resumo `.md`, fora do projeto; sem nuvem.

## Continuidade — 08/10/2026 (tela de login)
- Operador atual: Ruan (git user `Ruan`), continuando o trabalho no repositório **privado** `github.com/Artiew/Hack`.
  O push para o GitHub está autorizado por ele; os CSVs do desafio e `var/` continuam fora do git pelo `.gitignore`.
- `/Entrar` centralizada na janela (vertical e horizontal), cartão maior, marca com o escudo no topo e etiqueta
  "Protótipo · dados sintéticos · tudo local" no rodapé. Campos e botão maiores; botão ocupa a largura toda.
  Só variáveis de cor já existentes — nenhuma cor nova escrita à mão; tema claro, escuro e 500 px conferidos em print.
- Como funciona: a página define `ViewData["Entrada"]` e o `_Layout` acrescenta `conteudo-entrada` ao `<main>`;
  o CSS novo fica no fim de `site.css`. Para dar o mesmo visual a outra tela de acesso, basta o `ViewData`.
- Evidência: 289 testes aprovados, 0 falhas; prints de uma instância temporária com pasta de dados separada, já encerrada.
- Mesmo visual aplicado a **/TrocarSenha** e **/SegundoFator** (08/10/2026). A centralização usa `.principal-entrada`
  (flex) + `.conteudo-entrada` (`flex: 1`), para o cartão se centralizar no espaço que sobra quando há faixa de aviso
  no topo — é o caso da troca de senha obrigatória. `TrocarSenha` ganhou destino explícito (`asp-page`), como manda P-013.
  Ponta a ponta com usuários fictícios: troca de senha concluída pelo formulário novo; 2FA ativado, logout, login e
  código conferido na tela nova; os 3 formulários com token anti-CSRF. 289 testes aprovados.

## Continuidade — 08/10/2026 (MITRE, custo de correção e gráficos)
- **MITRE ATT&CK (item obrigatório do manual, antes ⚠️):** `regras/mitre.json` traz catálogo de 9 técnicas (nome em
  português, fase do ataque, explicação, mitigação) e três confianças: `observada`, `provavel`, `possivel`.
  `Core/Analise/MapaMitre` aponta a técnica do ALERTA pelo texto da regra do firewall (termos em `porDescricaoAlerta`)
  e a da VULNERABILIDADE pelo tipo (T1190/T1210/T1203, sempre "possível", com premissa declarada).
  **Alerta sem termo reconhecido fica sem técnica e vira lacuna declarada — nunca palpite.**
  `EventoSeguranca.Mitre` guarda os códigos; `ProcessadorLote` preenche junto com tipo de ativo e serviço.
  As 5 regras de `correlacao.json` e os tipos em `frameworks.json` saíram de `"mitre": []`.
  Validação na subida: técnica fora do catálogo, confiança inválida ou técnica sem premissa impedem a plataforma de iniciar.
  Tela: seção "Técnica de ataque" no detalhe do evento. Relatório: anexo com técnica × eventos × confiança × como reduzir,
  e a contagem dos eventos sem técnica.
- **Custo para corrigir (antes "A estimar"):** `regras/custos.json` (hora técnica em faixa + horas fixas e por máquina,
  por tipo de ação) e `Core/Analise/RegrasCustos` (`CalculadoraCustos`). Sempre FAIXA, com premissa obrigatória —
  a validação recusa faixa sem premissa. Piso nunca arredonda até zero ("trabalho que existe não custa R$ 0").
  `custos.json` declara também o que NÃO está incluído (licenças, parada do negócio, empresa externa).
- **Score por problema:** `AcaoRecomendada` ganhou `QuedaScore`, `PercentualScore`, `CustoMinimo`/`CustoMaximo`;
  `Avaliacao` ganhou `CustoTotalMinimo`/`CustoTotalMaximo`. A queda é recalculada pela MESMA calculadora do score,
  sem os eventos daquela ação: não é estimativa.
- **Gráficos (sem biblioteca, sem internet):** relatório tem barras de "peso de cada problema no score" e de
  "prejuízo potencial × custo do plano" na mesma escala, legíveis em preto e branco; a tela ganhou o painel
  "Peso de cada problema" e, em cada ação do plano, "melhora X% do score · custa entre R$ A e R$ B".
- **Estado da verificação: 303 testes aprovados, 0 falhas** (13 novos em `MitreECustosTests`), solução sem avisos nem erros.
  Durante o caminho a suíte acusou o piso do custo arredondando para zero, corrigido em `ArredondarParaBaixo` com teste
  próprio (`Acao_pequena_nao_custa_zero_reais`). Relatório conferido em print, gerado por envio real dos exemplos
  fictícios numa instância temporária (login + 2FA + upload), depois apagada: anexo MITRE com a lacuna declarada
  ("25 eventos sem técnica"), gráfico de peso dos problemas e comparação prejuízo × custo.
- Ajustes depois da primeira conferência: "não calculado" virou **"não baixa o score"** (ação que não remove achado),
  e `arredondarPara` passou de 500 para **250**, senão todas as ações saíam com a mesma faixa de preço.
- **P-020 · Smart App Control bloqueia os binários recém-compilados.** Windows 11 com Smart App Control LIGADO
  (`HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy` → `VerifiedAndReputablePolicyState = 1`) recusa carregar os
  `.dll` novos do projeto: `FileLoadException ... (0x800711C7)`. Não é vírus (o Defender não registrou detecção).
  O veredito **oscila**: às vezes libera minutos depois, às vezes volta a bloquear no binário seguinte. Contornos
  testados e SEM efeito: recompilar do zero, `dotnet exec`, copiar a saída para outra pasta. WSL não está instalado.
  Solução real: desligar o Smart App Control (decisão do usuário; é de mão única) ou rodar a suíte em outra máquina.
  Observação para a banca: o Defender marcou `_Layout.cshtml` dentro do zip baixado do GitHub (ID 2147751841) — falso positivo.

## Continuidade — 08/10/2026 (relatório em duas colunas, score animado, entrada do executivo)
- **NÃO VERIFICADO** — ver o aviso no topo deste arquivo. Compila sem avisos nem erros; nada foi executado.
- Relatório HTML/PDF, página 1: `.duas-colunas` coloca **"O que é mais grave"** (cartões) ao lado de
  **"Como resolver, em ordem"**. A tabela de ações virou lista numerada `.passos`: cada passo traz prazo,
  esforço, efeito no score e custo. Pedido do Ruan: "o relatório tem que dar a direção na hora que o
  executivo ler do como resolver".
- Ícones próprios em cada título de seção (sprite SVG dentro do próprio HTML, `Sprite` + `Ic("nome")`):
  resumo, negócio, score, plano, robô, limite, alvo. Nada vem da internet, funciona no PDF.
- Cartões de risco: número + selo de gravidade, título em destaque, **uma frase forte** (o "por que importa"),
  detalhe em letra menor e "O que fazer" separado por um traço no pé do cartão.
- Simulação na tela: o score virou número **grande no topo da aba Plano de ação**, antes das caixas de seleção.
  Ao marcar uma correção o número **desce contando** (`animarPara`, ~550 ms, com easing), aparece "−X% do risco"
  e uma barra encolhe. Respeita `prefers-reduced-motion`. A recalculação ao vivo já existia (`change` no
  formulário); o que foi acrescentado é a animação e o destaque.
- **Entrada do executivo:** `Index.OnGet` agora redireciona quem não pode enviar (executivo) para o painel do
  lote mais recente. Ele não escolhe mais arquivo nem lote; o histórico continua dentro do painel. Admin e
  analista seguem vendo a lista e o envio.
- Referência de layout: o Ruan mandou painéis de mercado (WhatsApp do grupo) e o one-pager do SlideTeam.
  **São inspiração, não modelo** — o `leiame.txt` do easter egg da organização diz expressamente "não as
  reproduza" e avisa que painel só-técnico NÃO cumpre o desafio. O caminho seguido é o oposto: prejuízo em
  reais, custo para corrigir e passos com prazo.
- Pedido ainda em aberto: transformar a página 1 num **one-pager de verdade** (tudo numa folha A4), com as
  seções detalhadas vindo depois. O Ruan aprovou o grid e os ícones; o one-pager completo não foi começado.

- **Falta, nesta ordem:** 1) conferir o relatório novo (HTML e PDF) página por página, com os gráficos; 3) "tempo até doer";
  4) deixar o relatório mais técnico e mais bonito, pedido do Ruan; 5) `docs/RESUMO_PARA_BANCA.md`;
  6) linha do tempo por máquina, mapa de calor, triagem de alertas; 7) rodar com os 3 CSVs reais
  (a máquina do Ruan não tem `dados/`). MITRE ATT&CK saiu da lista: foi feito nesta sessão.
- Análise do easter egg da organização (`qqdiz.com.br/bash.html`): os documentos escondidos confirmam os pesos do manual
  e as fases; o que falta no projeto está listado acima. Máquina do Ruan não tem `dados/` nem `var/`.

## Continuidade — 08/10/2026 (máquina da faculdade `unisanta`: suíte finalmente rodou)
- **Ambiente desta máquina:** Windows 10 Education **19045.2193** — o MESMO build do P-015, então o **CET vale aqui**
  (os `.exe` do .NET 10 param com "doesn't fully support CET"; o `dotnet.exe` funciona). O `rodar.ps1` liga o
  contorno sozinho. Smart App Control **não** existe no Windows 10, então o bloqueio do P-020/CLAUDE.md não aparece:
  é a máquina onde dá para rodar a suíte.
- **SDK:** o PATH tinha só .NET 6.0.101/6.0.108. Instalado o **10.0.401 em `C:\Users\unisanta\.dotnet`** pelo
  `dotnet-install.ps1` oficial (só para o usuário, sem administrador), local que o `rodar.ps1` já procura.
  `global.json` preservado. Para rodar à mão nesta máquina, numa janela de PowerShell:
  `$d="$HOME\.dotnet"; $env:DOTNET_ROOT=$d; $env:Path="$d;$env:Path"` e o contorno do P-015
  (`CscToolPath`/`CscToolExe` apontando para `var\contorno-cet\csc.cmd`, `UseSharedCompilation=false`,
  `UseAppHost=false`); os testes por `dotnet exec tests\Piersec.Tests\bin\Release\net10.0\Piersec.Tests.dll`.
  Sem o `DOTNET_ROOT` o erro é "You must install or update .NET", que **não** é o CET — não confundir os dois.
- **Resultado:** 303 testes, 1 falha → corrigida (P-022) → **303/303, 0 falhas**, build Release sem avisos nem erros.
  Não há `dados/` nem `var/` aqui: nenhum CSV real e nenhum relatório real foi lido ou gerado.
- **Relatório conferido (passo 3 do aviso do topo), com os exemplos fictícios** numa instância temporária (porta 5447,
  pasta de dados própria, usuário analista fictício; apagada depois). HTML e PDF baixados pelas rotas reais, PDF
  convertido em imagem página a página (diretoria: 8 páginas; completo: 22). **Aprovado:** duas colunas lado a lado,
  ícone em cada título, passos numerados com prazo/esforço/efeito/custo, faixa de 4 indicadores, selo "15 verificações,
  15 aprovadas", tags equilibradas (div/section/table/ol), nenhum texto escapado duas vezes, números do score batendo.
  **Defeitos encontrados nessa conferência — TODOS CORRIGIDOS** (pedido do Ruan: "arrume os erros e faça o commit"):
  1. PDF: 3º cartão de risco ia sozinho para a página 2 → cabe na página 1 (P-024).
  2. PDF: título "4. Plano de ação completo" órfão no pé da página → seção 4 começa em página nova (P-024).
  3. Números do plano: "Melhora no score" calculada isolada não somava o "50 → 6" → agora na ORDEM DO PLANO,
     contando as anteriores como feitas; fictícios: −40 e −4, soma 44 = 50 − 6 (P-023). Veio do `93ff58c`.
  4. "(1 servidor e 0 outras)" → "(1 servidor)".
  5. Matriz impacto × esforço 2 × 2 → 3 × 3 (cada ação na sua célula); anexo MITRE virou "I." depois do H
     (severidades ajustadas: "J."); "Phishing" não se separa mais da definição.
  Conferido de novo pela plataforma (instância temporária, fictícios): diretoria 8 → 7 páginas, completo 22 → 21.
  Suíte: **306/306**, 0 avisos. Os 3 testes novos e os 2 ajustados FALHAM com o código antigo (conferido com `git stash`).
- **Tela do lote:** abre com 200 depois do envio (302 → GET). Um 404 que apareceu no roteiro era do cliente HTTP do
  PowerShell 5.1 seguindo o 302 sozinho; seguindo à mão, como o navegador, dá 200. Não é defeito da plataforma.
- **Ainda não feito:** animação do score no navegador com o console aberto (`executivo.js`) e entrada do executivo
  com 0, 1 e 2 lotes (risco de laço no redirecionamento do `Index`). Esta máquina não tem Node, então o roteiro
  `tests/verificar-executivo.cjs` não roda aqui.
- **Site real no ar** em `https://localhost:5001` com `var/` novo (admin criado nesta sessão; senha inicial mostrada
  uma vez no terminal, troca e 2FA obrigatórios no 1º acesso). O certificado de `localhost` NÃO está confiável nesta
  máquina (P-021): o navegador avisa "não seguro" até rodar `dotnet dev-certs https --trust`.
- **Numeração do troubleshooting:** o Smart App Control está chamado de "P-020" na seção anterior deste arquivo, mas
  em `docs/TROUBLESHOOTING.md` o P-020 é o QR code e o P-021 é o Brave. O Smart App Control **não está registrado**
  no troubleshooting; quando for registrado, será P-025 (P-022 título da página 1, P-023 números do plano, P-024 PDF).
- **Identidade do git neste clone:** estava vazia; configurada SÓ no repositório (`git config` local) como
  `Ruan <tavaresdasilvaruan@gmail.com>`, igual aos commits anteriores. Remote: `github.com/Artiew/Hack` (privado).
  Nesta sessão houve só commit local, sem push.
