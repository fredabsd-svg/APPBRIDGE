# API — Control Plane AppBridge · contrato v0
> Entregável 5 de 7 da fase de Design · Sessão S001 · 2026-08-08
> Status: **submetido — aguardando aprovação de Frederico** (RP-04)
> Depende de: `ARQUITETURA.md`, `MODELO-DE-DADOS.md`, ADR-0012 (convenções da API)

---

## 1. Como ler este documento

- **Convenções transversais estão em ADR-0012** e não se repetem endpoint a endpoint: versionamento
  em `/v1`, erro em Problem Details, idempotência no lançamento, `tenant_id` **nunca** vindo do
  cliente, `404` para recurso de outro tenant, paginação por cursor.
- Cada endpoint traz **fase** e os **requisitos que o justificam** (RA-04). Endpoint sem requisito
  não entra no contrato.
- Base: `https://<control-plane>/v1` — alcançável apenas pela rede interna ou pela malha privada
  (RNF-009, ADR-0003). **Não há publicação na internet.**
- Todo tráfego em TLS 1.2+ (RNF-003).

### 1.1 Cabeçalhos comuns

| Cabeçalho | Direção | Uso |
|-----------|---------|-----|
| `Authorization: Bearer <token>` | requisição | Token de sessão do Control Plane (RF-004) |
| `X-Correlation-Id` | ambas | Correlação ponta a ponta; gerado pelo cliente ou pelo servidor (RNF-039) |
| `X-AppBridge-Client` | requisição | Versão do launcher (ADR-0012 §1) |
| `Idempotency-Key` | requisição | Obrigatório em `POST /launches` (ADR-0012 §3) |
| `X-AppBridge-Acting-Tenant` | requisição | **Somente operador do provedor**; toda requisição com ele é auditada (RF-075) |
| `ETag` / `If-None-Match` | ambas | Sincronização barata do catálogo (RF-015) |
| `Retry-After` | resposta | Acompanha `429` e `503` (RNF-010) |

---

## 2. Autenticação e sessão

O launcher autentica no provedor de identidade e **troca** aquele token por um token do Control Plane.
Os dois não se confundem — e nenhum dos dois é a credencial que abre a sessão RDS (ADR-0001, ADR-0010).

### `POST /v1/auth/session` — MVP-0 · RF-001, RF-003, RF-036

Troca o token do provedor de identidade por um token de sessão do AppBridge.

```jsonc
// requisição
{ "identityToken": "eyJ...", "workstationName": "PC-CONTABIL-07" }

// 201 Created
{
  "accessToken": "eyJ...",
  "expiresAt": "2026-08-08T14:30:00Z",
  "refreshToken": "...",
  "user": {
    "id": "018f...", "displayName": "Ana Souza",
    "tenant": { "id": "018f...", "name": "Escritório Modelo" },
    "roles": ["user"]
  }
}
```

> **A tentativa de autenticação é registrada antes de a resposta sair, na mesma transação**
> (ADR-0007). Se a trilha não gravar, o login não acontece — e a resposta é `503`, não `500`, porque
> o problema é de dependência e é transitório.

| Erro | Código | Situação |
|------|--------|----------|
| `401` | `INVALID_IDENTITY_TOKEN` | Token do provedor inválido ou expirado |
| `403` | `USER_DISABLED` | Conta desabilitada no diretório ou no AppBridge |
| `403` | `TENANT_SUSPENDED` | Tenant suspenso |
| `503` | `AUDIT_UNAVAILABLE` | Trilha indisponível — login negado por decisão (ADR-0007) |

### `POST /v1/auth/refresh` — MVP-0 · RF-004
Renova o token sem login interativo. `401 REFRESH_EXPIRED` obriga novo login.

### `POST /v1/auth/logout` — MVP-0 · RF-006
Invalida o token e o refresh. **Não encerra sessões RDS abertas** — ver §4.4 e R-014.

### `GET /v1/me` — MVP-0 · RF-001
Identidade, tenant, papéis e políticas efetivas do usuário.

---

## 3. Catálogo

### `GET /v1/applications` — MVP-0 · RF-011, RF-013, RF-015

Retorna **apenas** os aplicativos autorizados ao usuário. Aplicativo não autorizado não aparece, não
é contado e não é referenciável (RF-011).

```jsonc
// 200 OK   ETag: "cat-018f3a92"
{
  "items": [
    {
      "id": "018f...",
      "displayName": "Domínio Contábil",
      "description": "Escrita fiscal e contabilidade",
      "iconUrl": "/v1/applications/018f.../icon",
      "launchMode": "remote_app",
      "protocolUri": "appbridge://launch/018f...",
      "available": true
    }
  ],
  "nextCursor": null
}
```

