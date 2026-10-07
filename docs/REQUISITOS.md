# Matriz de requisitos

Regra: ✅ só para o que foi **executado e provado**. ⚠️ parcial · ❌ não feito · ❓ depende de informação que ainda não temos.
Atualizado ao fim da **Fase 2** (fontes reais, severidade com justificativa, correlação). Números dos dados reais vêm de contagens (`docs/FONTES.md`).

## Requisitos funcionais (manual 4.1)

| Req. | O que o manual exige | Evidência (arquivo/teste/tela) | Status |
|---|---|---|---|
| RF1 | Ler ≥ 3 fontes diferentes | 3 arquivos reais lidos de ponta a ponta: 29.564 registros, 0 rejeitados (`AdaptadorVulnerabilidades`, `AdaptadorAlertasSiem`, `FontesReaisTests`). Por estrutura são **2 ferramentas** em 3 arquivos: contagem de "fontes" aguarda o mentor | ⚠️ |
| RF2 | Converter para modelo comum | `EventoSeguranca` para as fontes reais: datas → UTC (10.000/10.000 alertas com data), ativos normalizados, CVSS com ponto ou vírgula, Windows-1252 tratado (P-007), linhas embrulhadas e data partida tratadas (P-005). Testes `FontesReaisTests`, `PadronizacaoTests`, `LeitorCsvTests` | ✅ |
| RF3 | Correlacionar com regras claras e reproduzíveis | 5 regras em `regras/correlacao.json` (validadas na inicialização), com justificativa e frameworks; mesma entrada = mesma saída (`Mesma_entrada_gera_exatamente_a_mesma_saida`); regras sem resultado mostram o motivo. Dados reais: 548 correlações. Limite: **sem chave comum entre alertas e vulnerabilidades** (0 máquinas em comum); MITRE pendente | ⚠️ |
| RF4 | Crítica/Alta/Média/Baixa com justificativa | `ClassificadorSeveridade` + `regras/severidade.json` (CVSS oficial, níveis do SIEM, textos). 100% dos eventos reais com justificativa, inclusive "por que não é Crítica" (`SeveridadeTests`). Falta o contexto (exposição, serviço) — Fase 3 | ⚠️ |
| RF5 | Score 0–100 com fatores | — (Fase 3) | ❌ |
| RF6 | Relatório gerado automaticamente | Upload dos 3 arquivos reais → HTML + PDF em ~3,5 s, sem intervenção (ensaio do zero com `rodar.cmd -DoZero`); conteúdo ainda parcial | ⚠️ |
| RF7 | Sinalizar lacunas | Por campo, linha, arquivo, fonte ("não informa data/usuário/exposição") e lote; aviso de possível corte em 10.000 linhas; lacunas agrupadas no relatório. Falta: fonte esperada ausente | ⚠️ |
| RF8 | Validar consistência antes de emitir | Só testes unitários; a plataforma ainda não valida na emissão (Fase 4) | ❌ |

## Requisitos de segurança (manual 4.2)

| Req. | O que o manual exige | Evidência | Status |
|---|---|---|---|
| RS1 | Autenticação | — (Fase 4) | ❌ |
| RS2 | OWASP Top 10 | Provado: CSRF (POST sem token → 400), escape de HTML (testes + PDF), path traversal (→ 404), validação de upload. Falta: cabeçalhos/CSP no site, login, limites de tentativa | ⚠️ |
| RS3 | Proteger dados em trânsito e em repouso | HTTPS local funcionando, mas o certificado do .NET 10 ainda não é confiável no Windows. Em repouso: `var/` não cifrado | ⚠️ |
| RS4 | Registrar atividades sem dados sensíveis | Log do lote só com números; falta auditoria de acesso/alterações | ⚠️ |
| RS5 | Minimização / LGPD | Anonimização de usuário pronta (as fontes reais não trazem usuário); CSV bruto não é guardado | ⚠️ |
| RS6 | Dependências confiáveis e atualizadas | Produção: **0 pacotes NuGet** (só .NET 10.0.401). Testes: xunit.v3 4.0.1, Test.Sdk 18.10.1, runner 4.0.0. `--vulnerable --include-transitive` = nenhum; `--outdated` = nenhum (07/10/2026, após a Fase 2) | ✅ |

