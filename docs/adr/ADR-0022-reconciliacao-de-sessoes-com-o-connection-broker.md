# ADR-0022 — Reconciliação de sessões com o Connection Broker
Data: 2026-09-26 · Status: **aceito** (decisão delegada por Frederico em 2026-09-26, S020) · Autor: Arquiteto de Software Principal (S019, a pedido de Frederico para seguir à T-602)

## Contexto

O ADR-0021 passou a registrar a sessão no lançamento com `backend_session_id` nulo (vínculo pendente) e
deixou para a reconciliação três tarefas:

- vincular a sessão pendente à sessão real do RDS;
- fechar a sessão que terminou fora do AppBridge;
- gravar o início e o fim de sessão do RF-038.

O ADR-0006 exige essa reconciliação periódica como defesa contra a contagem inflada de licenças (R-009,
R-017). A T-602 aceita quando uma sessão encerrada fora do AppBridge é fechada em até um ciclo, e ela
valida o PRE-23 em campo.

O Control Plane não sabe quem é o usuário de uma sessão do RDS. O Connection Broker (`Get-RDUserSession`)
devolve `DOMÍNIO\conta`, o host e o `UnifiedSessionId`. O AppBridge guarda `ad_object_sid` e `upn`. O
prefixo do UPN nem sempre é o `sAMAccountName`, então comparar nomes seria uma heurística frágil.

## Decisão

1. **`ISessionBackend.ListActiveSessionsAsync(hosts)`** devolve as sessões que o backend conhece nos
   hosts informados: host, identificador no backend, SID do usuário e estado. O `RdsSessionBackend`
   consulta o Connection Broker configurado em `RdsSession:ConnectionBroker` com `Get-RDUserSession`.
   O mesmo script PowerShell traduz `DOMÍNIO\conta` em SID com `NTAccount.Translate`. **A comparação
   é por SID** (`user_account.ad_object_sid`), nunca por nome.
2. **O `SessionReconciler` é um serviço de fundo que roda a cada ciclo** (`SessionReconciler:IntervalSeconds`,
   padrão 60 s, `PREMISSA:` PRE-30), tenant a tenant, só para tenants `active`. A consulta ao backend é
   feita **antes** de abrir a transação. Depois, na transação do tenant, o reconciler pega as mesmas
   travas consultivas por usuário do ADR-0021, em ordem, e só então relê as sessões abertas. Assim, ele
   não disputa linha com um lançamento em curso.
3. **Regras, quando o backend responde:**
   - Sessão **vinculada** que está na lista: `last_seen_at` é atualizado.
   - Sessão **vinculada** que saiu da lista: é fechada com `reconciled_missing`.
   - Sessão **pendente**: é vinculada à sessão do backend no mesmo host e com o mesmo SID que ainda não
     pertença a outra sessão aberta, preferindo a mais recente. O vínculo grava `SessionStarted`.
   - Sessão **pendente** sem par e já fora da janela do PRE-29: é fechada com `reconciled_missing`,
     com o motivo `never_connected`.
   - Sessões do backend sem registro no AppBridge (administrador por RDP, sessão anterior à implantação)
     **não são adotadas**. Só contam no log do ciclo. Adotar criaria sessão sem lançamento, sem estação
     e sem origem, e o metering do ADR-0006 conta o que o AppBridge lançou.
4. **Regra, quando o backend falha** (Connection Broker fora, PowerShell ausente, erro de tradução): nada
   é fechado por ausência. Sessões abertas sem sinal há mais de `SessionReconciler:StaleAfterMinutes`
   (padrão 30 min, `PREMISSA:` PRE-31) são fechadas com `stale_expired`. É a expiração por inatividade
   que o ADR-0006 exige. Isso prefere subcontar a supercontar, porque a contagem inflada bloqueia
   trabalho legítimo (R-009) e a subcontada não. A sessão real no RDS não é tocada.
5. **Trilha:** cada vínculo grava `access_event` `session_started`, e cada fechamento grava
   `session_ended`. O `payload` leva `sessionId`, `sessionHostId` e `endReason`, e o fim leva também
   `durationSeconds` (RF-038). Esses eventos vão na mesma transação do ciclo do tenant. Não são
   bloqueantes no sentido da RNF-022 (fim de sessão está excluído): se a transação falha, o ciclo
   seguinte refaz o trabalho.
6. **Falha de um tenant não para os outros.** Conflito de concorrência ou erro de banco só adia aquele
   tenant para o próximo ciclo, com registro no log estruturado.

## Alternativas consideradas

- **Comparar pelo nome da conta.** Rejeitada: o UPN e o `sAMAccountName` divergem em ambientes reais, e o
  erro seria vincular a sessão de uma pessoa ao lançamento de outra.
- **Adotar as sessões que o AppBridge não lançou.** Rejeitada por ora: elas não têm lançamento, estação nem
  origem, e a trilha ficaria com linhas sem "de onde". Fica como decisão de produto para o metering do
  MVP-1.
- **Fechar por ausência mesmo quando o backend falha.** Rejeitada: um Connection Broker fora do ar fecharia
  todas as sessões no primeiro ciclo.
- **Chamar o backend dentro da transação.** Rejeitada: prenderia as travas por usuário durante até 15 s
  de PowerShell e atrasaria os lançamentos das 8h (R-016).

## Consequências

**Positivas:** o ciclo de vida da sessão fecha (RF-038), as sessões pendentes são vinculadas ou
fechadas, a contagem que o metering do MVP-1 vai usar deixa de inflar sozinha, e o R-034 é mitigado.

**Negativas:** o Control Plane passa a depender do módulo `RemoteDesktop` e da tradução de SID na
máquina Windows (ADR-0009). Uma sessão encerrada fora do AppBridge continua contando por até um ciclo.
Com o backend fora, conta por até 30 minutos.

**Riscos:** o custo e a confiabilidade de `Get-RDUserSession` com dezenas de sessões são exatamente o
PRE-23, que só se mede em campo. Uma sessão vinculada em estado desconectado continua contando, e é assim
que o RDS a trata. Se o PRE-31 for curto demais e o broker oscilar, sessões vivas são fechadas no banco
e o próximo lançamento cria outra linha. Só a trilha perde precisão, e o usuário não é afetado.

## Requisitos relacionados (RF/RNF)

RF-038, RF-062, RNF-022, RNF-035 · ADR-0004, ADR-0006, ADR-0009, ADR-0021 · R-009, R-016, R-017, R-034 · PRE-23