Com `If-None-Match` e catálogo inalterado, responde `304 Not Modified` sem corpo — a sincronização
periódica de RF-015 custa quase nada.

> `available: false` indica aplicativo temporariamente indisponível (host em manutenção). É diferente
> de ausente: o atalho continua existindo e o usuário recebe explicação em vez de erro genérico.

### `GET /v1/applications/{id}/icon` — MVP-0 · RF-013 · **resolve PD-03**

Devolve o binário do ícone (PNG), com `ETag` e `Cache-Control` longo. **Decisão:** o ícone é servido
pelo Control Plane a partir de armazenamento de arquivos, referenciado por `icon_ref`; não fica no
banco. Binário em coluna infla backup e replicação do PostgreSQL sem benefício, e ícone é o tipo de
conteúdo que a camada HTTP já sabe cachear bem.

### `GET /v1/applications/{id}` — MVP-0 · RF-011
Detalhe. Aplicativo de outro tenant, ou não autorizado ao usuário: **`404`** (ADR-0012 §5).

---

## 4. Lançamento — o caminho crítico

### `POST /v1/launches` — MVP-0 · RF-018..RF-021, RF-025, RF-037, RF-039

O endpoint mais importante da API. Autoriza, monta, assina, registra e devolve.

```jsonc
// requisição            Idempotency-Key: 018f3a...
{
  "applicationId": "018f...",
  "purpose": "user_initiated",     // ou "prelaunch" (RF-023)
  "workstationName": "PC-CONTABIL-07"
}

// 201 Created
{
  "launchId": "018f...",
  "sessionReused": true,
  "rdpFile": "<base64 do .rdp assinado>",
  "expiresAt": "2026-08-08T13:45:60Z",
  "host": { "displayName": "Servidor de aplicativos" },
  "correlationId": "018f..."
}
```

**Decisões de contrato que este endpoint materializa:**

1. **O cliente não escolhe nada.** Não envia host, não envia parâmetros de conexão, não envia política
   de redirecionamento. Tudo é resolvido no servidor (RF-021, ADR-0008). O único dado do cliente é
   qual aplicativo e de qual estação.
2. **`purpose` separa prelaunch de lançamento real** na trilha. Sem essa distinção, a auditoria
   mostraria o usuário "abrindo o SessionPrimer" toda manhã, e a contagem de uso por aplicativo
   ficaria poluída.
3. **`rdpFile` vem em base64**, não como corpo `text/plain`, para evitar ambiguidade de codificação —
   o `.rdp` assinado é sensível a byte, e uma conversão de encoding invalida a assinatura.
4. **`expiresAt` é contratual, não informativo**: o launcher grava, executa e apaga (RF-020). O
   servidor recusa reapresentação da mesma `Idempotency-Key` depois do vencimento.

| Erro | Código | Situação | Requisito |
|------|--------|----------|-----------|
| `403` | `PERMISSION_REVOKED` | Permissão não vigente — **registrado como `denied_permission`** | RF-007, RF-039 |
| `404` | `APPLICATION_NOT_FOUND` | Inexistente ou de outro tenant | ADR-0012 §5 |
| `409` | `QUOTA_EXHAUSTED` | Teto de licença atingido (MVP-1) | RF-064, ADR-0006 |
| `409` | `IDEMPOTENCY_CONFLICT` | Mesma chave, corpo diferente | ADR-0012 §3 |
| `422` | `APPLICATION_UNAVAILABLE` | Sem host disponível ou host em drenagem | RF-025 |
| `429` | `RATE_LIMITED` | Excesso de tentativas | RNF-010 |
| `503` | `SIGNING_UNAVAILABLE` | `rdpsign` falhou — **nenhum `.rdp` sai sem assinatura** | RNF-002, ADR-0009 |
| `503` | `AUDIT_UNAVAILABLE` | Trilha indisponível — lançamento negado | ADR-0007 |

> **Os dois `503` são o desenho funcionando, não falha de projeto.** São os únicos caminhos possíveis
> quando assinar ou registrar não é possível: o contrato **não oferece** degradação para "entregar sem
> assinar" ou "conceder sem registrar".

### `GET /v1/sessions/me` — MVP-0 · RF-024, RF-027
Sessões ativas do próprio usuário, para o launcher indicar estado e apoiar a reconexão.

### `DELETE /v1/sessions/{id}` — **MVP-1** · RF-008, RF-045

Encerramento forçado, pelo administrador ou por revogação.

