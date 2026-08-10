# ADR-0017 — Emissão do token de sessão e persistência do refresh token
Data: 2026-08-10 · Status: **aceito** · Autor: Arquiteto de Software Principal (decisão delegada por Frederico em 2026-08-08, sob a continuidade de "você decide" e a aprovação sequencial de T-301)

## Contexto

`API.md` §2 já define o contrato de `POST /v1/auth/session`: a resposta inclui `accessToken`,
`expiresAt` e `refreshToken`; `POST /v1/auth/refresh` (T-303) deve renovar sem login interativo;
`POST /v1/auth/logout` (T-303) deve invalidar. RF-004 exige expiração curta e renovação sem novo
login "enquanto a sessão do usuário for válida". O esquema de segurança do OpenAPI (`API.md` §11)
já declara `bearerFormat: JWT`.

O que **nenhum documento aprovado decide**: como o `accessToken` é assinado e verificado, e — a
lacuna mais concreta, encontrada só ao implementar T-301 — **onde o `refreshToken` é persistido**.
Um JWT autocontido não pode ser revogado antes de expirar; sem algum estado do lado do servidor,
`POST /v1/auth/logout` (RF-006, "logout invalida") não tem o que invalidar. `MODELO-DE-DADOS.md` não
tem tabela para isso — a mesma classe de lacuna que a revisão S008 encontrou entre `API.md` e o
modelo de dados (ADR-0016, Gaps 1 e 2), desta vez encontrada durante a implementação, não durante
uma revisão dedicada.

## Decisão

### 1. `accessToken`: JWT, HMAC-SHA256, chave via variável de ambiente

Assinatura simétrica (HS256), chave lida de `APPBRIDGE_JWT_SIGNING_KEY` — nunca de arquivo (RP-06,
mesmo princípio de `APPBRIDGE_DB_CONNECTION`). Claims: `sub` (`UserAccount.Id`), `tenant_id`, `upn`,
`roles` (array; MVP-0 só tem `"user"` — RF-075/operador do provedor é MVP-1), `jti` (id único do
token), `iat`, `exp`.

`PREMISSA:` TTL de **15 minutos**. RF-004 pede "curta"; 15 min é curto o bastante para limitar o
dano de um token roubado (AM-03) sem gerar renovação perceptível ao usuário dentro de uma jornada
normal de trabalho. Não medido — confirmar no dogfood (mesma natureza de PRE-07, o TTL de 60 s do
`.rdp`).

Assinatura assimétrica (RS256) com rotação de chave fica para quando um segundo serviço precisar
verificar o token de forma independente — não é o caso do MVP-0, Control Plane único.

### 2. `refreshToken`: valor opaco, armazenado só como hash

**Nunca um JWT.** Um refresh token não precisa ser autocontido — sua única função é ser consultado e
validado contra um repositório, e um valor opaco não vaza claim nenhuma se interceptado. Gerado como
256 bits aleatórios, codificado em base64url.

**Armazenado apenas como hash SHA-256** (`refresh_token.token_hash`), nunca o valor bruto — mesma
disciplina de nunca guardar senha em texto claro. Um vazamento do banco não entrega token utilizável.

Nova tabela `refresh_token` (domínio Identidade, `MODELO-DE-DADOS.md` §4.3):

| Coluna | Tipo | Notas |
|--------|------|-------|
| `id` | uuid v7 PK | |
| `tenant_id` | uuid FK | |
| `user_account_id` | uuid | FK composta `(tenant_id, user_account_id)` |
| `token_hash` | text UNIQUE | SHA-256 do valor opaco, nunca o valor em si |
| `expires_at` | timestamptz | |
| `revoked_at` | timestamptz NULL | Logout ou rotação em `/auth/refresh` (T-303) fecham aqui — não é o `deleted_at` de ADR-0011 §3, é um campo de domínio próprio |
| `created_at` | timestamptz | |

`PREMISSA:` TTL de **30 dias**. RF-004: "enquanto a sessão do usuário for válida" — 30 dias é uma
janela generosa mas finita; revisitar no dogfood.

