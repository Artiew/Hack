# Plataforma de risco — Hackathon Piersec 2026

Protótipo que recebe CSVs de ferramentas de segurança, padroniza os dados, classifica a severidade com justificativa,
correlaciona eventos, calcula um score de risco de 0 a 100, monta um plano de ação e gera sozinho um relatório
executivo (HTML e PDF) cuja primeira página é escrita para quem não é da área. Uma operação automática (simulada) abre
chamados sozinha dentro de travas e deixa o resto numa fila de aprovação humana.
**Estado atual: Fase 6** (score, plano, relatório para leigo, validação, login, operação automática simulada, serviços e prejuízo em faixas, visão técnica com rastro até a linha do CSV e "discordar da severidade"). O que está provado fica em
[`docs/REQUISITOS.md`](docs/REQUISITOS.md); o que sabemos dos dados, em [`docs/FONTES.md`](docs/FONTES.md).

> Uso restrito ao ambiente do hackathon. Não publique este repositório: os dados do desafio não podem sair da máquina.

## Como rodar

Requisitos: Windows, [SDK do .NET 10](https://dot.net) e Microsoft Edge ou Google Chrome (usado para gerar o PDF).

**Jeito mais fácil:** dois cliques em **`Abrir Plataforma.cmd`**, na pasta do projeto. Esqueceu a senha do admin?
Dois cliques em **`Esqueci a senha do admin.cmd`** (gera uma senha temporária, mostrada uma vez, com troca obrigatória).
Dica: deixe o navegador salvar a senha ao entrar; ele guarda cifrada e preenche sozinho.

Pelo terminal:
```powershell
.\rodar.cmd                        # compila, roda os testes, sobe o site e abre o navegador
.\rodar.cmd -DoZero                # o mesmo, apagando antes tudo de var\ (lotes, relatórios, usuários, auditoria, chaves)
.\rodar.cmd -RedefinirSenhaAdmin   # esqueceu a senha do admin
```

O site fica em **https://localhost:5001**. Se algum teste falhar, o site não sobe.

**Primeiro acesso:** na primeira execução (ou depois de `-DoZero`) é criado o usuário `admin`. A senha inicial, de
8 caracteres, aparece **uma única vez** no terminal (ou vem da variável `ADMIN_SENHA_INICIAL`, se tiver exatamente
8 caracteres). No primeiro login a troca de senha é obrigatória. O admin cria os outros usuários em **Usuários**
(papéis: admin, analista, executivo).

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
  Operacao/              automação: gatilhos, decisão (travas), chamados simulados
  Consulta/              filtros da visão técnica (número → eventos → linha do CSV)
  Relatorio/             HTML do relatório e conversão para PDF pelo navegador
src/Piersec.Web/         telas (ASP.NET Core Razor Pages)
tests/Piersec.Tests/     testes automatizados (xUnit v3), sempre com dados fictícios
regras/                  severidade, correlacao, frameworks, ativos, score, plano, automacao, servicos (.json; editáveis; validados ao subir)
exemplos/                CSVs fictícios para demonstração
dados/                   CSVs do desafio (fora do git)
var/                     dados gerados (fora do git)
```

- **Nova fonte:** criar uma classe que implementa `IAdaptadorFonte` e registrá-la no `Program.cs`.
- **Automação:** em `regras/automacao.json`, `nivelAutonomia` (0 = só sugere … 3 = mais autonomia), `modoEnsaio` (true =
  mostra o que faria sem gravar) e `limiteAcoesPorDia`. Nada é enviado a outro sistema: o chamado é um arquivo na tela Automação.
- **Ajustar uma regra:** editar `regras/*.json`. Se houver erro (tipo inexistente, parâmetro faltando, `{campo}` errado),
  a plataforma não sobe e diz o que corrigir.

## Variáveis de ambiente (opcionais)

| Variável | Para quê |
|---|---|
| `ADMIN_SENHA_INICIAL` | Senha inicial do `admin` na primeira execução (exatamente 8 caracteres). Sem ela, é sorteada e mostrada no terminal. |
| `PIERSEC_CHAVE_ANONIMIZACAO` | Chave (base64, ≥ 32 bytes) para anonimizar usuários. Sem ela, a chave é criada (cifrada) em `var/segredos/`. |
| `PIERSEC_NAVEGADOR` | Caminho do Edge/Chrome, se não estiver no lugar padrão. |
