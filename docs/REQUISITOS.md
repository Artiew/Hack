# Matriz de requisitos

Regra: ✅ só para o que foi **executado e provado**. ⚠️ parcial · ❌ não feito · ❓ depende de informação que ainda não temos.
Atualizado em **09/10/2026**: resumo da diretoria, terceira fonte e correlação executados com exemplos fictícios. As seções antigas abaixo preservam o histórico; Números dos dados reais vêm de contagens (`docs/FONTES.md`).

## Requisitos funcionais (manual 4.1)

| Req. | O que o manual exige | Evidência (arquivo/teste/tela) | Status |
|---|---|---|---|
| RF1 | Ler ≥ 3 fontes diferentes | Três adaptadores de produção: scanner, SIEM e inventário de exposição. `ExposicaoInternetTests` e `CorrelacaoTresFontesTests`; envio fictício de três fontes provado no navegador. A terceira fonte oficial ainda depende de dados/autorização da organização | ✅ suporte / ❓ dados oficiais |
| RF2 | Converter para modelo comum | `EventoSeguranca` para as fontes reais: datas → UTC (10.000/10.000 alertas com data), ativos normalizados, CVSS com ponto ou vírgula, Windows-1252 tratado (P-007), linhas embrulhadas e data partida tratadas (P-005). Testes `FontesReaisTests`, `PadronizacaoTests`, `LeitorCsvTests` | ✅ |
| RF3 | Correlacionar fontes por regras claras | Sete regras JSON, incluindo exposição + falha e exposição + falha + alerta direcionado. Vínculo explícito por ativo/ambiente, alvo e janela temporal; três evidências navegáveis provadas. MITRE como hipótese, não ataque confirmado. Dados antigos sem essas chaves não ganham relações inventadas | ✅ exemplos fictícios / ⚠️ dados sem vínculo |
| RF4 | Crítica/Alta/Média/Baixa com justificativa | `ClassificadorSeveridade` + `regras/severidade.json` (CVSS oficial, níveis do SIEM, textos). 100% dos eventos reais com justificativa, inclusive "por que não é Crítica" (`SeveridadeTests`). Falta o contexto (exposição, serviço) — Fase 3 ; **discordar da severidade**: o analista ajusta com motivo obrigatório, o que cria uma revisão do lote recalculada (o original não muda), com anexo J no relatório, V15 e auditoria (`RevisaoTests`). Ainda falta usar exposição/criticidade do serviço no cálculo da severidade | ⚠️ |
| RF5 | Score 0–100 com fatores | `CalculadoraScore` + `regras/score.json`: direção escrita ("100 = risco máximo"), 5 fatores com contribuição (soma = score, teste), fator sem dados = "não medido" (não vira zero), confiança, faixas com nome, comparação com o lote anterior e motivo (`Comparacao_com_o_lote_anterior...`), sanidade: limpo → 0 "Baixo", caótico → ≥ 80 "Crítico"; **"3 achados explicam X% do risco"** recalculando o score sem cada achado, e dizendo "o risco está espalhado" quando explicam menos de 30% (`ScoreEPlanoTests`). Dados reais: 74 (Alto), confiança média (55% do peso medido) | ✅ |
| RF6 | Relatório gerado automaticamente | Upload dos 3 arquivos reais → HTML + PDF em ~3,5 s, sem intervenção (ensaio do zero com `rodar.cmd -DoZero`). Página 1 para a diretoria (como estamos / mais grave / o que fazer / se fizer só uma coisa), seções de score, plano e limites, anexos técnicos e glossário. Falta validação antes de emitir (RF8) | ⚠️ |
| RF7 | Sinalizar lacunas | Por campo, linha, arquivo, fonte ("não informa data/usuário/exposição") e lote; aviso de possível corte em 10.000 linhas; lacunas agrupadas no relatório; **fonte esperada ausente** ("faltou o arquivo de alertas…", na página 1, na tela e nos limites); **linhas repetidas** contadas uma vez e declaradas com o número da linha (`DuplicidadeEFontesTests`) | ✅ |
| RF8 | Validar consistência antes de emitir | `ValidadorLote`: 12 verificações antes de emitir + **V13** (a operação automática respeitou as travas, conferido por outro caminho em `MotorDecisao.Conferir`) (somas, lidos × eventos, origem de todo evento, datas coerentes, fonte vazia = lacuna, correlações íntegras, contribuições = score, **score recalculado por segundo caminho**, percentuais do plano, máquinas por tipo, credenciais mascaradas, relatório final relido). Selo "N verificações, N aprovadas"; se reprovar, relatório marcado "NÃO VALIDADO". Dados reais: 12/12; arquivo corrompido de propósito (`exemplos/alertas-corrompido-ficticio.csv`): V04 reprova (`ValidacaoTests`) | ✅ |

