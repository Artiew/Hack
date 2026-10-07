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