Segue as convenções de ADR-0011 (UUID v7, `timestamptz` UTC, FK composta com `tenant_id`) como toda
tabela de tenant. **Não é tabela de trilha** (não é `AppendOnlyEntity`): `revoked_at` precisa ser
gravável depois da criação, o que uma tabela append-only, por desenho, proíbe.

### 3. Rotação no uso

Cada chamada bem-sucedida a `/auth/refresh` (T-303) emite um novo `refreshToken` e revoga o anterior
(`revoked_at`). Limita o estrago de um refresh token roubado a uma única troca — depois disso, tanto
o dono legítimo quanto o atacante encontram o token revogado, o que é em si um sinal de
comprometimento a ser tratado por T-303.

### 4. Middleware de autenticação registrado agora, sem consumidor ainda

`AddAuthentication().AddJwtBearer(...)` é registrado em `Program.cs` como parte desta tarefa — o
esquema de claims só existe num lugar (aqui), e endpoints futuros (`T-304` em diante) que exigirem
`[Authorize]` reaproveitam a configuração sem redecidir nada. Nenhum endpoint protegido existe ainda
para exercitá-lo de ponta a ponta — mesmo raciocínio de T-201 para o health check: registrar o
mecanismo real cedo, sem inventar um consumidor que ainda não existe.

### 5. Nenhum provedor de identidade real nesta tarefa

`IIdentityProvider` é a interface que valida o token do Entra ID/AD DS e resolve `ExternalSubject` +
o domínio AD do tenant (ADR-0001). **Nenhuma implementação real é construída em T-301** — não há
tenant Entra, domínio AD DS nem hardware ainda (E-01 não tem equipamento comprado; ADR-0001 item 6
permite autenticar direto contra AD DS no MVP-0, mas nem isso existe neste ambiente). Escrever uma
implementação "real" sem nada para validá-la contra violaria a disciplina deste projeto de rodar
para verificar, não só compilar. Um `DevIdentityProvider` existe só em `Development`
(`IHostEnvironment.IsDevelopment()`), claramente nomeado, para permitir rodar e testar o endpoint de
ponta a ponta nesta sessão — não é, e não deve ser confundido com, a integração real.

## Alternativas consideradas

| Item | Alternativa | Por que não |
|---|---|---|
| Refresh token | JWT autocontido, sem tabela | Não revogável antes de expirar — contradiz RF-006 (logout invalida) |
| Refresh token | Guardar o valor bruto | Mesmo risco de senha em texto claro; um vazamento de banco entregaria token pronto para uso |
| `accessToken` | RS256 com par de chaves | Complexidade de gestão de chave sem benefício no MVP-0 (Control Plane único, ninguém mais verifica o token) |
| Implementação de `IIdentityProvider` | Construir a integração real com Entra/AD DS agora | Não verificável nesta sessão (infraestrutura de E-01 não existe); código não testável é código não confiável |

## Consequências

**Positivas**
- Logout e revogação por comprometimento passam a ser reais, não apenas prometidos pelo contrato de `API.md`.
- O esquema de claims fica decidido uma vez, documentado, reaproveitável por toda tarefa futura que precisar de `[Authorize]`.

**Negativas**
- Uma tabela nova e uma migração a mais — pequeno custo de schema.
- `DevIdentityProvider` é superfície de código que existe só para viabilizar teste local e **precisa
  ser removida ou isolada de forma que nunca rode fora de `Development`** — risco de segurança se
  vazar para um ambiente real.

**Riscos**
- Os dois TTLs (`PREMISSA`, item 1 e 2) não são medidos — mesma categoria de risco que PRE-07 já
  carrega para o `.rdp`. Revisitar no dogfood (T-005).
- `DevIdentityProvider` autentica qualquer requisição sem verificação real — **nunca pode ser
  registrado fora de `Development`**; é a mesma classe de risco que um `IgnoreQueryFilters()` mal
  colocado (ADR-0004 item 7), e merece o mesmo cuidado de revisão.

## Requisitos relacionados

RF-001, RF-003, RF-004, RF-005, RF-006 · RP-06 · Origem: lacuna encontrada na implementação de T-301