## Requisitos de segurança (manual 4.2)

| Req. | O que o manual exige | Evidência | Status |
|---|---|---|---|
| RS1 | Autenticação | Nada abre sem login (páginas e relatórios → 302 para /Entrar). `admin` criado na 1ª execução com senha de `ADMIN_SENHA_INICIAL` (exatamente 8) ou sorteada e mostrada uma vez; troca obrigatória (toda página leva a /TrocarSenha); PBKDF2-SHA256 600 mil iterações; bloqueio após 5 erros por 15 min; erro idêntico para usuário inexistente/senha errada/bloqueado; sessão de 30 min; papéis admin/analista/executivo (executivo não envia nem vê tabelas técnicas; /Usuarios → /Negado); **admin exclui usuários** com confirmação (não exclui a si mesmo nem o último admin) e a sessão aberta do excluído cai no próximo clique (`OnValidatePrincipal`; ponta a ponta). **Verificação em duas etapas (2FA, TOTP RFC 6238, 100% local)**: obrigatória para admin (só abre o cadastro até ativar), opcional para analista/executivo; QR code gerado pelo próprio projeto (0 pacotes); chave cifrada em repouso; mesmo código não vale 2 vezes; ±30 s de tolerância; erros de código contam no mesmo bloqueio da senha e a senha certa não zera as tentativas; sessão "só senha" sem papel, 5 min, só abre /SegundoFator; 8 códigos de recuperação de uso único (só o SHA-256 é gravado); "Esqueci a senha do admin" também desliga o 2FA; tudo na auditoria. `SegurancaTests` (24) + `DoisFatoresTests` (20, com vetores da RFC 6238 e da norma do QR code) + ponta a ponta com curl (código calculado fora do site, como o celular) | ✅ |
| RS2 | OWASP Top 10 | CSRF (POST sem token → 400; sair só por POST), escape de HTML (testes + PDF), path traversal (→ 404), validação de upload, CSP restrita no site e no relatório, `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cache-Control: no-store`, sem cabeçalho `Server`, cookie `__Host-` Secure/HttpOnly/SameSite=Strict, limite de 10 logins/min por endereço (429), erro sem detalhe técnico em produção | ✅ |
| RS3 | Proteger dados em trânsito e em repouso | Trânsito: HTTPS local com certificado confiável, cookie só HTTPS, HSTS em produção. Repouso: lote, resumo, relatório HTML e PDF e chave de anonimização gravados **cifrados** (`*.cifrado`, Data Protection AES-256 autenticado; chaves em `var/chaves` protegidas pelo DPAPI do Windows); arquivo alterado no disco é recusado; PDF gerado em `var/tmp` e apagado em seguida. `CifragemTests` + conferência dos arquivos reais (4/4 cifrados, 0 temporários). Limite: `usuarios.json` (só hashes) e `auditoria.jsonl` (sem dados sensíveis) ficam abertos | ✅ |
| RS4 | Registrar atividades sem dados sensíveis | `var/auditoria.jsonl` (só acrescenta): entrada, falha, bloqueio, saída, troca de senha, usuário criado, lote processado, relatório aberto, envio negado; login inexistente vira "(desconhecido)". Página /Auditoria (admin). Teste: nenhuma senha usada aparece no log nem em `usuarios.json` | ✅ |
| RS5 | Minimização / LGPD | Anonimização de usuário pronta (as fontes reais não trazem usuário); CSV bruto não é guardado | ⚠️ |
| RS6 | Dependências confiáveis e atualizadas | Produção: **0 pacotes NuGet** (só .NET 10.0.401). Testes: xunit.v3 4.0.1, Test.Sdk 18.10.1, runner 4.0.0. `--vulnerable --include-transitive` = nenhum; `--outdated` = nenhum (07/10/2026, após a Fase 2) | ✅ |

## Regras do relatório (manual 7.1) e da saída (manual 8)

