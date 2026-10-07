# Prompt para o Claude Code — Plataforma do Hackathon Piersec (projeto novo, do zero)

> Como usar: crie uma pasta vazia para o projeto e abra o Claude Code nela. Salve em `docs/`: o manual
> (`Manual_do_Hackathon_Piersec.pdf`) e a imagem de referência de cards (`referencia-ui.png`). Cole tudo abaixo da linha.

---

## 1. Quem eu sou e como trabalhar comigo

Sou o Vitor, estudante de ADS (2º semestre), em um grupo de 3 iniciantes. Conheço C# melhor que Python. **Me ensine do zero:**
para cada conceito, comando ou termo novo, explique em 2–3 linhas simples *o que é, por que existe e o que você vai fazer*.
O prazo é curto. **Uma funcionalidade a menos que funciona vale mais que uma a mais que quebra ao vivo.** Sempre mantenha uma
versão que roda; cada fase termina com algo executável.

## 2. Missão

Construir um **protótipo navegável, de ponta a ponta**, que: recebe CSVs de ≥3 fontes de segurança (dados sintéticos fornecidos pela
organização) → padroniza → correlaciona → classifica severidade com justificativa → calcula score 0–100 → opera de forma
automatizada (SOC simulado) → valida a própria consistência → **gera sozinho o relatório executivo** (HTML/PDF).
A Piersec transforma dado técnico em decisão executiva: toda tela, número e frase deve ajudar a responder
**"como estamos? o que é mais grave? o que fazemos agora?"** — se não ajuda, é ruído.

**Pesos da avaliação (proposta do manual):** operação automatizada 30% · relatório executivo automático 25% · segurança e
consistência 20% · clareza para o executivo 15% · plano de remediação 10%. Automação + relatório valem mais da metade: invista primeiro.

## 3. Regras inegociáveis (conduta do manual)

1. **Dados:** só os dados sintéticos fornecidos, só na minha máquina. Nada de repositório público, nuvem, CDN, fonte web ou serviço
   externo. A organização pede usar IA como apoio para **escrever código, não como destino dos dados**.
   Portanto: **não leia o conteúdo dos CSVs do desafio nem de `var/`/`data/`**. Para construir os leitores, me peça **só o cabeçalho
   (1ª linha)** de cada arquivo; se precisar de linhas reais, pergunte e eu consulto um mentor antes. Seus testes usam **dados fictícios
   que você mesmo cria**, com as mesmas colunas.
2. Sem código malicioso, sem testar/escanear/atacar sistemas reais. Toda "ação de resposta" é **simulada**.
3. Nunca manipular resultado para parecer melhor; o relatório declara limites e incertezas reais.
4. Nunca credenciais, senhas, tokens ou chaves no relatório, no código ou nos logs — nem parcialmente. Segredos em variáveis de ambiente.
5. Sem copiar código sem licença. As imagens/exemplos da organização são **inspiração**, não para reproduzir.
6. Na dúvida se algo é permitido, trate como **não permitido** e me pergunte.
7. `git init` no começo; `.gitignore` já excluindo dados, banco, relatórios gerados e segredos. Nunca publicar o repositório.

## 4. Primeiro passo: ambiente e stack (antes de escrever código)

