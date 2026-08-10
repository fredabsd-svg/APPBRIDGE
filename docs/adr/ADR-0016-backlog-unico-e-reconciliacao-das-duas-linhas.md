# ADR-0016 — Backlog único: `ROADMAP.md` como fonte, incorporando a revisão da linha B
Data: 2026-08-10 · Status: **aceito** · Autor: Arquiteto de Software Principal · Aprovado por: Frederico (2026-08-10)

## Contexto

A sessão S008 constatou que o repositório tinha **duas linhas de trabalho divergentes** (R-026): o
`main` com `ANALISE_BUGS_E_MELHORIAS.md`, `BACKLOG_MVP0A_PRIORIZADO.md` e 32 issues; o branch de
trabalho com ADR-0015, `scripts/check-docs.sh` e as sessões S006–S008.

O problema não era a existência de duas análises — **duas leituras independentes do mesmo projeto são
valiosas, e a linha B encontrou dois defeitos reais que a linha A não viu**. O problema era haver
**dois backlogs concorrentes** para o mesmo MVP-0a, com dois conjuntos de identificadores de tarefa
(`T-101..T-1204` e `T-00.x..T-06.x`), o que torna a rastreabilidade de RA-04 indecidível: uma tarefa
citada num documento não é localizável no outro.

Frederico decidiu reconciliar mantendo `ROADMAP.md` como fonte.

## Decisão

### 1. `ROADMAP.md` é o backlog único do projeto

É o entregável aprovado, com rastreabilidade requisito↔tarefa completa e critério de aceite por
tarefa. Todo issue, plano ou cronograma referencia os identificadores dele (`E-01..E-12`,
`T-101..T-1204`).

### 2. `BACKLOG_MVP0A_PRIORIZADO.md` passa a anexo histórico

Não é apagado — é registro de um trabalho legítimo e de decisões de priorização que foram
incorporadas. Recebe cabeçalho indicando que **não é fonte de tarefas** e apontando para o
`ROADMAP.md`.

### 3. `ANALISE_BUGS_E_MELHORIAS.md` permanece, com errata

O conteúdo é útil e dois de seus achados viraram tarefa. Mas ele contém **14 referências de requisito
incorretas** (§6 da revisão S008), e quem o usar como fonte de rastreabilidade será levado a
requisitos errados. Recebe cabeçalho de errata apontando para a revisão. **O corpo não é reescrito**:
é registro de uma análise feita numa data, e reescrevê-lo apagaria o que de fato foi dito.

### 4. O que a linha B contribuiu é incorporado ao `ROADMAP.md`

| Contribuição | Onde entra |
|---|---|
| **Gap 1** — coluna `purpose` ausente no modelo de dados | Nova tarefa **T-207** em E-02 · 2 pts |
| **Gap 2** — `ISessionBackend` sem cancelamento | Nova tarefa **T-506** em E-05 · 3 pts |
| **Varredura de segredos antecipada** (PS-04, era MVP-1) | Nova tarefa **T-1106** em E-11 · 2 pts |
| Priorização por caminho crítico, estimativas por fase | Já convergente com `ROADMAP.md` §4 e §5 |
| Medições E-01a | Já são **T-005** e as tarefas T-602, T-1002, T-1003 |

Total do MVP-0 passa de **241 para 248 pontos**; o recorte do MVP-0a, de ~95 para **~100**.

### 5. Os issues do GitHub são realinhados ao `ROADMAP.md`

Correções necessárias, registradas em B-010 e B-011 e detalhadas na revisão S008: criar os issues do
épico E-01, corrigir o issue #5 (contradiz o ADR-0009), corrigir as referências de requisito de #16,
#23, #25 e #26, e corrigir o issue #7 (colisão de ADR-0015).

### 6. Número de ADR é atribuído no momento da escrita

A colisão do ADR-0015 aconteceu porque um número foi **reservado em um plano** antes de o ADR existir.
Nenhum documento de planejamento volta a pré-atribuir número de ADR.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| **Adotar `BACKLOG_MVP0A_PRIORIZADO.md` como fonte** | É mais granular e tem boa priorização, mas não tem rastreabilidade requisito↔tarefa, não cobre o épico de infraestrutura e não é entregável aprovado. Adotá-lo exigiria reconstruir a rastreabilidade que o `ROADMAP.md` já tem. |
| **Manter os dois, com mapeamento entre eles** | Dobra o custo de manutenção e garante que um dos dois envelheça. É a situação que produziu R-026. |
| **Apagar os documentos da linha B** | Destruiria o registro de uma análise que encontrou dois defeitos reais, e violaria o espírito de RA-06. |
| **Reescrever a análise corrigindo as 14 referências** | Falsificaria o registro histórico. Errata preserva o que foi dito e informa o leitor. |

## Consequências

**Positivas**
- Um só conjunto de identificadores; rastreabilidade RA-04 volta a ser decidível.
- Os dois defeitos reais encontrados pela linha B entram no backlog com tarefa, estimativa e critério
  de aceite.
- O registro das duas análises é preservado.

**Negativas**
- Os 32 issues existentes referenciam identificadores que deixam de ser a fonte. Realinhá-los é
  trabalho manual.
- `BACKLOG_MVP0A_PRIORIZADO.md` continua no repositório e alguém pode lê-lo sem ver o cabeçalho.

**Riscos**
- **A divergência revelou um número que nenhuma das duas linhas tinha sozinha.** A linha B estimou 96
  pontos para o MVP-0a **sem o épico de infraestrutura**; a linha A estimou ~95 pontos **com ele**.
  Somando o que cada uma cobre, o MVP-0a real fica perto de **130 pontos** — não 96 nem 95. Isso
  precisa ser reavaliado contra a data de out/2026 do ADR-0013 e está registrado como **R-030**.
- O cronograma do backlog paralelo tem inconsistência interna: a tabela de datas termina o E-05 em
  **31/dez/2026**, enquanto o texto afirma "data alvo do dogfood: out/2026". Além disso, assume
  **8 pontos/semana com 40 h/semana dedicadas**, contra a premissa **PRE-26** (dedicação de 40% a
  60%). O próprio documento reconhece que a metade dobra os prazos — o que levaria o MVP-0a para
  meados de 2027 e atropelaria o piloto.
- Realinhar 32 issues manualmente é trabalho tedioso e propenso a erro. Mitigação: fazer em lote, com
  a revisão S008 §6 como lista de conferência.

## Requisitos relacionados

Não altera RF nem RNF. Altera a fonte do backlog e acrescenta T-207, T-506 e T-1106 ·
Fecha **B-010** · Origem: revisão S008, decisão de Frederico