## Regras do relatório (manual 7.1) e da saída (manual 8)

| Req. | O que o manual exige | Evidência | Status |
|---|---|---|---|
| RG1 | Gerado pela plataforma | `CicloAnalise` gera HTML e PDF a partir do lote; metodologia lida dos mesmos JSON usados na análise | ✅ |
| RG2 | Todo número rastreável à fonte | Evento → `arquivo:linha` + SHA-256; correlação → lista de eventos e máquinas. Números do resumo ainda não são clicáveis | ⚠️ |
| RG3 | Declarar limites e incertezas | Lacunas agrupadas, limites da fonte, ajustes de leitura, premissas (fuso, ambiente = arquivo), possível corte em 10.000 linhas | ⚠️ |
| RG4 | Justificativa legível de severidade | 29.564/29.564 eventos reais com justificativa em português (≤ 200 caracteres, teste), inclusive "por que não é Crítica"; correlações com "por que importa" | ✅ |
| RG5 | Português simples, jargão explicado | Relatório em português; CVSS, SHA-256, controles de frameworks explicados; falta glossário e linguagem de negócio | ⚠️ |
| RG6 | Exportável (PDF/HTML) | HTML e PDF pela tela; PDF dos exemplos conferido visualmente | ✅ |
| RG7 | Só dados sintéticos fornecidos | Plataforma processa só os 3 arquivos fornecidos + fictícios nossos; análise da IA feita só com contagens | ✅ |
| S1 | Sem credenciais no relatório | Nenhuma credencial no código; varredura automática ainda não feita | ⚠️ |
| S2 | LGPD, minimizar dados pessoais | Ver RS5 | ⚠️ |
| S3 | Sem detalhe explorável desnecessário | Links de referência não são exibidos no relatório; descrições técnicas longas ficam fora (só "CVE no pacote X") | ⚠️ |
| S4 | Classificação de confidencialidade | Selo na capa e no rodapé (teste + PDF) | ✅ |

## Escopo (manual 4.3)

| Item | Status |
|---|---|
| Obrigatório: ≥ 3 fontes com regra e explicação | ⚠️ (3 arquivos, 2 ferramentas; aguardando mentor) |
| Obrigatório: correlação com NIST, CIS, ISO, MITRE ATT&CK | ⚠️ (NIST/CIS/ISO ✅ em toda regra e tipo; MITRE aguarda catálogo de regras) |
| Obrigatório: padronização legível com severidade | ✅ |
| Obrigatório: score de risco geral | ❌ |
| Obrigatório: relatório executivo gerado pela plataforma | ⚠️ (gera; falta score, negócio e plano) |
| Obrigatório: RS4 a RS6 | ⚠️ |
| Desejável: trilha de evidências navegável | ⚠️ (ids e linhas no relatório; navegação na Fase 6) |
| Desejável: recomendações automáticas | ⚠️ ("atualizar pacote X resolve N falhas") |
| Desejável: priorização por esforço e impacto | ❌ |
| Desejável: remediação automatizada e chamados | ❌ |
| Desejável: transparência, rastreabilidade, operação autônoma | ⚠️ (regras e justificativas abertas; operação autônoma na Fase 5) |

## Checklist final (manual 10)

| Item | Status |
|---|---|
| Funciona de ponta a ponta | ⚠️ (3 arquivos reais → relatório; faltam fases 3–6) |
| Operação automatizada funciona | ❌ |
| Relatório gerado pela plataforma | ✅ |
| Resumo responde "como estamos / mais grave / o que fazer" | ❌ |
| Score com justificativa e fatores | ❌ |
| Correlações por evidências | ⚠️ (dentro das vulnerabilidades e dos alertas; entre fontes, sem chave comum) |
| Validações de consistência executadas | ❌ |
| Sem credenciais, dados pessoais indevidos ou dados reais | ⚠️ (sem varredura ainda) |
| Plano de remediação | ❌ |
| Regras de segurança e conduta respeitadas | ⚠️ (fora do OneDrive, dados fora do git, IA só viu cabeçalhos e contagens) |