> **Lacuna do MVP-0, declarada no contrato:** este endpoint **não existe no MVP-0**. Enquanto ele não
> existir, revogar acesso não encerra sessão aberta (R-014), e a resposta operacional é desabilitar a
> conta no AD e encerrar a sessão manualmente no host. Está escrito aqui para que a ausência seja
> visível a quem lê a API, e não descoberta no dia de uma demissão.

### O cliente não é fonte da verdade sobre fim de sessão

**Não existe endpoint para o launcher informar que a sessão terminou.** É deliberado: se o encerramento
dependesse do cliente, bastaria uma máquina desligada na tomada para a sessão ficar "ativa" para
sempre, inflando a contagem de licenças (R-009). A verdade vem da reconciliação com o Connection
Broker, no servidor (`ISessionBackend`, ARQUITETURA §4.2).

---

## 5. Trilha de auditoria

### `GET /v1/audit/launches` — MVP-0 (leitura) · RF-040 · MVP-1 (filtros completos) · RF-046

Filtros: `from`, `to`, `userId`, `applicationId`, `outcome`. Paginação por cursor.

```jsonc
// 200 OK
{
  "items": [{
    "launchId": "018f...", "requestedAt": "2026-08-08T11:02:31Z",
    "user": { "id": "018f...", "displayName": "Ana Souza" },
    "application": { "id": "018f...", "displayName": "Domínio Contábil" },
    "outcome": "denied_permission",
    "sourceIp": "10.20.0.34", "workstationName": "PC-CONTABIL-07",
    "correlationId": "018f..."
  }],
  "nextCursor": "eyJ0cyI6..."
}
```

### `GET /v1/audit/access-events` — MVP-0 · RF-036, RF-038, RNF-015
### `GET /v1/audit/admin-events` — MVP-1 · RF-041, RNF-017
Inclui `beforeState`/`afterState` e o campo `actingAsProvider` (ADR-0004 item 7).

### `GET /v1/audit/certificate-usage` — V2 · RF-042, RNF-016

### Exportação — MVP-1 · RNF-021
Os mesmos endpoints com `Accept: text/csv` devolvem CSV para entrega em auditoria.

> **Consultar a trilha é ação auditável.** Toda leitura destes endpoints gera registro em
> `admin_audit_event` (RNF-024) — quem consultou a trilha de quem. Auditoria que não se audita é meia
> auditoria.

---

## 6. Administração — MVP-1

Todas sob `/v1/admin/...`, exigem papel administrativo e **geram trilha administrativa** (RF-041).

| Método | Rota | Função | Requisito |
|--------|------|--------|-----------|
| `GET` `POST` | `/admin/applications` | Listar e publicar | RF-043 |
| `PATCH` `DELETE` | `/admin/applications/{id}` | Alterar e despublicar (lógico) | RF-043 |
| `GET` `POST` | `/admin/applications/{id}/permissions` | Conceder a grupo | RF-010, RF-044 |
| `DELETE` | `/admin/applications/{id}/permissions/{pid}` | **Revogar = fechar vigência**, não apagar | RF-007, ADR-0011 |
| `GET` `POST` | `/admin/groups`, `/admin/groups/{id}/members` | Grupos e membros | RF-044 |
| `GET` `PATCH` | `/admin/users`, `/admin/users/{id}` | Listar, desabilitar | RF-044 |
| `GET` | `/admin/sessions` | Sessões ativas | RF-045 |
| `GET` `PUT` | `/admin/policies/redirection` | Política de redirecionamento | RNF-014, ADR-0008 |
| `GET` `PUT` | `/admin/policies/retention` | Retenção por categoria | RNF-018, ADR-0007 |
| `GET` | `/admin/hosts` | Hosts e estado (V2) | RF-047 |

**Regras de contrato específicas:**

- `DELETE` em permissão **não apaga linha**: grava `effective_to` e exige `revocationReason`. Uma API
  que "apaga" permissão convida a implementação a apagar de fato, e a trilha perderia o histórico
  que RF-007 precisa.
- `PUT /admin/policies/retention` abaixo do mínimo da categoria devolve **`422 RETENTION_BELOW_MINIMUM`**.
  O mínimo é do banco (`MODELO-DE-DADOS` §3.2) e da API — protege o provedor de instrução equivocada
  de cliente (ADR-0007 item 4).
- `PUT /admin/policies/redirection` divergindo do padrão do tenant **exige `exceptionReason`**;
  ausente, `422 EXCEPTION_REASON_REQUIRED` (ADR-0008 condição 2).

### Metering — MVP-1 · ADR-0006

