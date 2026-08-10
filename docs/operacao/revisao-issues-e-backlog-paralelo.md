# Revisão dos issues do GitHub e do backlog paralelo
> Sessão S008 · 2026-08-10 · Escopo: 32 issues abertos (#3 a #34), `ANALISE_BUGS_E_MELHORIAS.md` e
> `BACKLOG_MVP0A_PRIORIZADO.md`, ambos no `main`

---

## 1. Situação encontrada

O repositório tem **duas linhas de trabalho divergentes**:

| Linha | Conteúdo | Onde |
|-------|----------|------|
| **A** — design + execução | 7 entregáveis, ADR-0001..0015, material de E-01, `scripts/check-docs.sh`, sessões S001–S007 | branch `claude/new-session-nxmrhb` |
| **B** — análise + backlog | `ANALISE_BUGS_E_MELHORIAS.md`, `BACKLOG_MVP0A_PRIORIZADO.md`, 32 issues | `main` (commit `4c7ab40`) |

O PR #1 foi mesclado até `67bf139`; depois disso as duas linhas seguiram separadas. `main` **não tem**
ADR-0015, o script de checagem nem as sessões S006/S007; o branch **não tem** a análise nem o backlog.

**Reconciliar as duas é pré-requisito de qualquer trabalho de código.** Enquanto durar a divergência,
qualquer decisão se apoia numa metade do projeto.

---

## 2. O que a análise acertou — e são erros meus

Estes dois achados são **reais, são defeitos da documentação que produzi** e não haviam sido
detectados por mim nem pelo `check-docs.sh`. São a contribuição mais valiosa da linha B.

### 2.1 Gap 1 · `purpose` existe na API e não existe no modelo — **confirmado**

`API.md` §4 define `"purpose": "user_initiated" | "prelaunch"` no corpo de `POST /v1/launches`, e
justifica: separar prelaunch de lançamento real na trilha. **`MODELO-DE-DADOS.md` §7.1 não tem coluna
correspondente na tabela `launch`.**

Consequência concreta: a contagem de RF-062 somaria prelaunchs como uso real. Num escritório de 10
pessoas, cada uma com um prelaunch por dia, o contador de licenças de um aplicativo inflaria em 10
usos/dia que nunca existiram — e o teto de RF-064 bloquearia trabalho legítimo. É a mesma família de
R-009, por um caminho que eu não havia mapeado.

> Correção necessária: coluna `purpose` (enum `user_initiated | prelaunch`) na tabela `launch`, e
> exclusão explícita de prelaunchs nas consultas de metering. **A tabela chama-se `launch`**, não
> `session_launch` como diz a análise.

### 2.2 Gap 2 · `ISessionBackend` não tem operação de cancelamento — **confirmado**

`ARQUITETURA.md` §4.2 lista seis operações; nenhuma cancela uma sessão que acabou de ser criada. Se o
prelaunch falhar **depois** de o RDS criar a sessão, ela fica aberta, invisível ao usuário, contando
contra o teto até a reconciliação por inatividade.

> Correção necessária: operação de cancelamento na interface, chamada no caminho de falha do
> prelaunch, com registro na trilha.

**Ambas exigem ADR** (alteram modelo de dados e contrato de interface — RP-07), e ambas cabem no
MVP-0a.

### 2.3 Reforços úteis, já previstos

Teste de violação multi-tenant (já é **V-02** em `SEGURANCA.md` §7), varredura de segredos (já é
**PS-04**), health check (já é **RNF-040**), reconciliação manual de licenças (já está em `API.md` §6).
A análise dá a eles prioridade e estimativa — contribuição legítima.

---

## 3. Contradições com decisões já aceitas

### 3.1 Issue #5 contradiz o ADR-0009 — **e é regressão de segurança**

O issue pede as chaves de configuração `RdpSigning:CertificatePath` e
`RdpSigning:CertificatePassword` ("mínimo 64 chars aleatórios").

O **ADR-0009** decidiu o contrário: a chave privada de assinatura fica no **repositório de
certificados da máquina, marcada como não exportável**, com a conta de serviço tendo apenas permissão
de uso. Não há arquivo PFX, e portanto não há senha de PFX a guardar.

Implementar o issue como está reintroduz exatamente o que o ADR evitou: material de chave em arquivo,
protegido por uma senha em configuração. A chave de assinatura é o **ativo A-01** do modelo de ameaças
— o mais perigoso do MVP-0, porque comprometê-la permite forjar `.rdp` que todas as estações confiam
(AM-01, AM-02).

> Ou o issue se alinha ao ADR-0009, ou é preciso um ADR novo que o substitua. Não pode ser resolvido
> em código.

### 3.2 Issue #4 propõe symlink para caminho absoluto local

`/docs (symlink para /home/user/APPBRIDGE/docs)`. Um symlink para caminho absoluto de uma máquina
específica não funciona em nenhum outro clone. Se `/docs` e `/src` forem o mesmo repositório, não há
symlink a fazer.

### 3.3 A análise pede "stub de cofre no MVP-0"

*"Documentar que MVP-0 não vai ter cofre funcional — apenas stub que recusa qualquer chave A1."*

O cofre é **V2** (RF-055..RF-061). Não existe no MVP-0 nem no MVP-0a, e não há nada a stubar. A ação
é desnecessária.

---

## 4. Lacuna estrutural: o épico de infraestrutura não existe nos issues

Os 32 issues cobrem E-00 (setup), E-01a (medições) e E-02 a E-05 (código). **Não há um único issue
para comprar o servidor, criar o domínio, instalar a pilha RDS, aplicar FSLogix/AppLocker, ingressar
as estações ou rodar a varredura externa** — o épico E-01, 34 pontos, que `ROADMAP.md` e o **ADR-0013
item 4** definem como o que vem **antes da primeira linha de código**.

O efeito prático é mais grave que a omissão: **os issues de E-01a não são executáveis.** Medir a
confiabilidade do Connection Broker (#8) exige uma implantação RDS que ninguém providenciou. Medir
latência de assinatura sob carga (#9) exige o certificado de assinatura, que se cria em T-106. Testar
o procedimento de revogação (#10) exige sessões reais.

O backlog paralelo começa pelo código e trata a infraestrutura como se já existisse.

---

## 5. Colisão de identificador — ADR-0015

| | |
|---|---|
| **Issue #7 e a análise §Recomendações** | "Criar **ADR-0015** — Encadeamento Criptográfico da Trilha" |
| **Branch A, sessão S007** | **ADR-0015** — Checagem de consistência no fechamento de sessão, **aceito** |

ADR aceito é imutável (RA-05) e o número não se reaproveita. O issue #7 precisa ser corrigido.

> **A lição vale mais que a correção:** a colisão aconteceu porque um número foi **reservado antes de
> o ADR existir**. Números de ADR devem ser atribuídos no momento da escrita, nunca no planejamento —
> caso contrário duas linhas de trabalho reservam o mesmo. Esta revisão, por isso, não pré-atribui
> nenhum.

---

## 6. Referências de requisito incorretas

Comparadas com `REQUISITOS.md` aprovado. Não são detalhe: a rastreabilidade RA-04 é o que permite
saber por que uma tarefa existe.

| Onde | Diz | Correto |
|------|-----|---------|
| Issue #23 | Endpoint de Catálogo (**RF-001**) | RF-001 é autenticação; catálogo é **RF-011** |
| Issue #25 | Geração de `.rdp` temporário (**RF-019**) | RF-019 é a assinatura; o arquivo temporário é **RF-020**, a geração é **RF-018** |
| Issue #26 | Endpoint de Launch (**RF-006, RF-020**) | RF-006 é logout; o lançamento é **RF-018** e **RF-021** |
| Issue #16 e análise §Oport. 5 | Health check (**RNF-034**) | RNF-034 é a meta de disponibilidade; health check é **RNF-040** |
| Análise, Problema 1 | "choque com **RNF-040** (availability 99,5%)" | Invertido: a disponibilidade é **RNF-034** |
| Análise, Risco 1 | "**ADR-0009** reconhece que nada impede o operador de assinar" | ADR-0009 trata da assinatura do `.rdp`. O achado é **AM-33** em `SEGURANCA.md`, risco **R-020** |
| Análise, Risco 1 | "registrar que **AM-02** (abuso operacional) fica 🔴" | AM-02 é comprometimento da chave de assinatura. O abuso do cofre é **AM-33** |
| Análise, Risco 2 | "violaria **RNF-030**" | RNF-030 trata de **novos lançamentos** em 60 s; a sessão aberta está fora do seu escopo — é por isso que R-014 existe |
| Análise, Risco 2 | "afeta RNF-014, RNF-020, RNF-023, RNF-036" | Nenhum se relaciona a encerramento de sessão (redirecionamento, UTC, minimização, `tenant_id`) |
| Análise, Risco 3 | "afeta **RNF-022** (trilha verificável)" | RNF-022 é a auditoria **bloqueante**. Verificabilidade por terceiro não é requisito — é a pendência **PS-03** |
| Análise, Risco 3 | "o diferencial **DIFI-02** (segurança de certificado)" | Certificado é **DIF-01**; DIF-02 é metering |
| Análise, Risco 4 | "afeta **RNF-026** (dimensionamento)" | RNF-026 é capacidade para 500 usuários; contagem inflada não o afeta |
| Análise, Gap 2 | "expiração por inatividade (**RNF-033**)" | RNF-033 é backup |
| Análise, Gap 1 | tabela `session_launch` | A tabela é **`launch`** |

---

## 7. Erro meu no rastro de auditoria

Os logs das sessões S004 a S007 foram datados **2026-08-08**, mas os commits correspondentes são de
**2026-08-09 e 2026-08-10**. Corrigido nesta sessão, com renomeação dos arquivos.

É um defeito da mesma natureza que os seis achados de S006 — e passou pelo `check-docs.sh`, que não
verifica data de log contra data de commit. **Verificação a acrescentar ao script.**

---

## 8. Recomendação

| # | Ação | Por quê |
|---|------|---------|
| 1 | **Reconciliar as duas linhas** — trazer a análise e o backlog para o branch, ou mesclar o branch no `main` | Enquanto durar, qualquer decisão se apoia em metade do projeto |
| 2 | **Escolher um backlog só.** Recomendo manter `ROADMAP.md` como fonte (é o entregável aprovado, com rastreabilidade completa) e **incorporar a ele** o que o backlog paralelo trouxe de novo — priorização, estimativa e os dois gaps | RA-04: dois backlogs concorrentes tornam a rastreabilidade indecidível |
| 3 | **Criar os issues de E-01** antes de qualquer issue de código, e marcar os de E-01a como bloqueados por eles | ADR-0013 item 4; sem isso E-01a não é executável |
| 4 | **Um ADR novo** para o Gap 1 (coluna `purpose`) e o Gap 2 (cancelamento) | Alteram modelo e interface (RP-07) |
| 5 | **Outro ADR** para o encadeamento da trilha, no lugar do ADR-0015 pedido no issue #7 — **atribuindo o número só na hora de escrever** | Colisão de número |
| 6 | **Corrigir o issue #5** para refletir o ADR-0009 — sem `CertificatePath`/`CertificatePassword` | Regressão de segurança sobre o ativo A-01 |
| 7 | **Corrigir as referências de requisito** dos issues #16, #23, #25, #26 | RA-04 |
| 8 | Acrescentar ao `check-docs.sh` a verificação de data de log × data de commit | O erro da §7 passou despercebido |

**Nenhuma alteração foi feita nos issues do GitHub.** Mexer neles é ação para fora do repositório e
depende de decisão de Frederico.
