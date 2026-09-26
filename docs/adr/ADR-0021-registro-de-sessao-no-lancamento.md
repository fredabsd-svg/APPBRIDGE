# ADR-0021 — Registro de sessão no lançamento e vínculo pendente
Data: 2026-09-26 · Status: **aceito** (decisão delegada por Frederico em 2026-09-26, S020) · Autor: Arquiteto de Software Principal (S018, a pedido de Frederico para iniciar a T-601)

## Contexto

A T-601 pede um `SessionRegistry` que registre o início da sessão, a reutilize no segundo aplicativo
(RF-024) e a vincule ao lançamento (`launch.session_id`, MODELO-DE-DADOS §7.1). Até a S017, o
`RdsSessionBackend` só reutilizava sessões já presentes no banco, mas nenhum código as criava. Por isso
`launch.session_id` ficava nulo em todo lançamento concedido, e a capacidade do host (`max_sessions`)
não enxergava as sessões que o próprio AppBridge acabara de encaminhar.

No lançamento, o Control Plane ainda não conhece o identificador da sessão no RDS. A sessão só nasce
quando o `mstsc` conecta, depois da resposta. Esse identificador (`backend_session_id`) só pode ser
descoberto no Connection Broker, que é trabalho da reconciliação (T-602, PRE-23).

Dois lançamentos quase simultâneos do mesmo usuário são o caso normal, e não exceção: o prelaunch no
logon e o primeiro clique no atalho às 8h (R-016). Sem serialização, os dois veem "nenhuma sessão",
escolhem hosts diferentes e criam duas sessões. Isso quebra o RF-024 e infla a contagem de licenças (R-009).

## Decisão

1. **O `SessionRegistry` é dono da regra de reutilização.** No lançamento, ele procura uma sessão ativa do
   usuário em host `online` ou `draining` do pool do aplicativo. Um host em drenagem não recebe sessão
   nova, mas reconectar a uma sessão existente é o comportamento do próprio RDS. Se não há sessão, o
   registry pede ao `ISessionBackend.ResolveHostAsync` só a escolha do host. A regra de negócio sai do
   `RdsSessionBackend`, como pede o §4.2 da ARQUITETURA (RNF-035).
2. **A sessão nova é registrada no lançamento concedido, e só depois da assinatura.** A linha de `session`
   nasce na mesma transação que grava o `launch` concedido e a sua trilha (ADR-0007), e o `launch.session_id`
   aponta para ela. Se a assinatura ou a política falham, nenhum `.rdp` sai. O `mstsc` não conecta, e
   nenhuma linha de sessão é criada.
3. **`backend_session_id` passa a aceitar nulo, e nulo significa vínculo pendente.** A reconciliação
   (T-602) preenche o valor quando encontra a sessão no Connection Broker. Ela também fecha com
   `reconciled_missing` a sessão que nunca apareceu. Não se cria estado novo nem coluna nova.
4. **Uma sessão pendente só ocupa vaga dentro de uma janela.** Ela conta para reutilização e para a
   capacidade do host enquanto `last_seen_at` estiver dentro de
   `SessionRegistry:PendingBindingMinutes`, que por padrão é 10 minutos. `PREMISSA:` PRE-29: 10 minutos
   cobrem a validade de 60 s do `.rdp` (PRE-07), o tempo de conexão e pelo menos um ciclo da reconciliação.
   Reutilizar uma sessão pendente renova o `last_seen_at`. Uma sessão já vinculada não expira por essa
   janela: quem a fecha é a reconciliação.
5. **O posicionamento é serializado por usuário.** Antes de procurar a sessão, o registry pega
   `pg_advisory_xact_lock` numa chave derivada de `(tenant_id, user_account_id)`, dentro da transação do
   lançamento. O segundo pedido espera o primeiro terminar e então reutiliza a sessão que ele registrou.
   Pedidos de usuários diferentes não se bloqueiam.
6. **Registrar a sessão não grava `SessionStarted`.** O evento de início do RF-038 exige que a sessão exista
   no RDS. Ele passa a ser gravado quando a reconciliação vincular a sessão (T-602). O lançamento em si
   já é auditado pelo `launch` (RF-037).

## Alternativas consideradas

- **Só a reconciliação cria as linhas de sessão.** Assim, `launch.session_id` ficaria nulo para sempre ou
  exigiria um vínculo posterior por heurística. A capacidade também ficaria cega entre o lançamento e o
  ciclo seguinte, que é exatamente o pico das 8h. Rejeitada.
- **Coluna `status` (`pending`/`bound`).** Seria mais explícita, mas repetiria a informação que o
  `backend_session_id` nulo já carrega e aumentaria a migração. Rejeitada por ora. Pode voltar se a T-602
  precisar de mais estados.
- **Índice único parcial por usuário com `ended_at IS NULL`.** Impediria o mesmo usuário de ter sessões
  em dois pools, o que é legítimo quando os aplicativos estão em pools diferentes. Rejeitada em favor da
  trava consultiva.
- **Registrar a sessão antes de assinar e cancelá-la na falha.** Isso cria trabalho de compensação sem
  ganho, porque nenhuma sessão RDS existe se o `.rdp` não saiu. Rejeitada. A `CancelSessionAsync` continua
  valendo para backends que criam a sessão antes do cliente conectar (T-506).

## Consequências

**Positivas:** o segundo aplicativo vai para o host da sessão existente (RF-024), e `launch.session_id`
passa a ser preenchido. A capacidade do host passa a contar as sessões recém-encaminhadas. O caso
prelaunch mais clique deixa de criar duas sessões. O `RdsSessionBackend` fica só com o que é do RDS.

**Negativas:** até a T-602 existir, nenhuma sessão é vinculada nem fechada. Uma sessão aberta e depois
encerrada no RDS continua "ativa" no banco até a janela vencer, e só a partir daí deixa de ocupar vaga. O
`last_seen_at` deixa de ser atualizado só pela reconciliação: o reúso de sessão pendente também o renova.

**Riscos:** se a janela for curta demais para o ciclo real da reconciliação, uma sessão viva pode deixar
de ser reutilizada e o usuário ganha uma segunda sessão em outro host. Se for longa demais, uma conexão
que falhou segura uma vaga por mais tempo. Os dois efeitos só aparecem com mais de um host no pool, e o
dogfood tem um. A trava consultiva prende a transação do lançamento por usuário durante a assinatura.
Isso é aceitável, porque o mesmo usuário raramente pede mais de dois lançamentos por segundo, mas deve ser
medido junto com o PRE-12.

## Requisitos relacionados (RF/RNF)

RF-021, RF-024, RF-037, RF-038, RF-062, RNF-035 · ADR-0006, ADR-0007, ADR-0011, ADR-0016 · R-009, R-016