| Método | Rota | Função | Requisito |
|--------|------|--------|-----------|
| `GET` | `/admin/applications/{id}/usage` | Uso simultâneo atual × teto | RF-062 |
| `PUT` | `/admin/applications/{id}/usage/limit` | Definir teto | RF-063 |
| `POST` | `/admin/applications/{id}/usage/reconcile` | **Forçar reconciliação e destravar contagem** | R-009, ADR-0006 |

> O último existe por causa de R-009: se a contagem inflar, o administrador precisa de um caminho para
> destravar **sem esperar pelo ciclo automático** — senão o produto fica bloqueando trabalho legítimo e
> ninguém consegue intervir. A ação é registrada na trilha administrativa.

---

## 7. Agent — V2

| Método | Rota | Função | Requisito |
|--------|------|--------|-----------|
| `POST` | `/v1/agents/enroll` | Registro do host, com credencial própria e revogável | RF-053 |
| `WSS` | `/v1/agents/connect` | Canal **outbound** persistente: telemetria e comandos | RF-050..RF-052 |
| `DELETE` | `/v1/admin/agents/{id}` | Revogar credencial de um host | RF-053 |

O canal é sempre iniciado pelo Agent. **O Control Plane nunca abre conexão para o host** — é o que
sustenta "sem abrir portas" (RF-050, RNF-001).

## 8. Cofre de certificados — V2 (esboço)

| Método | Rota | Requisito |
|--------|------|-----------|
| `POST` `GET` | `/v1/vault/certificates` | RF-055 |
| `POST` | `/v1/vault/custody-terms` | RF-056 |
| `POST` `GET` | `/v1/vault/usage-policies` | RF-057 |
| `DELETE` | `/v1/vault/certificates/{id}` | Revogação imediata · RF-059 |

**Regra que já vale para o contrato:** nenhum endpoint devolve o PFX nem sua senha, em nenhuma
circunstância, para nenhum papel — nem para o operador do provedor. O material entra no cofre e só sai
injetado na sessão (RF-058). Uma API que oferece download do certificado não é um cofre.

---

## 9. Catálogo de erros

`appbridgeCode` é a chave estável; o texto pode mudar sem quebrar o cliente (ADR-0012 §2).

| HTTP | `appbridgeCode` | Significado | Ação esperada do launcher |
|------|-----------------|-------------|---------------------------|
| 400 | `MALFORMED_REQUEST` | Corpo inválido | Erro de programação; reportar diagnóstico |
| 401 | `INVALID_IDENTITY_TOKEN` | Token do provedor inválido | Refazer login |
| 401 | `SESSION_EXPIRED` | Token do AppBridge expirado | Renovar; se falhar, login |
| 401 | `REFRESH_EXPIRED` | Refresh expirado | Login interativo |
| 403 | `PERMISSION_REVOKED` | Sem permissão vigente | Mensagem clara + remover atalho (RF-032) |
| 403 | `USER_DISABLED` | Conta desabilitada | Mensagem + logout local |
| 403 | `TENANT_SUSPENDED` | Tenant suspenso | Mensagem; não repetir |
| 403 | `PROVIDER_ROLE_REQUIRED` | Cabeçalho de travessia sem papel — **registrado** | Erro de programação |
| 404 | `APPLICATION_NOT_FOUND` | Inexistente ou de outro tenant | Sincronizar catálogo |
| 409 | `QUOTA_EXHAUSTED` | Licenças em uso | Mensagem + oferecer nova tentativa |
| 409 | `IDEMPOTENCY_CONFLICT` | Chave reusada com corpo diferente | Erro de programação |
| 422 | `APPLICATION_UNAVAILABLE` | Sem host disponível | Mensagem + nova tentativa depois |
| 422 | `RETENTION_BELOW_MINIMUM` | Retenção abaixo do mínimo | Painel exibe o mínimo |
| 422 | `EXCEPTION_REASON_REQUIRED` | Exceção de política sem justificativa | Painel exige o campo |
| 429 | `RATE_LIMITED` | Limite de taxa | Respeitar `Retry-After` |
| 500 | `INTERNAL_ERROR` | Falha inesperada | Diagnóstico com `correlationId` |
| 503 | `SIGNING_UNAVAILABLE` | Assinatura falhou | Nova tentativa; alertar operação |
| 503 | `AUDIT_UNAVAILABLE` | Trilha indisponível | Nova tentativa; **alerta operacional imediato** |
| 503 | `DIRECTORY_UNAVAILABLE` | Diretório fora do ar | Nova tentativa |

**Nenhuma resposta de erro contém** nome de host, caminho de arquivo, consulta SQL, exceção ou versão
de componente (RNF-043). O que o suporte precisa está no `correlationId`.