| Req. | O que o manual exige | Evidência | Status |
|---|---|---|---|
| RG1 | Gerado pela plataforma | `CicloAnalise` gera HTML e PDF a partir do lote; metodologia lida dos mesmos JSON usados na análise | ✅ |
| RG2 | Todo número rastreável à fonte | No site (admin/analista): todo número da tela do lote é clicável → lista filtrada dos eventos que o formam → detalhe do evento com **arquivo, linha (cabeçalho = 1) e SHA-256** do arquivo, justificativa e correlações (`ConsultaTests`, `Linha_de_origem_conta_o_cabecalho_como_linha_1`, ponta a ponta). No relatório (arquivo estático, sem link para o site): ids de evento e `arquivo:linha` nos anexos. A linha bruta do CSV não é guardada (minimização), e isso é explicado na tela | ✅ |
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
| Obrigatório: RS4 a RS6 | ⚠️ (RS4 e RS6 ✅; RS5: anonimização pronta, minimização a revisar) |
| Desejável: trilha de evidências navegável | ✅ (número → eventos → arquivo e linha de origem; filtros por severidade, serviço, fonte, ambiente, máquina, regra, correlação e ação do plano) |
| Desejável: recomendações automáticas | ✅ (plano gerado a partir dos dados; impacto calculado) |
| Desejável: priorização por esforço e impacto | ✅ (matriz impacto × esforço, prazo por severidade; `Matriz_impacto_por_esforco`, `Plano_calcula_quanto_cada_acao_elimina`) |
| Desejável: remediação automatizada e chamados | ✅ (chamados e plano de correção SIMULADOS, artefato `/automacao/CH-NNNN.txt`; nada é enviado. `OperacaoTests`, ponta a ponta com os exemplos) |
| Desejável: transparência, rastreabilidade, operação autônoma | ✅ (regras e playbooks em JSON; cada decisão com regra, evidência, confiança e motivo no log; desfazer; auditoria) |

## Checklist final (manual 10)

| Item | Status |
|---|---|
| Funciona de ponta a ponta | ⚠️ (login → envio → relatório validado e cifrado → operação automática → aprovar/rejeitar/desfazer: provado com os exemplos fictícios; falta rodar a operação com os 3 arquivos reais) |
| Operação automatizada funciona | ✅ (ver tabela "Operação automatizada" abaixo; provado com dados fictícios e teste de ponta a ponta no site) |
| Relatório gerado pela plataforma | ✅ |
| Resumo responde "como estamos / mais grave / o que fazer" | ✅ (página 1; teste + PDF conferido) |
| Score com justificativa e fatores | ✅ (seção 2: cada fator com pergunta, valor, peso, pontos e explicação) |
| Correlações por evidências | ⚠️ (dentro das vulnerabilidades e dos alertas; entre fontes, sem chave comum) |
| Validações de consistência executadas | ✅ (12 verificações a cada lote; selo no relatório) |
| Sem credenciais, dados pessoais indevidos ou dados reais | ⚠️ (sem varredura ainda) |
| Plano de remediação | ✅ (ações com impacto calculado, esforço, prazo e score previsto) |
| Regras de segurança e conduta respeitadas | ⚠️ (fora do OneDrive, dados fora do git, IA só viu cabeçalhos e contagens) |

## Operação automatizada (PROMPT 5.2 · 30% da nota)

