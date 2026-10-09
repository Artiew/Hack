# ARV Security — Hackathon Piersec 2026

Plataforma local que recebe CSVs de segurança, padroniza registros, classifica severidade com justificativa,
correlaciona eventos, calcula score de risco e gera relatórios executivo e técnico em HTML e PDF.
A operação automática registra chamados e prepara planos **simulados**, com aprovação humana conforme as regras.

Os dados do desafio, relatórios gerados, contas e chaves ficam somente na máquina e não acompanham o código da entrega.

## Como iniciar

Requisitos: Windows, SDK do .NET 10 compatível com `global.json` (10.0.401 ou versão permitida por essa configuração)
e Microsoft Edge ou Google Chrome para gerar os PDFs.

Dois cliques em **Abrir Plataforma.cmd** compilam a solução, iniciam o site e abrem o navegador.
Esse atalho pula a execução dos testes. Mantenha o terminal aberto enquanto usar o site.

Pelo PowerShell, na pasta do projeto:

```powershell
.\rodar.ps1
```

Esse comando compila, executa os testes e inicia o site. Se o PowerShell bloquear scripts:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\rodar.ps1
```

Endereço: **https://localhost:5001**. Para conferir o certificado HTTPS de desenvolvimento:

```powershell
dotnet dev-certs https --trust
```

Se a plataforma já estiver aberta, o atalho abre o navegador para a instância existente.
Para carregar alterações de código, encerre essa instância com Ctrl+C e inicie novamente.

## Primeiro acesso e recuperação

Na primeira execução é criado o usuário `admin` com uma senha aleatória de oito caracteres,
mostrada uma vez no terminal, ou obtida da variável `ADMIN_SENHA_INICIAL` se ela tiver exatamente oito caracteres.
A troca da senha e o cadastro da verificação em duas etapas são obrigatórios para administradores.

O admin cria contas com os perfis admin, analista e executivo. O perfil executivo tem acesso à visão executiva;
admin e analista também acessam o relatório técnico. O 2FA é opcional para os demais perfis.

Guarde os códigos de recuperação em local privado. Se esquecer a senha do admin, encerre a plataforma e use
**Esqueci a senha do admin.cmd**. Esse atalho gera uma senha temporária e desativa o fator antigo;
será necessário trocar a senha e cadastrar o aplicativo novamente.

O parâmetro `-DoZero` de `rodar.ps1` apaga o armazenamento local, inclusive lotes, usuários, auditoria e chaves.
Não é necessário para iniciar normalmente.

## Envio e limites da análise

- Envie os CSVs autorizados pela tela **Visão geral**. Fontes suportadas: vulnerabilidades, alertas e inventário de exposição à internet.
- Vários arquivos do mesmo tipo não representam várias fontes distintas. Uma correlação exige os vínculos previstos nas regras;
  quando esses vínculos não existem, a plataforma informa a limitação. Correlação não comprova invasão.
- O score varia de 0 a 100, sendo 100 o máximo risco no modelo. Fatores ausentes não viram zero;
  a cobertura e a confiança indicam quanto foi possível medir. Score baixo com dados incompletos não comprova segurança completa.
- Prejuízo e custo são faixas estimadas pelas premissas do exercício, não perdas comprovadas, orçamento aprovado ou economia garantida.
- O simulador recalcula um cenário projetado; não altera máquinas. A operação não envia comandos a sistemas externos.
- As verificações conferem consistência interna, não a veracidade das fontes. Relatórios reprovados são sinalizados;
  suspeita de credencial no filtro final retém o conteúdo.
- Lotes, relatórios e estado da operação são armazenados cifrados. Um PDF baixado precisa ser protegido por quem o guarda.

## Arquivos necessários

| Caminho | Finalidade |
|---|---|
| `src/Piersec.Core/` | Coleta, padronização, avaliação, operação, validação e relatórios |
| `src/Piersec.Web/` | Aplicação web, páginas e estilos |
| `regras/` | Configurações de severidade, score, plano, custos, correlação, fontes e automação |
| `tests/Piersec.Tests/` | Testes da solução, com dados fictícios definidos no código |
| `Piersec.slnx` e `global.json` | Solução e configuração do SDK e dos testes |
| `rodar.ps1` e atalhos `.cmd` | Inicialização e recuperação do admin |
| `.gitignore` | Exclusão de dados, saídas geradas, arquivos de compilação e segredos |
| `Reparar-AnalisesLocais.ps1` | Preservação de análises antigas quando suas chaves não abrem na máquina atual |

A solução inclui o projeto de testes na compilação, mesmo quando o atalho não executa os testes.
Não remover `tests/Piersec.Tests/`. Os exemplos usados pela suíte estão em `DadosFicticios.cs`;
a pasta `exemplos/` não é necessária.

Para executar a suíte:

```powershell
dotnet test --solution Piersec.slnx
```

## Configurações opcionais

| Variável | Finalidade |
|---|---|
| `ADMIN_SENHA_INICIAL` | Senha inicial do admin, somente na criação da primeira conta |
| `PIERSEC_CHAVE_ANONIMIZACAO` | Chave de anonimização em base64, com no mínimo 32 bytes; se ausente, a plataforma cria e guarda uma chave cifrada |
| `PIERSEC_NAVEGADOR` | Caminho do Edge ou Chrome, quando não estiver no local padrão |

Nunca colocar os valores dessas variáveis no código, no Git ou na documentação de entrega.
As regras em `regras/*.json` são validadas na inicialização; configuração inválida impede o site de iniciar.