---

## 10. Esqueleto OpenAPI

```yaml
openapi: 3.1.0
info:
  title: AppBridge Control Plane API
  version: "0.1.0"
servers:
  - url: https://{controlPlane}/v1
components:
  securitySchemes:
    bearerAuth: { type: http, scheme: bearer, bearerFormat: JWT }
  headers:
    IdempotencyKey:
      description: Chave de idempotência por clique do usuário (ADR-0012 §3)
      schema: { type: string, format: uuid }
  schemas:
    Problem:
      type: object
      required: [type, title, status, appbridgeCode]
      properties:
        type: { type: string, description: "URN, nunca URL" }
        title: { type: string, description: "Texto ao usuário, pt-BR" }
        status: { type: integer }
        detail: { type: string }
        instance: { type: string }
        correlationId: { type: string, format: uuid }
        appbridgeCode: { type: string, description: "Chave estável de decisão do cliente" }
security: [{ bearerAuth: [] }]
paths:
  /launches:
    post:
      summary: Autoriza, gera e assina o .rdp de um lançamento
      description: RF-018..RF-021, RF-037, RF-039 · ADR-0007, ADR-0008, ADR-0009
      parameters:
        - in: header
          name: Idempotency-Key
          required: true
          schema: { type: string, format: uuid }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [applicationId, purpose, workstationName]
              properties:
                applicationId: { type: string, format: uuid }
                purpose: { type: string, enum: [user_initiated, prelaunch] }
                workstationName: { type: string }
      responses:
        "201":
          description: Lançamento autorizado
          content:
            application/json:
              schema:
                type: object
                required: [launchId, rdpFile, expiresAt]
                properties:
                  launchId: { type: string, format: uuid }
                  sessionReused: { type: boolean }
                  rdpFile: { type: string, format: byte }
                  expiresAt: { type: string, format: date-time }
        "403": { $ref: "#/components/responses/Problem" }
        "409": { $ref: "#/components/responses/Problem" }
        "503":
          description: SIGNING_UNAVAILABLE ou AUDIT_UNAVAILABLE — sem degradação silenciosa
```

---

## 11. Rastreabilidade — endpoint → requisito

| Endpoint | Requisitos | Fase |
|----------|-----------|------|
| `POST /auth/session` | RF-001, RF-003, RF-036 | MVP-0 |
| `POST /auth/refresh` | RF-004 | MVP-0 |
| `POST /auth/logout` | RF-006 | MVP-0 |
| `GET /me` | RF-001 | MVP-0 |
| `GET /applications` | RF-011, RF-013, RF-015 | MVP-0 |
| `GET /applications/{id}/icon` | RF-013 (**PD-03**) | MVP-0 |
| `POST /launches` | RF-018..RF-021, RF-023, RF-025, RF-037, RF-039 | MVP-0 |
| `GET /sessions/me` | RF-024, RF-027 | MVP-0 |
| `GET /audit/launches`, `/audit/access-events` | RF-040, RF-036, RF-038, RNF-015 | MVP-0 |
| `DELETE /sessions/{id}` | RF-008, RF-045 | **MVP-1** |
| `/admin/applications*`, `/admin/groups*`, `/admin/users*` | RF-043, RF-044, RF-010 | MVP-1 |
| `/admin/policies/*` | RNF-014, RNF-018 | MVP-1 |
| `/admin/applications/{id}/usage*` | RF-062..RF-064 | MVP-1 |
| `GET /audit/admin-events` + exportação | RF-041, RF-046, RNF-017, RNF-021, RNF-024 | MVP-1 |
| `/agents/*` | RF-050..RF-054 | V2 |
| `/vault/*`, `/audit/certificate-usage` | RF-055..RF-061, RF-042 | V2 |

**Requisitos de MVP-0 sem endpoint** — verificado item a item: RF-002, RF-005, RF-014, RF-020,
RF-022, RF-026, RF-029..RF-034 são comportamento do launcher ou do servidor, sem superfície de API;
RF-019 e RF-021 são internos ao `POST /launches`; RF-012 é seed, sem endpoint no MVP-0.

---

## 12. Pendências

| ID | Item | Situação |
|----|------|----------|
| **PD-03** | Armazenamento de ícones | ✅ **Resolvida** — arquivo referenciado por `icon_ref`, servido por `GET /applications/{id}/icon` com `ETag` |
| **PD-04** | Armazenamento das respostas de idempotência (memória, tabela ou cache) por 60 s | Aberta — decisão de implementação |
| **PD-05** | Limites concretos de taxa por endpoint (RNF-010) | Aberta — depende de medição (T-005) |
