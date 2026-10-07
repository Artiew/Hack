# Matriz de requisitos

Regra: ✅ só para o que foi **executado e provado**. ⚠️ parcial · ❌ não feito · ❓ depende de informação que ainda não temos.
Atualizado ao fim da **Fase 3** (score, priorização, plano de ação, relatório para leigo). Números dos dados reais vêm de contagens (`docs/FONTES.md`).

## Requisitos funcionais (manual 4.1)

| Req. | O que o manual exige | Evidência (arquivo/teste/tela) | Status |
|---|---|---|---|
| RF1 | Ler ≥ 3 fontes diferentes | 3 arquivos reais lidos de ponta a ponta: 29.564 registros, 0 rejeitados (`AdaptadorVulnerabilidades`, `AdaptadorAlertasSiem`, `FontesReaisTests`). Por estrutura são **2 ferramentas** em 3 arquivos: contagem de "fontes" aguarda o mentor | ⚠️ |
| RF2 | Converter para modelo comum | `EventoSeguranca` para as fontes reais: datas → UTC (10.000/10.000 alertas com data), ativos normalizados, CVSS com ponto ou vírgula, Windows-1252 tratado (P-007), linhas embrulhadas e data partida tratadas (P-005). Testes `FontesReaisTests`, `PadronizacaoTests`, `LeitorCsvTests` | ✅ |
| RF3 | Correlacionar com regras claras e reproduzíveis | 5 regras em `regras/correlacao.json` (validadas na inicialização), com justificativa e frameworks; mesma entrada = mesma saída (`Mesma_entrada_gera_exatamente_a_mesma_saida`); regras sem resultado mostram o motivo. Dados reais: 548 correlações. Limite: **sem chave comum entre alertas e vulnerabilidades** (0 máquinas em comum); MITRE pendente | ⚠️ |
| RF4 | Crítica/Alta/Média/Baixa com justificativa | `ClassificadorSeveridade` + `regras/severidade.json` (CVSS oficial, níveis do SIEM, textos). 100% dos eventos reais com justificativa, inclusive "por que não é Crítica" (`SeveridadeTests`). Falta o contexto (exposição, serviço) — Fase 3 | ⚠️ |
| RF5 | Score 0–100 com fatores | `CalculadoraScore` + `regras/score.json`: direção escrita ("100 = risco máximo"), 5 fatores com contribuição (soma = score, teste), fator sem dados = "não medido" (não vira zero), confiança, faixas com nome, comparação com o lote anterior e motivo (`Comparacao_com_o_lote_anterior...`), sanidade: limpo → 0 "Baixo", caótico → ≥ 80 "Crítico". Dados reais: 74 (Alto), confiança média (55% do peso medido) | ✅ |
| RF6 | Relatório gerado automaticamente | Upload dos 3 arquivos reais → HTML + PDF em ~3,5 s, sem intervenção (ensaio do zero com `rodar.cmd -DoZero`). Página 1 para a diretoria (como estamos / mais grave / o que fazer / se fizer só uma coisa), seções de score, plano e limites, anexos técnicos e glossário. Falta validação antes de emitir (RF8) | ⚠️ |
| RF7 | Sinalizar lacunas | Por campo, linha, arquivo, fonte ("não informa data/usuário/exposição") e lote; aviso de possível corte em 10.000 linhas; lacunas agrupadas no relatório. Falta: fonte esperada ausente | ⚠️ |
| RF8 | Validar consistência antes de emitir | `ValidadorLote`: 12 verificações antes de emitir (somas, lidos × eventos, origem de todo evento, datas coerentes, fonte vazia = lacuna, correlações íntegras, contribuições = score, **score recalculado por segundo caminho**, percentuais do plano, máquinas por tipo, credenciais mascaradas, relatório final relido). Selo "N verificações, N aprovadas"; se reprovar, relatório marcado "NÃO VALIDADO". Dados reais: 12/12; arquivo corrompido de propósito (`exemplos/alertas-corrompido-ficticio.csv`): V04 reprova (`ValidacaoTests`) | ✅ |

## Requisitos de segurança (manual 4.2)

| Req. | O que o manual exige | Evidência | Status |
|---|---|---|---|
| RS1 | Autenticação | Nada abre sem login (páginas e relatórios → 302 para /Entrar). `admin` criado na 1ª execução com senha de `ADMIN_SENHA_INICIAL` (exatamente 8) ou sorteada e mostrada uma vez; troca obrigatória (toda página leva a /TrocarSenha); PBKDF2-SHA256 600 mil iterações; bloqueio após 5 erros por 15 min; erro idêntico para usuário inexistente/senha errada/bloqueado; sessão de 30 min; papéis admin/analista/executivo (executivo não envia nem vê tabelas técnicas; /Usuarios → /Negado). `SegurancaTests` (24) + teste de ponta a ponta com curl | ✅ |
| RS2 | OWASP Top 10 | CSRF (POST sem token → 400; sair só por POST), escape de HTML (testes + PDF), path traversal (→ 404), validação de upload, CSP restrita no site e no relatório, `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cache-Control: no-store`, sem cabeçalho `Server`, cookie `__Host-` Secure/HttpOnly/SameSite=Strict, limite de 10 logins/min por endereço (429), erro sem detalhe técnico em produção | ✅ |
| RS3 | Proteger dados em trânsito e em repouso | HTTPS local em `https://localhost:5001` com certificado confiável (`dotnet dev-certs https --check --trust` → confiável, válido até 07/10/2027). Em repouso: `var/` ainda não cifrado | ⚠️ |
| RS4 | Registrar atividades sem dados sensíveis | `var/auditoria.jsonl` (só acrescenta): entrada, falha, bloqueio, saída, troca de senha, usuário criado, lote processado, relatório aberto, envio negado; login inexistente vira "(desconhecido)". Página /Auditoria (admin). Teste: nenhuma senha usada aparece no log nem em `usuarios.json` | ✅ |
| RS5 | Minimização / LGPD | Anonimização de usuário pronta (as fontes reais não trazem usuário); CSV bruto não é guardado | ⚠️ |
| RS6 | Dependências confiáveis e atualizadas | Produção: **0 pacotes NuGet** (só .NET 10.0.401). Testes: xunit.v3 4.0.1, Test.Sdk 18.10.1, runner 4.0.0. `--vulnerable --include-transitive` = nenhum; `--outdated` = nenhum (07/10/2026, após a Fase 2) | ✅ |

