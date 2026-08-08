# ADR-0012 — Convenções da API: versionamento, formato de erro, idempotência e escopo de tenant
Data: 2026-08-08 · Status: **aceito** · Autor: Arquiteto de Software Principal (decisão delegada por Frederico em 2026-08-08)

## Contexto

`API.md` vai definir o contrato v0 do Control Plane. Quatro decisões se repetem em todo endpoint e
precisam ser tomadas uma vez:

1. **Versionamento** — como evoluir o contrato sem quebrar launchers já instalados;
2. **Formato de erro** — como o launcher distingue "sem permissão" de "licença esgotada" de "não
   consegui assinar", já que RF-025 exige mensagem acionável e RNF-043 proíbe vazar detalhe interno;
3. **Idempotência** — porque o lançamento **não é idempotente** e uma repetição de requisição (rede
   instável, usuário clicando duas vezes) infla a contagem de licenças, que é exatamente o risco R-009;
4. **De onde vem o `tenant_id`** — a decisão mais importante das quatro.

Sobre o item 4: ADR-0004 exige isolamento por mecanismo. Se o `tenant_id` chegar como parâmetro de
rota ou de corpo, o isolamento passa a depender de o servidor conferir se aquele tenant é mesmo o do
chamador — verificação que precisa ser repetida em todo endpoint e que **um dia será esquecida em um
deles**. É a mesma classe de erro que a FK composta do ADR-0011 fecha no banco.

## Decisão

### 1. Versionamento no caminho: `/v1/...`

Versão maior no caminho da URL. Mudança compatível (novo campo opcional, novo endpoint) não muda a
versão; mudança incompatível cria `/v2` e **exige ADR**, com convivência das duas versões enquanto
houver launcher instalado na anterior (RF-035 automatiza a atualização do cliente, mas não a garante
imediata).

O launcher envia sua própria versão em `X-AppBridge-Client`, para que o servidor possa registrar e,
no futuro, recusar clientes abaixo de um mínimo.

### 2. Erro no formato Problem Details (RFC 9457)

Toda resposta de erro é `application/problem+json`:

```json
{
  "type": "urn:appbridge:problem:quota-exhausted",
  "title": "Todas as licenças deste aplicativo estão em uso",
  "status": 409,
  "detail": "O limite de 12 usos simultâneos do Domínio Contábil foi atingido.",
  "instance": "/v1/launches",
  "correlationId": "018f3a...",
  "appbridgeCode": "QUOTA_EXHAUSTED"
}
```

Regras que fazem parte da decisão:

- `type` usa **URN**, não URL. Um `type` apontando para um domínio na internet criaria a expectativa
  de resolvê-lo — e o produto opera em rede sem internet (ADR-0003).
- `title` e `detail` são **texto para o usuário final, em português** (RNF-043): sem nome de host,
  sem caminho de arquivo, sem exceção, sem SQL. O que o suporte precisa está no `correlationId`
  (RNF-039), que amarra a resposta ao log estruturado e à trilha.
- `appbridgeCode` é a chave estável para o cliente decidir comportamento. **O cliente nunca faz
  ramificação por texto de mensagem** — texto muda, código não.

### 3. Idempotência no lançamento

`POST /v1/launches` aceita o cabeçalho **`Idempotency-Key`** (UUID gerado pelo launcher por clique do
usuário). Repetição da mesma chave dentro da janela de validade do `.rdp` (60 s, PRE-07) devolve **a
mesma resposta**, sem criar segundo registro de lançamento nem segunda sessão.

Isso não é refinamento: sem ele, um duplo clique ou uma retentativa de rede vira duas linhas em
`launch`, duas contagens de licença e, com teto configurado, um bloqueio provocado pelo próprio
produto (R-009, RF-064).

Endpoints de escrita administrativa (`POST`/`PUT`/`PATCH` em `/v1/admin/...`) também aceitam a chave.
`GET` e `DELETE` já são idempotentes por natureza.

### 4. O `tenant_id` nunca vem do cliente

**Nenhuma rota de dados de tenant contém `tenant_id`, e nenhum corpo de requisição o aceita.** O
tenant é derivado exclusivamente do token, no `TenantContext` (ADR-0004). Um campo `tenantId` enviado
pelo cliente é **ignorado**, não é erro — ignorar evita que a mensagem de erro confirme a existência
de outro tenant.

