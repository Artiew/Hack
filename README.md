# Plataforma de risco — Hackathon Piersec 2026

Protótipo que recebe CSVs de ferramentas de segurança, padroniza os dados e gera sozinho um relatório executivo (HTML e PDF).
**Estado atual: Fase 1 (esqueleto ponta a ponta).** O detalhe do que está provado fica em [`docs/REQUISITOS.md`](docs/REQUISITOS.md).

> Uso restrito ao ambiente do hackathon. Não publique este repositório: os dados do desafio não podem sair da máquina.

## Como rodar

Requisitos: Windows, [SDK do .NET 10](https://dot.net) e Microsoft Edge ou Google Chrome (usado para gerar o PDF).

```powershell
.\rodar.cmd           # compila, roda os testes, sobe o site e abre o navegador
.\rodar.cmd -DoZero   # o mesmo, mas apagando antes os lotes, relatórios e a chave (var\)
```

O site fica em **https://localhost:5001**. Se algum teste falhar, o site não sobe.

Para testar sem os dados do desafio, envie os arquivos de `exemplos/`. Eles foram **criados por nós**, são fictícios e
têm erros de propósito (data inválida, ativo em branco, severidade desconhecida, texto malicioso, coluna a mais).

## Como está organizado

```
src/Piersec.Core/        regras, sem tela
  Coleta/                leitor de CSV + um adaptador por fonte (IAdaptadorFonte)
  Padronizacao/          datas → UTC, nomes de ativos, anonimização, conversão de severidade
  Pipeline/              processamento do lote e ciclo completo (processar → salvar → relatório)
  Armazenamento/         lotes salvos em var/lotes/{id}/
  Relatorio/             HTML do relatório e conversão para PDF pelo navegador
src/Piersec.Web/         telas (ASP.NET Core Razor Pages)
tests/Piersec.Tests/     testes automatizados (xUnit v3), sempre com dados fictícios
exemplos/                CSVs fictícios para demonstração
var/                     dados gerados (fora do git)
```

Para adicionar uma fonte: criar uma classe que implementa `IAdaptadorFonte` e registrá-la no `Program.cs`.

## Variáveis de ambiente (opcionais)

| Variável | Para quê |
|---|---|
| `PIERSEC_CHAVE_ANONIMIZACAO` | Chave (base64, ≥ 32 bytes) para anonimizar usuários. Sem ela, a chave é criada em `var/segredos/`. |
| `PIERSEC_NAVEGADOR` | Caminho do Edge/Chrome, se não estiver no lugar padrão. |