## Regras do relatório (manual 7.1) e da saída (manual 8)

| Req. | O que o manual exige | Evidência | Status |
|---|---|---|---|
| RG1 | Gerado pela plataforma | `CicloAnalise` gera HTML e PDF a partir do lote; metodologia lida dos mesmos JSON usados na análise | ✅ |
| RG2 | Todo número rastreável à fonte | Evento → `arquivo:linha` + SHA-256; correlação → lista de eventos e máquinas. Números do resumo ainda não são clicáveis | ⚠️ |
| RG3 | Declarar limites e incertezas | Lacunas agrupadas, limites da fonte, ajustes de leitura, premissas (fuso, ambiente = arquivo), possível corte em 10.000 linhas; seção "O que esta análise não consegue dizer"; aviso de confiança baixa junto do score e aviso de empresas/ambientes possivelmente diferentes na página 1 (`AvisosTests`; dados reais: SIEM-1 × SIEM-2) | ✅ |
| RG4 | Justificativa legível de severidade | 29.564/29.564 eventos reais com justificativa em português (≤ 200 caracteres, teste), inclusive "por que não é Crítica"; correlações com "por que importa" | ✅ |
| RG5 | Português simples, jargão explicado | Página 1 sem códigos técnicos (teste `Primeira_pagina_responde_as_tres_perguntas_sem_codigos_tecnicos`), cada risco com "o que é / por que importa / como resolver", glossário com 14 termos, plurais corretos. Revisão de linguagem pela equipe ainda pendente | ⚠️ |
| RG6 | Exportável (PDF/HTML) | HTML e PDF pela tela; PDF dos exemplos conferido visualmente | ✅ |
| RG7 | Só dados sintéticos fornecidos | Plataforma processa só os 3 arquivos fornecidos + fictícios nossos; análise da IA feita só com contagens | ✅ |
| S1 | Sem credenciais no relatório | `FiltroCredenciais` mascara todo texto do relatório; V12 relê o relatório pronto e o retém se sobrar algo; teste com "senha=" fictícia não aparece no HTML (`Credencial_nos_dados_nunca_chega_ao_relatorio`); varredura do código a cada commit | ✅ |
| S2 | LGPD, minimizar dados pessoais | Ver RS5 | ⚠️ |
| S3 | Sem detalhe explorável desnecessário | Links de referência não são exibidos no relatório; descrições técnicas longas ficam fora (só "CVE no pacote X") | ⚠️ |
| S4 | Classificação de confidencialidade | Selo na capa e no rodapé (teste + PDF) | ✅ |

## Escopo (manual 4.3)

| Item | Status |
|---|---|
| Obrigatório: ≥ 3 fontes com regra e explicação | ⚠️ (3 arquivos, 2 ferramentas; aguardando mentor) |
| Obrigatório: correlação com NIST, CIS, ISO, MITRE ATT&CK | ⚠️ (NIST/CIS/ISO ✅ em toda regra e tipo; MITRE aguarda catálogo de regras) |
| Obrigatório: padronização legível com severidade | ✅ |
| Obrigatório: score de risco geral | ✅ |
| Obrigatório: relatório executivo gerado pela plataforma | ✅ (gerado sozinho, com resumo executivo, score, plano e anexos; validação na Fase 4) |
| Obrigatório: RS4 a RS6 | ⚠️ |
| Desejável: trilha de evidências navegável | ⚠️ (ids e linhas no relatório; navegação na Fase 6) |
| Desejável: recomendações automáticas | ✅ (plano gerado a partir dos dados; impacto calculado) |
| Desejável: priorização por esforço e impacto | ✅ (matriz impacto × esforço, prazo por severidade; `Matriz_impacto_por_esforco`, `Plano_calcula_quanto_cada_acao_elimina`) |
| Desejável: remediação automatizada e chamados | ❌ |
| Desejável: transparência, rastreabilidade, operação autônoma | ⚠️ (regras e justificativas abertas; operação autônoma na Fase 5) |

## Checklist final (manual 10)

| Item | Status |
|---|---|
| Funciona de ponta a ponta | ⚠️ (3 arquivos reais → relatório com score e plano; faltam login, validação, automação) |
| Operação automatizada funciona | ❌ |
| Relatório gerado pela plataforma | ✅ |
| Resumo responde "como estamos / mais grave / o que fazer" | ✅ (página 1; teste + PDF conferido) |
| Score com justificativa e fatores | ✅ (seção 2: cada fator com pergunta, valor, peso, pontos e explicação) |
| Correlações por evidências | ⚠️ (dentro das vulnerabilidades e dos alertas; entre fontes, sem chave comum) |
| Validações de consistência executadas | ✅ (12 verificações a cada lote; selo no relatório) |
| Sem credenciais, dados pessoais indevidos ou dados reais | ⚠️ (sem varredura ainda) |
| Plano de remediação | ✅ (ações com impacto calculado, esforço, prazo e score previsto) |
| Regras de segurança e conduta respeitadas | ⚠️ (fora do OneDrive, dados fora do git, IA só viu cabeçalhos e contagens) |