A única exceção é o **operador do provedor** (RF-075), que atravessa tenants:

- usa o cabeçalho `X-AppBridge-Acting-Tenant`;
- só é aceito se o token carregar o papel de operador do provedor;
- **toda requisição com esse cabeçalho gera registro em `admin_audit_event` com
  `acting_as_provider = true`** (ADR-0004 item 7), inclusive as de leitura;
- token sem o papel que envie o cabeçalho recebe `403`, e a tentativa é registrada.

### 5. Recurso de outro tenant responde `404`, não `403`

Requisitar um recurso que existe mas pertence a outro tenant devolve **`404 Not Found`**. `403`
confirmaria a existência do recurso, o que é vazamento de informação por si só.

**Distinção importante:** um aplicativo que **está no catálogo do usuário** mas cuja permissão foi
revogada devolve `403` no lançamento — ali o usuário já sabe que o aplicativo existe, e RF-025 exige
mensagem acionável ("seu acesso foi removido"), não um `404` enigmático.

### 6. Paginação por cursor

Listagens usam `?limit=&cursor=` e devolvem `nextCursor`. Deslocamento numérico (`offset`) é recusado:
degrada em tabelas grandes como `launch` e produz resultado inconsistente quando há inserção
concorrente — que é o caso permanente de uma trilha de auditoria.

## Alternativas consideradas

| Item | Alternativa | Por que não |
|------|-------------|-------------|
| Versão | Cabeçalho `Accept` com versão de mídia | Mais elegante e menos visível; dificulta diagnóstico por log e por ferramenta de linha de comando. |
| Versão | Sem versionamento, só evolução compatível | Boa intenção que não sobrevive ao primeiro requisito incompatível. |
| Erro | Formato próprio `{ erro, mensagem }` | Reinventa um padrão existente que o ASP.NET Core já produz nativamente, e perde interoperabilidade. |
| Erro | Devolver a exceção em ambiente interno "porque é dogfood" | Vira permanente. RNF-043 não tem exceção por ambiente. |
| Idempotência | Deduplicar por `(usuário, aplicativo, janela de tempo)` | Adivinha a intenção: dois lançamentos legítimos e rápidos do mesmo aplicativo seriam colapsados em um. A chave explícita não adivinha. |
| Tenant | `tenant_id` na rota, com verificação no servidor | É o desenho mais comum e a origem mais comum de vazamento entre tenants. Recusado pelo mesmo motivo do ADR-0011 §4. |
| Tenant | Subdomínio por tenant (`escritorio.appbridge...`) | Interessante para o Caminho A, mas exige DNS e certificado por tenant, o que contraria a operação on-premises simples do MVP-0 (RNF-037). Pode voltar como evolução. |
| Paginação | `offset`/`page` | Simples e errado em tabela que cresce durante a leitura. |

## Consequências

**Positivas**
- O isolamento de tenant deixa de depender de verificação repetida endpoint a endpoint: **não existe
  parâmetro para verificar**.
- O launcher trata erros por código estável, e o texto pode ser reescrito sem quebrar cliente.
- O lançamento fica seguro contra retentativa, protegendo a contagem de licenças.

**Negativas**
- O operador do provedor precisa de um mecanismo próprio (cabeçalho + papel + registro), que é
  código a mais e superfície a mais para revisar.
- Idempotência exige guardar a resposta associada à chave por 60 s, com armazenamento e expiração
  próprios.
- Paginação por cursor é mais trabalhosa de implementar e de consumir que `offset`.

**Riscos**
- **O cabeçalho `X-AppBridge-Acting-Tenant` é o ponto mais sensível de toda a API.** Uma falha na
  verificação do papel transforma o mecanismo de suporte multiempresa em porta de travessia de
  tenant. Deve ter teste dedicado de negativa (ADR-0004 item 9) e revisão de código específica.
- `404` para recurso de outro tenant dificulta o diagnóstico de erro legítimo de configuração — o
  suporte verá "não encontrado" onde há problema de vínculo. Mitigação: o log estruturado registra a
  causa real, ainda que a resposta não a revele.

## Requisitos relacionados

RF-011, RF-018, RF-021, RF-025, RF-039, RF-064, RF-075 · RNF-010, RNF-039, RNF-043, RNF-036 ·
ADR-0004, ADR-0006, ADR-0007, ADR-0011 · Origem: §7 prompt (entregável 5), RA-04
