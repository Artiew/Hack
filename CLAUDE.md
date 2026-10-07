# Projeto Piersec — instruções para o Claude Code

Plataforma do Hackathon Piersec (Semana de TI 2026, Unisanta): recebe CSVs de segurança, padroniza, classifica,
correlaciona, calcula score 0–100, monta plano de ação e gera sozinha o relatório executivo (HTML/PDF).
O briefing completo está em **`docs/PROMPT.md`** — leia antes de qualquer tarefa, junto com `docs/REQUISITOS.md`,
`docs/FONTES.md` e `docs/TROUBLESHOOTING.md`.

## Quem é o usuário e como responder
- Vitor, estudante de ADS (2º semestre), grupo de 3 iniciantes; sabe C# básico.
- Responda em português. Para cada conceito, comando ou termo novo: 2–3 linhas simples (o que é, por que existe, o que vai fazer).
- Comece com 1–2 frases do que vai fazer; termine com **feito / evidência / próximo passo**. Se estiver adivinhando, diga.
- Antes de cada fase: plano em 5 linhas e o que ele vai conseguir mostrar no fim.

## Regras inegociáveis
- **Nunca leia o conteúdo dos CSVs do desafio** (`dados/`, `var/`, arquivos `Eventos SIEM-*.csv`) nem os relatórios gerados a partir deles.
  Para entender os dados, escreva um script local que imprima **só contagens e "moldes" de formato** (dígito → 9, letra → A),
  mostre o script ao Vitor antes e rode. Testes usam só dados fictícios (`exemplos/` e os criados nos testes).
- **Nada de nuvem:** sem `git push`, sem GitHub, sem OneDrive/Drive. O projeto viaja por pen drive (ver abaixo).
- Sem credenciais/senhas/tokens no código, nos logs ou no relatório. Ações de "resposta" são sempre simuladas.
- Nunca maquiar resultado: o relatório declara limites e incertezas reais.
- Na dúvida se algo é permitido, trate como **não permitido** e pergunte ao Vitor (que consulta um mentor).
- Só marque ✅ em `docs/REQUISITOS.md` o que foi executado e provado.

## Estado atual
> **Mantenha esta seção em dia.** Ao fim de cada sessão (ou antes de gerar um bundle), atualize o que foi feito,
> o próximo passo e as pendências, e inclua no commit. É assim que a sessão no outro computador fica sabendo.

- **Fases 1, 2 e 3 concluídas** (esqueleto, fontes reais + correlação, score + plano + relatório para leigo).
- **Próximo passo aprovado pelo Vitor:** quando a confiança do score for baixa, mostrar o aviso **junto do número**
  na página 1 (ex.: "8 de 100 — risco baixo, mas só 20% foi medido") e explicar no relatório que enviar mais fontes pode
  mudar o score (fatores medidos entram na média). Depois: **Fase 4** (validação RF8, login RS1, segurança do site).
- Perguntas ao mentor ainda abertas: fim de `docs/FONTES.md`.

## Como rodar e testar
- Tudo a partir da raiz do projeto (o `global.json` daqui define o SDK 10 e o runner de testes).
- `.\rodar.cmd` (ou `.\rodar.cmd -DoZero` para apagar `var/`): compila, roda os testes e sobe `https://localhost:5001`.
  O site fica no ar enquanto o processo roda — rode em segundo plano e não feche a janela.
- Só testes: `dotnet test --solution Piersec.slnx`.
- Regras de negócio editáveis em `regras/*.json` (validadas ao subir; erro impede a plataforma de iniciar).
- Windows PowerShell 5.1: mensagem de commit com aspas → escreva num arquivo e use `git commit -F arquivo`.
- Commits terminam com a linha de co-autoria indicada pelo ambiente.

## Trabalhar em dois computadores (pen drive)
- **Levar daqui:** `git bundle create <PEN>:\piersec.bundle main` (commit tudo antes; o bundle leva só o que foi commitado).
- **Na faculdade:** o Vitor abre o Claude Code e pede para ler `<PEN>:\CLAUDE-FACULDADE.md`.
- **Trazer da faculdade:** `git pull <PEN>:\piersec-faculdade.bundle main`, depois rode os testes, leia `git log` dos commits
  novos e a seção "Estado atual" acima, e resuma ao Vitor o que mudou.
- Os CSVs do desafio **não** vão no pen drive sem autorização do mentor.