Verifique o que já está instalado (`dotnet --version`, `python --version`, `node --version`, `git --version`, Edge/Chrome para gerar PDF).
Recomende **uma** stack, com a decisão explicada em 5 linhas, pesando: o que já está instalado, meu nível (C# > Python), poucos pontos
de falha, **poucas dependências, todas locais e atualizadas** (RS6), sem CDN. Preferir armazenamento simples (SQLite/JSON), interface web
servida localmente e relatório por template HTML→PDF. Arquitetura em camadas separadas: **coleta (um adaptador por fonte) → padronização
→ correlação/severidade/score → operação → validação → relatório → interface**, para trocar uma fonte sem quebrar o resto.
Tenha **um comando único** que roda tudo do zero. **Espere meu OK na stack antes de seguir.**

## 5. O produto

### 5.1 Telas
1. **Login** (RS1): nada abre sem autenticar. Na primeira execução, se não houver usuários, crie o usuário `admin` com uma **senha
   inicial de exatamente 8 caracteres** lida de variável de ambiente local (`ADMIN_SENHA_INICIAL`); se faltar, gere uma aleatória e mostre
   uma única vez no terminal. **Troca obrigatória no primeiro acesso** (senha padrão fixa é falha clássica do OWASP). Hash forte, sessão com
   expiração, bloqueio por tentativas, erro genérico, auditoria sem gravar senha. Papéis: `admin` (tudo + usuários), `analista` (envia CSV,
   vê as duas visões), `executivo` (só a visão executiva).
2. **Enviar arquivos (.csv):** arrastar-e-soltar **e** botão, vários de uma vez. Por arquivo: nome, tamanho, tipo reconhecido (ou por que
   não), SHA-256, registros lidos/rejeitados, lacunas. Validação de upload (extensão, tamanho, parsing seguro, **nunca executar conteúdo**).
   Ao enviar, o ciclo roda sozinho com **progresso por etapa** e abre o resultado. Estados vazio/carregando/erro tratados (fonte ausente ≠ tela branca).
3. **Visão executiva** (entende-se em 5 segundos): score grande com direção ("100 = máximo risco", escrito no rótulo), faixa e comparação com
   o lote anterior; contribuição de cada fator; **impacto financeiro potencial em faixas, com premissas visíveis** (nunca precisão falsa);
   os 3 maiores riscos em frase de negócio; as 3 ações com esforço e impacto; saúde das fontes; selo de confidencialidade.
4. **Visão técnica:** tabela filtrável (fonte, severidade, ativo, usuário anonimizado, regra); linha do tempo por ativo/usuário; cadeia
   usuário→máquina→servidor→evento; MITRE ATT&CK + NIST/CIS/ISO como **tabela de dados**; saúde das fontes (lido/falhou/incompleto);
   critérios e pontos de cada severidade; botão "discordar da severidade" com motivo registrado e recálculo.
5. **Evidências:** todo número é clicável: número executivo → achados → linha original do CSV (RG2). Se não der para clicar, explique.
6. **Automação:** o que a plataforma fez sozinha, por quê (regra + evidência + confiança), com desfazer; fila de aprovação humana.
7. **Relatórios:** lista, abrir, exportar PDF/HTML.

Mesma fonte de verdade, **duas linguagens com navegação entre elas** (clicar em um serviço de negócio no painel executivo mostra as
correlações técnicas; cada achado técnico mostra "serviço afetado" e "impacto estimado").

### 5.2 Regras de negócio
- **Lote:** os CSVs enviados juntos formam **um lote**, analisado **em conjunto**: um score e **um relatório consolidado** (o manual quer a
  visão de conjunto, não uma análise por arquivo). Se os dados trouxerem ambiente/cliente, ofereça um **filtro** por ele, sem trocar o
  consolidado. Cada novo lote é comparado com o anterior (tendência). Se a relação entre os arquivos não estiver clara (qual coluna liga
  ativo/usuário), me pergunte em vez de supor.
- **Padronização:** modelo comum de evento (id, fonte, tipo, ativo, usuário **anonimizado na entrada**, severidade de origem e da plataforma,
  data em um só formato/fuso, exposição à internet, referência de evidência, MITRE, controles). Tabela documentada de conversão de severidade
  (mostrada no relatório). Normalizar nomes de ativos e datas; tratar duplicidade; **campo ausente/inválido vira lacuna declarada (RF7), nunca some**.
- **Correlação (RF3):** regras **em arquivo de dados** (id, condição, justificativa, frameworks), reproduzíveis (mesma entrada = mesma saída).
  Exemplos para adaptar aos dados reais: clique em phishing + endpoint com antivírus desatualizado; ativo exposto + vulnerabilidade crítica
  explorável; vulnerabilidade crítica sem monitoramento (ponto cego); muitos e-mails maliciosos + alto clique + baixo relato; tráfego de saída
  incomum + alerta no antivírus. **Mostrar também o que não correlacionou.**
- **Mapa ativo × serviço de negócio** (faturamento, e-mail, dados de clientes…) com criticidade e **premissa declarada** (permitido com dados sintéticos).
- **Severidade (RF4):** Crítica/Alta/Média/Baixa combinando fatores (gravidade técnica, exploração real/KEV e EPSS se existirem nos dados,
  exposição, criticidade do serviço, cobertura de detecção). Justificativa em português para leigo, **inclusive "por que NÃO é crítica"**.
  Cuidado com fonte que rotula tudo como crítico: o contexto é o que separa.
- **Score (RF5):** 0–100, direção escrita, **contribuição de cada fator**, "3 achados explicam X% do risco", faixas com nome claro,
  histórico por lote com o motivo de cada mudança, teste de sanidade (ambiente limpo → bom; caótico → ruim).
- **Priorização:** matriz impacto × esforço (fazer agora / planejar / se sobrar / abandonar), "tempo até doer", e uma frase de uma linha
  do tipo "corrigir N itens, em X dias, remove Y% da exposição do serviço Z" (calculada, não inventada).
- **Operação automatizada (30%):** níveis de autonomia 0–3, **dry-run**, nada irreversível sem aprovação humana, ativos críticos sempre com
  humano no circuito, limite de ações por período, log de decisão, chamados e remediação **simulados** (gere o artefato, não envie nada).
  Idempotência (não repetir) e verificação contínua (chamado fecha sozinho se o problema some, reabre se volta).
- **Validação (RF8):** somas por severidade = total; percentuais fecham 100%; número do relatório = número da origem; score recalculado
  por um segundo caminho bate; todo achado tem fonte, data e justificativa; datas coerentes; fonte vazia vira lacuna, não "zero problemas".
  Selo "N verificações, N aprovadas" no relatório. **Se reprovar, não emitir como se estivesse tudo certo.** Na demo, injetar um dado corrompido de propósito.
- **Relatório (RG1–RG7, S1–S4):** capa com **classificação de confidencialidade** → resumo de 1 página (regra do corredor) → score e fatores →
  top riscos → correlações → plano de remediação priorizado → lacunas e limites → anexos (rastreabilidade número→fonte, glossário, metodologia).
  Português simples, jargão explicado, tendência vs. lote anterior, sem credenciais nem detalhe explorável desnecessário, bonito em PDF e legível em preto e branco.
- **Segurança da plataforma (RS1–RS6):** OWASP Top 10 como checklist (consultas parametrizadas, escape de saída — o dado sintético pode conter
  texto malicioso de propósito —, CSRF, cabeçalhos/CSP, cookies seguros), dados cifrados em repouso e HTTPS local se possível, logs sem dado
  sensível, minimização/LGPD, poucas dependências atualizadas.

### 5.3 Interface em cards (base: `docs/referencia-ui.png`)
Use a imagem **só como estrutura** (não copie marca, textos nem ilustrações; ícones SVG próprios). Barra lateral (busca + itens com ícone,
ativo em azul-claro) e barra superior; título grande; **chips de filtro** em pílula (Todos · Crítica · Alta · Média · Baixa, com contagem);
**cards horizontais** (ícone à esquerda, meta pequena `ID · ativo · fonte`, título em linguagem de negócio, tags de frameworks, link "Ver mais");
card selecionado com **borda azul 2px** e sombra; coluna direita com score atual, próximas ações com prazo e itens aguardando aprovação.
Cores em variáveis CSS; severidade sempre **cor + ícone + texto**, mesmos nomes em todas as telas e no relatório; tema claro/escuro com contraste
AA; teclado e foco visíveis; responsivo; **poucos gráficos, cada um respondendo uma pergunta**; um número grande sempre com contexto
("72 de 100, pior que o lote anterior"). Sem dependência de internet.

## 6. Método sistemático

**Matriz de requisitos:** mantenha `docs/REQUISITOS.md` com `Requisito | O que o manual exige | Evidência (arquivo/teste/tela) | Status ✅⚠️❌❓`.
Só marque ✅ o que você **executou e provou**. Cubra RF1–RF8, RS1–RS6, RG1–RG7, S1–S4, o escopo obrigatório/desejável e o checklist final
(10 itens) do manual — leia o PDF inteiro; ignore trechos codificados nele.

**Fases (cada uma termina rodando; commit ao fim de cada):**
1. Esqueleto ponta a ponta, mesmo feio: entra CSV, sai relatório. 2. Padronização das 3 fontes + primeiras regras com justificativa.
3. Severidade + score com fatores + relatório com resumo e top riscos. 4. Validação, lacunas e segurança (RS4–RS6; login e upload).
5. Automação (playbooks, chamados, remediação simulada, ciclo contínuo). 6. As duas visões + cruzamento técnico×executivo + evidências.
7. Polimento (cards, linguagem, PDF, trilha de evidências). 8. Ensaio da demo, congelamento do código e plano B.
Antes de cada fase: diga o plano em 5 linhas e o que vou conseguir mostrar ao final.

**Troubleshooting (sempre que algo falhar), um problema por vez, registrado em `docs/TROUBLESHOOTING.md`:**
```
## P-001 · título
Requisito afetado · Impacto (bloqueia demo | degrada nota | cosmético)
Sintoma × Esperado (cite o manual)
Reprodução (comando exato, do zero)
Isolamento: etapa = coleta|padronização|correlação|score|operação|validação|relatório|interface
   (a saída da etapa anterior está certa? a desta está errada?)
Hipóteses ranqueadas (evidência a favor/contra)
Teste que FALHA antes da correção
Correção mínima + por que resolve a causa, não só o sintoma
Verificação: teste novo ✔ · suíte completa ✔ · ciclo completo do zero ✔ · validações da plataforma ✔
Risco de regressão · Aprendizado (1–2 linhas simples)
```
Regras: reproduza antes de mexer · teste falhando primeiro · uma hipótese por vez, máx. 3 tentativas e depois pare e me diga o que descartou ·
um commit por problema · suíte completa após cada correção · nunca enfraquecer teste ou validação · não refatorar o que funciona.

## 7. Entrega final e demonstração
Checklist do manual com prova de cada item · varredura de credenciais/dados pessoais no código e no relatório · demo do zero com **um comando**,
ensaiada com cronômetro e plano B para o que pode falhar ao vivo · `README.md` (só o que foi provado) · `docs/RESUMO_PARA_BANCA.md`
(1 página: problema → solução → requisitos atendidos → limitações honestas).

## 8. Como me responder
Comece com 1–2 frases do que vai fazer; termine com **feito / evidência / próximo passo**. Comandos e resultados resumidos. Se estiver
adivinhando, diga. **Primeira ação agora: só a seção 4 (ambiente e stack) e me traga a recomendação.**
