# Matriz de requisitos

Regra: ✅ só para o que foi **executado e provado**. ⚠️ parcial · ❌ não feito · ❓ depende de informação que ainda não temos.
Atualizado ao fim da **Fase 1** (esqueleto ponta a ponta).

## Requisitos funcionais (manual 4.1)

| Req. | O que o manual exige | Evidência (arquivo/teste/tela) | Status |
|---|---|---|---|
| RF1 | Ler ≥ 3 fontes diferentes | Lote aceita vários CSVs e escolhe o adaptador pelo cabeçalho (`ProcessadorLote`, `IAdaptadorFonte`). Só existe a fonte **fictícia** de exemplo; faltam os cabeçalhos das 3 fontes reais | ⚠️ |
| RF2 | Converter para modelo comum | `EventoSeguranca`; datas → UTC, ativos normalizados, usuário anonimizado (`PadronizacaoTests`) — só para a fonte exemplo | ⚠️ |
| RF3 | Correlacionar com regras claras e reproduzíveis | — (Fase 2) | ❌ |
| RF4 | Crítica/Alta/Média/Baixa com justificativa | Níveis e conversão da fonte (`ConversorSeveridade`, teste `Converte_textos_de_severidade_das_fontes`); **sem justificativa e sem contexto** ainda | ⚠️ |
| RF5 | Score 0–100 com fatores | — (Fase 3) | ❌ |
| RF6 | Relatório gerado automaticamente | Upload → relatório HTML + PDF sem intervenção (teste ponta a ponta via `curl`, Fase 1); conteúdo ainda parcial | ⚠️ |
| RF7 | Sinalizar lacunas | Campo ausente/inválido, linha mal formada, arquivo recusado e "lote sem dados ≠ ambiente limpo" viram lacunas (`PadronizacaoTests`, `ProcessadorLoteTests`). Falta: fonte esperada ausente | ⚠️ |
| RF8 | Validar consistência antes de emitir | Só existe teste unitário de soma (`Soma_por_severidade...`); a plataforma ainda não valida na emissão (Fase 4) | ❌ |

## Requisitos de segurança (manual 4.2)

| Req. | O que o manual exige | Evidência | Status |
|---|---|---|---|
| RS1 | Autenticação | — (Fase 4) | ❌ |
| RS2 | OWASP Top 10 | Provado: CSRF (POST sem token → 400), escape de HTML (`Texto_malicioso_nos_dados_e_escapado` + PDF), path traversal (→ 404, `Id_invalido_e_recusado`, `Nome_do_arquivo_perde_qualquer_caminho`), validação de upload (extensão, tamanho, binário). Falta: cabeçalhos/CSP no site, login, limites de tentativa | ⚠️ |
| RS3 | Proteger dados em trânsito e em repouso | HTTPS local em `https://localhost:5001` funcionando, mas o certificado do .NET 10 **ainda não é confiável** no Windows. Em repouso: `var/` não cifrado | ⚠️ |
| RS4 | Registrar atividades sem dados sensíveis | Log do lote só com números (`IndexModel`); falta auditoria de acesso/alterações | ⚠️ |
| RS5 | Minimização / LGPD | Usuário anonimizado na entrada (HMAC-SHA256, teste `Usuario_e_anonimizado...`); CSV bruto não é guardado; nome original não aparece no relatório (teste + `grep` no HTML) | ⚠️ |
| RS6 | Dependências confiáveis e atualizadas | Produção: **0 pacotes NuGet** (só .NET 10.0.401). Testes: xunit.v3 4.0.1, Test.Sdk 18.10.1, runner 4.0.0. `dotnet list package --vulnerable --include-transitive` = nenhum; `--outdated` = nenhum (07/10/2026) | ✅ |

## Regras do relatório (manual 7.1) e da saída (manual 8)

| Req. | O que o manual exige | Evidência | Status |
|---|---|---|---|
| RG1 | Gerado pela plataforma | `CicloAnalise` gera HTML e PDF a partir do lote, sem edição manual (ponta a ponta) | ✅ |
| RG2 | Todo número rastreável à fonte | Cada evento mostra `arquivo:linha` + SHA-256 do arquivo; números do resumo ainda não são clicáveis | ⚠️ |
| RG3 | Declarar limites e incertezas | Seção "Lacunas e limites" no relatório | ⚠️ |
| RG4 | Justificativa legível de severidade | — (Fase 2/3) | ❌ |
| RG5 | Português simples, jargão explicado | Relatório em português; SHA-256 e anonimização explicados; ainda falta glossário | ⚠️ |
| RG6 | Exportável (PDF/HTML) | HTML e PDF baixados pela tela (`/lotes/{id}/relatorio.html` e `.pdf`); PDF conferido visualmente | ✅ |
| RG7 | Só dados sintéticos fornecidos | Até agora só dados **fictícios criados por nós** (`exemplos/`, testes); os dados do desafio nunca foram lidos | ❓ |
| S1 | Sem credenciais no relatório | Nenhuma credencial no código; varredura automática ainda não feita | ⚠️ |
| S2 | LGPD, minimizar dados pessoais | Ver RS5 | ⚠️ |
| S3 | Sem detalhe explorável desnecessário | Ainda não tratado | ❓ |
| S4 | Classificação de confidencialidade | Selo na capa e no rodapé (teste `Tem_selo_de_confidencialidade` + PDF) | ✅ |

## Escopo (manual 4.3)

| Item | Status |
|---|---|
| Obrigatório: ≥ 3 fontes com regra e explicação | ⚠️ (só a fonte fictícia) |
| Obrigatório: correlação com NIST, CIS, ISO, MITRE ATT&CK | ❌ |
| Obrigatório: padronização legível com severidade | ⚠️ |
| Obrigatório: score de risco geral | ❌ |
| Obrigatório: relatório executivo gerado pela plataforma | ⚠️ (gera; conteúdo parcial) |
| Obrigatório: RS4 a RS6 | ⚠️ |
| Desejável: trilha de evidências navegável | ❌ |
| Desejável: recomendações automáticas | ❌ |
| Desejável: priorização por esforço e impacto | ❌ |
| Desejável: remediação automatizada e chamados | ❌ |
| Desejável: transparência, rastreabilidade, operação autônoma | ❌ |

## Checklist final (manual 10)

| Item | Status |
|---|---|
| Funciona de ponta a ponta | ⚠️ (esqueleto: CSV → relatório funciona) |
| Operação automatizada funciona | ❌ |
| Relatório gerado pela plataforma | ✅ |
| Resumo responde "como estamos / mais grave / o que fazer" | ❌ |
| Score com justificativa e fatores | ❌ |
| Correlações por evidências | ❌ |
| Validações de consistência executadas | ❌ |
| Sem credenciais, dados pessoais indevidos ou dados reais | ⚠️ (sem varredura ainda) |
| Plano de remediação | ❌ |
| Regras de segurança e conduta respeitadas | ⚠️ (projeto fora do OneDrive, sem dados do desafio lidos, nada publicado) |