| Item | Evidência | Status |
|---|---|---|
| Níveis de autonomia 0–3 | `regras/automacao.json` (`nivelAutonomia`, hoje 2); testes `Nivel_0_so_sugere`, `Nivel_1_pede_aprovacao_para_tudo`, `Nivel_2_...`, `Nivel_3_...`; tela Automação mostra o que cada nível faria com o último lote | ✅ |
| Dry-run (ensaio) | `modoEnsaio`: calcula tudo e não grava nada (`Modo_ensaio_mostra_o_que_faria_e_nao_grava_nenhum_chamado`) | ✅ |
| Nada irreversível / alto impacto sem pessoa | trava em `MotorDecisao` (vale em qualquer nível); isolar máquina sempre aguarda aprovação | ✅ |
| Ativo crítico sempre com pessoa | ação que altera servidor ou equipamento de rede aguarda aprovação mesmo no nível 3 (`Nivel_3_corrige_estacao_sozinho_mas_servidor_...`) | ✅ |
| Limite de ações por período | `limiteAcoesPorDia` (20), dia de Brasília (`Limite_por_dia_segura_o_excesso_e_volta_a_valer_no_dia_seguinte`) | ✅ |
| Lote não validado não age sozinho | `Lote_que_nao_passou_na_validacao_nao_dispara_nada_sozinho` | ✅ |
| Log de decisão (regra + evidência + confiança + desfazer) | histórico de cada chamado, tela Automação, artefato .txt, seção 4 do relatório; aprovar/rejeitar/desfazer na auditoria | ✅ |
| Chamados e remediação simulados (artefato, não envia) | `/automacao/CH-NNNN.txt` ("SIMULADO · nenhum sistema externo foi acionado") | ✅ |
| Idempotência | chave estável por playbook + problema (`Mesmo_lote_duas_vezes_nao_duplica_chamado`; ponta a ponta: 2º envio = 11 "já existiam", 0 novos) | ✅ |
| Verificação contínua | fecha sozinho quando some, reabre o MESMO chamado quando volta; sem dados do tipo, não fecha (`Chamado_fecha_sozinho_...`, `Lote_sem_dados_de_vulnerabilidade_nao_fecha_...`) | ✅ |
| Respeita a decisão humana | rejeitado/desfeito não é refeito sozinho (`Pessoa_aprova_rejeita_com_motivo_e_desfaz`) | ✅ |
| Consistência da automação | V13 confere as travas por outro caminho; adulterar um item reprova (`V13_aprova_...`) | ✅ |
| Com os dados reais | ainda não rodado | ❌ |

## Visões executiva e técnica (PROMPT 5.1 · Fase 6)

| Item | Evidência | Status |
|---|---|---|
| Mapa máquina × serviço de negócio com premissa declarada | `regras/servicos.json` (nome → tipo, primeira regra vence), premissa na seção 2 do relatório e na tela; V14 confere (`ServicosTests`) | ✅ |
| Impacto financeiro em faixas, com premissas visíveis | Faixa por serviço com premissa; prejuízo potencial = soma dos serviços em risco ("entre R$ X e R$ Y"), na página 1, seção 2 e tela; nunca número exato; sem serviço em risco, não inventa valor | ✅ |
| "3 achados explicam X% do risco" | Recalculado sem cada problema; linguagem de diretoria; "risco espalhado" quando explica pouco (`ScoreEPlanoTests`) | ✅ |
| Visão técnica filtrável | `/Lote/{id}/Eventos` (fonte, severidade, tipo, serviço, ambiente, máquina, regra, texto, correlação, ação do plano, arquivo), paginada; só admin/analista (executivo → /Negado, testado) | ✅ |
| Navegação executivo → técnico | Serviços, riscos, ações e contagens da tela do lote levam aos eventos; cada evento mostra o serviço afetado | ✅ |
| Filtro por ambiente sem trocar o consolidado | Tabela "Por ambiente" e filtro na visão técnica (ambiente = arquivo, premissa) | ✅ |
| Discordar da severidade com motivo e recálculo | Ver RF4 (`RevisaoTests`, ponta a ponta no site) | ✅ |
| Relatório do executivo só com o que ele usa | Versão da diretoria (HTML e PDF, cifrados) gerada a cada lote: resumo, 2. serviços e prejuízo, 3. score, 4. plano completo e glossário; sem automação, limites e anexos, e sem citar anexo que ele não recebe. Executivo recebe sempre essa versão (mesmo pedindo a completa); admin/analista recebem a completa e têm o botão "Versão da diretoria". Avisos de confiança, ambientes, fonte ausente e "não validado" continuam na página 1. `Versao_da_diretoria_tem_so_...`, `Ciclo_completo_...`; ponta a ponta com admin e executivo (PDF do executivo = PDF da diretoria) | ✅ |
| Linha do tempo por ativo / cadeia usuário→máquina→servidor | Os dados não trazem usuário nem ligação entre máquinas; a lista por máquina (filtro) ordena por gravidade, não por tempo | ❌ |
| MITRE ATT&CK como tabela de dados | Aguarda o catálogo de regras do mentor | ❓ |

## Interface em cards (PROMPT 5.1 tela 2 e 5.3 · Fase 7)

