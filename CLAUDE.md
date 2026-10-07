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
- **Feito depois da Fase 3 (07/10/2026):**
  - aviso de **confiança baixa junto do número** na página 1 e na tela do lote; seção 2 explica por que mais fontes mudam o score;
  - aviso de **empresas/ambientes possivelmente diferentes** quando dois arquivos do mesmo tipo não têm nenhuma máquina em
    comum (`VerificadorAmbientes`). É aviso, não erro. Nos dados reais aparece para SIEM-1 × SIEM-2.
  - 144 testes passando.
- **Fase 4 concluída** (validação, login + auditoria + proteções do site, dados cifrados em repouso):
  - **Parte 1 feita:** validação RF8 (`Validacao/ValidadorLote`, 12 verificações, selo no relatório, "NÃO VALIDADO" se
    reprovar, relatório retido se sobrar credencial). Demo: `exemplos/alertas-corrompido-ficticio.csv` reprova a V04.
    Exemplos fictícios são gerados por `exemplos/gerar-exemplos.ps1`.
  - **Parte 2 feita:** login RS1 (`Core/Seguranca`: PBKDF2, bloqueio, troca obrigatória, papéis), auditoria RS4
    (`var/auditoria.jsonl`, página /Auditoria), cabeçalhos/CSP/cookie `__Host-`, limite de 10 logins/min, páginas
    Entrar/TrocarSenha/Sair/Usuarios/Auditoria/Negado. Executivo não envia e não vê tabelas técnicas.
    Para testar com senha conhecida: `$env:ADMIN_SENHA_INICIAL='8chars!!'` antes do `rodar.cmd -DoZero`.
  - **Parte 3 feita:** lotes, resumos, relatórios e chave de anonimização gravados cifrados (`ICifrador`, Web usa Data
    Protection com chaves em `var/chaves` + DPAPI). PDF gerado em `var/tmp` e apagado. 192 testes passando.
- **Próximo passo: Fase 5 — operação automatizada (30% da nota):** playbooks com níveis de autonomia 0–3, dry-run, nada
  irreversível sem aprovação humana (ativos críticos sempre com humano), limite de ações por período, log de decisão,
  chamados e remediação SIMULADOS (gerar o artefato, não enviar), idempotência e verificação contínua (chamado fecha
  sozinho se o problema some, reabre se volta). Ver `docs/PROMPT.md` seção 5.2.
- Atenção: `rodar.cmd -DoZero` apaga também usuários e chaves; na 1ª execução a senha do admin aparece UMA vez no terminal.
- Ideia aprovada para a Fase 6: filtro por ambiente (cada arquivo separado) sem precisar reenviar.
- Perguntas ao mentor ainda abertas: fim de `docs/FONTES.md` (inclui: SIEM-1 e SIEM-2 são empresas diferentes?).

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
