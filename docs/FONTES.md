# Fontes de dados do desafio

Tudo aqui foi descoberto **sem ler o conteúdo** dos arquivos: só cabeçalhos (enviados pelo Vitor), contagens e
"moldes" de formato (dígito → 9, letra → A). Os arquivos ficam em `dados/` (ignorada pelo git). Análise de 07/10/2026.

## Arquivos

| Arquivo | Fonte | Linhas | Observações |
|---|---|---|---|
| `Eventos SIEM-1(CVE critica).csv` | Vulnerabilidades | 9.564 | 178 máquinas |
| `Eventos SIEM-2(CVE critica).csv` | Vulnerabilidades (mesmo cabeçalho) | 10.000 | 196 máquinas; **nenhuma** em comum com o SIEM-1 |
| `Eventos SIEM-3(Aurora fortigate).csv` | Alertas do SIEM vindos de 1 firewall | 10.000 | 1 `agent.name`, 1 `data.devname`, 21 regras distintas |

⚠️ Dois arquivos têm **exatamente 10.000 linhas**: pode ser o limite de exportação da ferramenta (dados cortados). Isso precisa ser confirmado com um mentor e, até lá, declarado como limite no relatório.

## Vulnerabilidades (SIEM-1 e SIEM-2)
Colunas: `agent.name, host.os.name, package.name, package.version, vulnerability.id, vulnerability.description, vulnerability.severity, vulnerability.score.base, vulnerability.reference`
- CSV normal, separado por vírgula.
- `vulnerability.id`: 100% no formato CVE; 987 CVEs distintas; 374 máquinas nos dois arquivos.
- `vulnerability.severity`: **um único valor em todas as linhas** (o nome do arquivo sugere "crítica"). É o caso "fonte que rotula tudo como crítico" do PROMPT: a severidade da plataforma tem que vir do contexto (CVSS, quantidade de máquinas, criticidade do serviço).
- `vulnerability.score.base` (CVSS): moldes `9.9` (18.999), `99` (513, provavelmente "10") e `9` (52). Separador decimal: ponto.
- Sem data, sem usuário, sem exposição à internet.

## Alertas (SIEM-3)
Cabeçalho: `timestamp, agent.name, rule.description, data.devname, rule.level, rule.id`
- ⚠️ **Cada linha inteira vem entre aspas**: precisa "desembrulhar" antes de separar as colunas.
- ⚠️ **A data tem vírgula sem aspas**: molde `Aaa 9,9999 @ 99:99:99.999` (mês em inglês, ex.: `Oct 7,2026 @ 08:49:03.123`). Toda linha de dados tem **7 campos** para um cabeçalho de 6; os 2 primeiros formam a data.
- Sem essas duas correções a leitura **desalinha as colunas sem dar erro**.
- `rule.level`: 0–15 em 100% das linhas. `rule.id`: numérico, 21 distintos.
- Nenhuma descrição cita CVE.
- Data sem fuso: premissa a declarar.

## Ligação entre as fontes (correlação)
- **Nenhuma máquina em comum** entre alertas e vulnerabilidades (nem ignorando maiúsculas/domínio).
- **Nenhuma CVE citada nos alertas.**
- Conclusão: **não há chave direta por ativo**. A correlação entre fontes precisa ser por **técnica de ataque (MITRE ATT&CK)** e por **ambiente**, declarando isso no relatório. Dentro das vulnerabilidades há correlações fortes (mesma CVE ou pacote em muitas máquinas).

## Codificação
- Os dois arquivos de vulnerabilidade **não estão em UTF-8**: são Windows-1252 (101 bytes na faixa em que difere do Latin-1). A plataforma lê como Windows-1252 e declara isso como lacuna (P-007).

## Resultado da leitura pela plataforma (Fase 2, só contagens)
| | SIEM-1 | SIEM-2 | SIEM-3 |
|---|---|---|---|
| Lidos / rejeitados | 9.564 / 0 | 10.000 / 0 | 10.000 / 0 (todas as datas remontadas) |
| Severidade da plataforma | 100% Crítica (CVSS 9–10) | 100% Crítica (CVSS 9–10) | 1.570 Baixa (nível 5) · 8.430 Média (níveis 7–10) |

- **Todas as vulnerabilidades têm CVSS ≥ 9.** A nota não separa nada: a priorização entre elas precisa de contexto (espalhamento, acúmulo por máquina, serviço de negócio) → Fase 3.
- Correlações: 212 CVEs em ≥ 10 máquinas · 55 softwares com ≥ 5 falhas · 277 máquinas com ≥ 5 críticas · 4 picos de alerta · 0 máquinas em comum entre alertas e vulnerabilidades.
- ⚠️ 277 de 374 máquinas acumulam ≥ 5 críticas: o limite atual quase não separa. Ajustar na priorização (Fase 3), sem esconder o número.

## Resultado da avaliação (Fase 3, só contagens)
- Máquinas por tipo (deduzido do sistema operacional): 41 servidores (32 Windows Server + 9 Linux), 333 computadores de funcionários, 1 equipamento de rede (firewall).
- **Score 74 de 100 (Alto)**, confiança média: 2 de 5 fatores medidos (55% do peso). Falhas nos programas = 92; sinais de ataque = 42.
- Plano: atualizar 3 programas elimina **67%** das falhas graves (285 máquinas, esforço baixo, 7 dias); corrigir as 10 máquinas mais vulneráveis elimina mais **20%**; score previsto após as duas ações: **39 (Moderado)**.

## Perguntas em aberto (mentor)
1. "≥ 3 fontes" significa 3 arquivos, 3 formatos ou 3 ferramentas?
2. SIEM-1 e SIEM-2 são ambientes/clientes diferentes, datas diferentes ou um arquivo partido?
3. As exportações foram cortadas em 10.000 linhas?
4. O firewall "Aurora" protege o mesmo ambiente das máquinas vulneráveis?
5. Em que fuso estão as datas dos alertas?
