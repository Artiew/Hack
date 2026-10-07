# Plataforma de risco — Hackathon Piersec 2026

Protótipo que recebe CSVs de ferramentas de segurança, padroniza os dados, classifica a severidade com justificativa,
correlaciona eventos, calcula um score de risco de 0 a 100, monta um plano de ação e gera sozinho um relatório
executivo (HTML e PDF) cuja primeira página é escrita para quem não é da área.
**Estado atual: Fase 3** (score, priorização, plano de ação, relatório para leigo). O que está provado fica em
[`docs/REQUISITOS.md`](docs/REQUISITOS.md); o que sabemos dos dados, em [`docs/FONTES.md`](docs/FONTES.md).

> Uso restrito ao ambiente do hackathon. Não publique este repositório: os dados do desafio não podem sair da máquina.

## Como rodar

Requisitos: Windows, [SDK do .NET 10](https://dot.net) e Microsoft Edge ou Google Chrome (usado para gerar o PDF).

```powershell
.\rodar.cmd           # compila, roda os testes, sobe o site e abre o navegador
.\rodar.cmd -DoZero   # o mesmo, mas apagando antes os lotes, relatórios e a chave (var\)
```

O site fica em **https://localhost:5001**. Se algum teste falhar, o site não sobe.

- **Dados do desafio:** coloque os CSVs em `dados/` (fora do git) e envie os três juntos pela tela.
- **Sem os dados do desafio:** envie os arquivos de `exemplos/`. São **fictícios, criados por nós**, no mesmo formato das
  fontes reais (inclusive linhas entre aspas e data com vírgula) e com erros de propósito (nota inválida, máquina em branco,
  texto malicioso, coluna a mais, arquivo de fonte desconhecida).

## Como está organizado

```
src/Piersec.Core/        regras de negócio, sem tela
  Coleta/                leitor de CSV + um adaptador por fonte (IAdaptadorFonte)
  Padronizacao/          datas → UTC, nomes de ativos, anonimização
  Analise/               regras (JSON), severidade, correlação, tipo de máquina, score, plano de ação
  Pipeline/              lote: coleta → padronização → severidade → correlação; ciclo completo
  Armazenamento/         lotes salvos em var/lotes/{id}/
  Relatorio/             HTML do relatório e conversão para PDF pelo navegador
src/Piersec.Web/         telas (ASP.NET Core Razor Pages)
tests/Piersec.Tests/     testes automatizados (xUnit v3), sempre com dados fictícios
regras/                  severidade, correlacao, frameworks, ativos, score, plano (.json; editáveis; validados ao subir)
exemplos/                CSVs fictícios para demonstração
dados/                   CSVs do desafio (fora do git)
var/                     dados gerados (fora do git)
```

- **Nova fonte:** criar uma classe que implementa `IAdaptadorFonte` e registrá-la no `Program.cs`.
- **Ajustar uma regra:** editar `regras/*.json`. Se houver erro (tipo inexistente, parâmetro faltando, `{campo}` errado),
  a plataforma não sobe e diz o que corrigir.

## Variáveis de ambiente (opcionais)

| Variável | Para quê |
|---|---|
| `PIERSEC_CHAVE_ANONIMIZACAO` | Chave (base64, ≥ 32 bytes) para anonimizar usuários. Sem ela, a chave é criada em `var/segredos/`. |
| `PIERSEC_NAVEGADOR` | Caminho do Edge/Chrome, se não estiver no lugar padrão. |
