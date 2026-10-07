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
