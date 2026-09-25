# ADR-0018 — Persistência da resposta idempotente do lançamento
Data: 2026-09-24 · Status: **aceito** · Autor: Arquiteto de Software Principal

## Contexto

O ADR-0012 exige que `POST /v1/launches` devolva a mesma resposta durante a validade de 60 segundos
do `.rdp`, rejeite a mesma chave com outro corpo e recuse a reapresentação da chave depois de
expirada. A pendência PD-04 deixava aberto onde guardar a resposta e como impedir dois lançamentos
quando chegam requisições simultâneas.

## Decisão

1. A resposta e o hash do corpo ficam em `launch_idempotency`, tabela PostgreSQL com filtro e chave
   única `(tenant_id, idempotency_key)`. A chave é um UUID e o hash usa SHA-256 do aplicativo,
   propósito e estação de trabalho.
2. A reserva da chave, a linha `launch`, a resposta e o evento de auditoria aplicável são gravados na
   mesma transação. O índice único serializa retentativas simultâneas; uma delas lê e devolve o
   resultado que venceu.
3. O JSON de resposta — que contém o `.rdp` assinado — fica disponível por 60 segundos. Uma tarefa de
   serviço remove o corpo expirado a cada 15 segundos. A chave, o hash e a data de expiração ficam
   como tombstone para que a mesma chave seja recusada depois do prazo.
4. Repetir a chave com outro hash devolve `409 IDEMPOTENCY_CONFLICT`; reapresentar uma chave expirada
   devolve `409 IDEMPOTENCY_KEY_EXPIRED`. Uma repetição válida devolve o mesmo status e corpo sem
   criar outro lançamento ou contagem.
5. A limpeza de corpos expirados é tarefa explícita do Control Plane. Ela usa um `UPDATE` limitado às
   respostas vencidas e registra a quantidade removida no log operacional, sem ler nem exportar
   dados de outro tenant.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Memória local | Reinícios e múltiplas instâncias perdem a resposta, permitindo um segundo lançamento após uma falha de rede. |
| Cache distribuído externo | Acrescenta infraestrutura ao MVP-0a e separa o estado idempotente da transação que grava o lançamento. |
| Resposta no log de auditoria | A trilha é append-only e tem retenção de meses; guardar um `.rdp` ali aumenta exposição e mistura cache com evidência permanente. |

## Consequências

**Positivas:** idempotência sobrevive a reinícios, instâncias concorrentes e falhas de rede; o corpo
assinado é removido logo após o TTL.

**Negativas:** tombstones permanecem no PostgreSQL e crescem com o número de lançamentos. A limpeza
temporária percorre a tabela por `expires_at`; o índice `(tenant_id, expires_at)` correspondente reduz o trabalho.

**Riscos:** a retenção indefinida de tombstones precisa ser revista com a volumetria medida no dogfood.
Um defeito no pruner pode manter respostas RDP expiradas no banco, embora o endpoint ainda recuse
reproduzi-las depois dos 60 segundos.

## Requisitos relacionados

RF-018..RF-021, RF-037, RF-039, RNF-019, RNF-022 · ADR-0007, ADR-0011, ADR-0012