| Item | Evidência | Status |
|---|---|---|
| Barra lateral (busca + itens com ícone, ativo em âmbar) e barra superior | `_Layout.cshtml`, ícones SVG próprios (nada da internet); prints claro/escuro | ✅ |
| Chips de filtro em pílula com contagem | Achados na tela do lote (Todos · Crítica · Alta · Média · Baixa) e contadores da visão técnica | ✅ |
| Cards horizontais (ícone, meta `ID · ativo · fonte`, título, tags de frameworks, "Ver mais"); selecionado com borda de destaque e sombra | Riscos e achados na tela do lote (print conferido) | ✅ |
| Painel executivo: risco, prioridade e ações com prazo, decisões pendentes | Indicadores e grade de contexto/serviços/plano na tela do lote; prévias clara/escura/celular conferidas com dados fictícios | ✅ |
| Severidade sempre cor + ícone + texto, mesmos nomes em tudo | `Visual.Selo`; ícone muda de forma (triângulo, círculo, quadrado, círculo vazado) | ✅ |
| Número grande com contexto | "50 de 100 · pior/melhor/igual à análise anterior" ou "primeira análise" | ✅ |
| Tema claro/escuro, foco visível, teclado, responsivo | Seletor no menu da conta no rodapé esquerdo, preferência local persistente; sem escolha acompanha o sistema. Teste de navegador e prévias claras/escuras/390 px com exemplos fictícios | ✅ |
| Envio: arrastar e soltar + botão, vários arquivos | `wwwroot/js/envio.js` (arquivo próprio, CSP `script-src 'self'`); sem JavaScript o formulário normal funciona | ✅ |
| Progresso por etapa | Envio em segundo plano (`ServicoEnvios`), 6 etapas; só quem enviou vê; no máximo 2 simultâneos (ponta a ponta) | ✅ |
| Estados vazio / carregando / erro | Sem lote → card explicativo; carregando → etapas; erro → mensagem e botão liberado; fonte ausente → aviso | ✅ |
| Contraste AA medido por ferramenta | Paleta principal calculada por luminância relativa WCAG: mínimo 5,12:1 claro / 7,49:1 escuro para textos sobre fundos. Auditoria de todos os componentes/estados ainda pendente | ⚠️ |

## Visão executiva — atualização de 08/10/2026

| Item | Evidência | Status |
|---|---|---|
| Panorama, prioridade, risco, impacto em faixa, tendência e confiança | `_PainelExecutivo.cshtml`; usa os resultados existentes e declara ausências/premissas; build e prévias com dados fictícios | ✅ |
| Simulador de correções sem executar ações | `SimuladorRemediacaoTests` (5 testes), HTTP e navegador com/sem JS; hash dos arquivos de análises/operação permanece igual após simular | ✅ |
| Histórico navegável e cobertura das fontes | Dois lotes fictícios no roteiro `tests/verificar-executivo.cjs`; fontes ausentes continuam explicitadas, sem transformar falta de dados em zero risco | ✅ |
| Modo apresentação por teclado | Botão Apresentar, setas trocam de seção e Esc sai; roteiro de navegador aprovado | ✅ |
| Restrição técnica preservada | Executivo acessa o painel/simulador; não recebe tabelas/links de eventos, rota técnica continua em /Negado; roteiro aprovado | ✅ |

Suíte após a atualização: 283 aprovados, 0 falhas, 0 ignorados. O relatório exportado não recebeu este novo layout. O mapa MITRE continua dependendo do catálogo do mentor; não foram inventadas métricas financeiras ou de conformidade.
### Ajustes de leitura executiva e navegação — 08/10/2026

| Item | Evidência | Status |
|---|---|---|
| Uma seção por vez, Panorama na abertura | Navegador verifica exatamente um painel visível; títulos e setas alternam serviços/plano/fontes/técnico; destinos GET sem JavaScript também aprovados | ✅ |
| Prejuízo, gasto, causa e solução no primeiro olhar | Quatro cartões do Panorama; teste fictício verifica valor de prejuízo existente, gasto "A estimar", fator predominante e ação priorizada; caso sem dados declara insuficiência | ✅ |
| Navegação preserva contexto | Seleção do simulador mantida, Voltar/Avançar, Home/End e setas aprovados; versões clara/escura e 390 px conferidas | ✅ |

Gasto monetário de remediação ainda depende de orçamento: não existe no modelo atual. Esforço não foi convertido em dinheiro. Causa indica predominância de fator na avaliação, não causa raiz comprovada de ataque.

Menu da conta (08/10/2026): botão com nome/papel no rodapé esquerdo abre Duas etapas, Tema, Trocar senha e Sair. Roteiro fictício prova destinos, destaque ativo, token de saída, logout, Enter/Esc/foco, clique fora, uso sem JavaScript e 390 px sem transbordamento.

Validação consolidada da entrega (08/10/2026): **289 aprovados, 0 falhas, 0 ignorados**; build Web Debug sem avisos/erros; roteiros fictícios de envio, 2FA e visão executiva aprovados. Cadastro pendente de 2FA ilegível pode ser recriado, com aviso, sem substituir fator já ativo. A falha de login 2FA do admin real relatada depois continua sem diagnóstico: o usuário cancelou a investigação. HTTPS confiável verificado nesta máquina e em Brave temporário; a confiança no certificado precisa ser configurada em cada computador.

### Automação para o perfil executivo — 09/10/2026

- Visão própria de acompanhamento em `_AutomacaoExecutiva.cshtml`: pendências da equipe, planos preparados, melhorias sugeridas e problemas que deixaram de aparecer nos dados. Cancelamentos separados de resoluções.
- Linguagem simples por tipo de ação, motivo e próximo passo; detalhes progressivos com link à análise. Controles e configuração operacional continuam na visão técnica. Ações seguem simuladas e o executivo não aprova/rejeita/desfaz.
- Verificado com contas/arquivos fictícios: permissões, contagens, estados, navegação por teclado e sem JavaScript; temas claro/escuro em quatro larguras sem rolagem lateral; 318 testes aprovados e build sem avisos/erros.

## Verificação sequencial — 09/10/2026

1. PDF da diretoria: 7 → 2 páginas no cenário fictício; resumo, impacto/custo e prioridades. Sem tabelas técnicas, CVEs, pacotes ou glossário. Avisos de cobertura e validação permanecem. Relatório completo preservado.
2. Terceira fonte: inventário de exposição exportado. Fator por ativo único; vazio, desconhecido, inválido ou contraditório = não medido. Três fontes válidas cobrem 75% do modelo, não 100% do ambiente. Não há varredura.
3. Correlação: exemplos próprios de scanner/SIEM/inventário com ambiente e ativo comuns; alvo explícito no alerta, separado do coletor; máximo 24 horas entre alerta e inventário. Cada vínculo abre três eventos e origens. Não prova exploração nem simultaneidade da vulnerabilidade, cuja fonte não informa data.

Evidência final: 353 testes aprovados, 0 falhas, 0 ignorados; build sem avisos/erros; QA isolado com 15 validações, evidências navegáveis, simulador, 2 temas e 3 larguras. Diretoria com 2 páginas nas três etapas. Não foram acessados CSVs/relatórios do desafio.

Premissas novas: custos.json inclui revisão de exposição (2–4 horas fixas + 0,5–1,5 hora por máquina); score.json associa exposicao ao novo tipo; correlacao.json adiciona duas regras e janela 24h, sem alterar os limites antigos; frameworks.json inclui inventário como contexto de avaliação/proteção de rede. Valores são premissas de exercício.

Reprodução e formatos: `exemplos/LEIA-ME-tres-fontes.md`. Para a banca, somente dados e fontes autorizados. Não interpretar três exemplos inventados como prova de que a organização forneceu três fontes utilizáveis.


### Atualização — glossário para a diretoria (09/10/2026)

A pedido do usuário, a versão da diretoria passa a ter uma terceira página com dez termos explicados em linguagem simples. As duas primeiras continuam dedicadas ao panorama e às decisões. O relatório completo mantém seu glossário técnico.

Verificação: 353 testes aprovados, 0 falhas e 0 ignorados; exportação fictícia em instância isolada conferida com três páginas, glossário somente na terceira e paginação correta. A terceira página foi revisada visualmente. Relatórios já gerados não são reescritos: gerar novo lote para obter a atualização.


### Estado para o commit e push — 09/10/2026

O usuário escolheu manter a exclusão da pasta `exemplos/`. A suíte executada nesse estado teve **288 aprovados, 65 falhas e 0 ignorados**, por ausência dos arquivos fictícios usados pelos testes (`FileNotFoundException`). A evidência anterior de 353/353 vale para o estado com os exemplos presentes; não representa esta entrega sem eles. Nenhum teste foi removido ou desativado. Roteiros de demonstração que citam os exemplos também não podem ser reproduzidos sem esses arquivos.
