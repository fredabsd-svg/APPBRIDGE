> ⚠️ **ANEXO HISTÓRICO — NÃO É FONTE DE TAREFAS.**
> Por **ADR-0016**, o backlog único do projeto é `ROADMAP.md`, que tem rastreabilidade
> requisito↔tarefa e cobre o épico de infraestrutura, ausente aqui. O que este documento trouxe de
> novo — os dois gaps arquiteturais e a antecipação da varredura de segredos — foi incorporado ao
> `ROADMAP.md` como T-207, T-506 e T-1106. Mantido como registro do trabalho e das decisões de
> priorização. Ver `operacao/revisao-issues-e-backlog-paralelo.md`.

# 📌 BACKLOG MVP-0a — Priorizado por Caminho Crítico
**Data:** 2026-08-09  
**Versão:** 1.0  
**Baseado em:** ANALISE_BUGS_E_MELHORIAS.md + ROADMAP.md + STATUS.md

---

## 🎯 FILOSOFIA DE PRIORIZAÇÃO

**Regra de ouro:** *O que não é código vem primeiro* (STATUS.md §3, R-023).

Sequência deliberada:
1. **Decisões arquiteturais** (ADRs, reunião com Frederico)
2. **Setup inicial** (repo, segredos, CI/CD, health check)
3. **Testes de viabilidade** (R-009, R-013 — medições cedo)
4. **Gaps críticos** (prelaunch rastreável, cancelamento de sessão)
5. **Features do MVP-0a** (catálogo, login, RDP assinado, atalhos)
6. **Oportunidades** (heartbeat proativo, reconciliação manual)

---

## 📊 RESUMO EXECUTIVO

| Phase | Épico | Pontos | Deps | Crítico |
|-------|-------|--------|------|---------|
| **Setup** | E-00 · Infraestrutura + decisões | 0 | Frederico | 🔴 Bloqueia tudo |
| **Validação** | E-01a · Medições críticas (R-009, R-013) | 8 | E-00 | 🔴 Bloqueia Features |
| **Fundação** | E-02 · Control Plane · fundações | 21 | E-01a | 🟠 Caminho crítico |
| **Gaps** | E-03 · Closures arquiteturais | 12 | E-02 | 🟠 Antes de Features |
| **Features** | E-04 · Catálogo + login + RDP | 34 | E-02, E-03 | 🟢 Core MVP-0a |
| **Launch** | E-05 · Atalhos + UI + testes de aceitação | 21 | E-04 | 🟢 Final MVP-0a |
| **Oportunidades** | E-06 · Melhorias rápidas | 8 | Paralelo | 🟢 Nice-to-have |

**Total MVP-0a:** ~96 pontos (estimativa ~12 semanas @ 8 pts/semana, solo)

---

## FASE 0: SETUP E DECISÕES

### ⚡ E-00 · Infraestrutura + Decisões Arquiteturais

**Objetivo:** Desbloquear implementação  
**Dependências:** Reunião com Frederico  
**Duração:** 1 semana (antes de qualquer commit de código)

#### T-00.1 · Reunião de Alinhamento (R-020, B-006, B-007)
**Prioridade:** 🔴 Bloqueador  
**Responsável:** Frederico + Tech Lead  
**Duração:** 2 h (1 reunião)  
**Decisões necessárias:**
- [ ] R-020 (cofre): que tipo de proteção técnica além de contrato? (segunda aprovação, escrow, HSM?)
- [ ] B-006 (PS-07): qual caminho de mitigação?
- [ ] B-007 (PS-03): encadeamento Merkle tree ou carimbo de tempo?
- [ ] PRE-05 (custo): cotação SPLA concluída? Viável R$ 50/usuário/mês?

**Saída esperada:** ADR-0015 (encadeamento criptográfico), decisão sobre cofre, go/no-go para MVP-0a

---

#### T-00.2 · Criar Repositório de Código
**Prioridade:** 🟠 Alta  
**Responsável:** DevOps  
**Duração:** 2 dias  
**Checklist:**
- [ ] Clonar template ASP.NET Core + Entity Framework
- [ ] Estrutura de pastas:
  ```
  /src
    /AppBridge.ControlPlane
      /Core (domínio)
      /Application (casos de uso)
      /Infrastructure (DB, AD, signing)
      /Api (endpoints)
    /AppBridge.Launcher (scaffolding .NET 10 WinUI 3)
    /AppBridge.Agent (scaffolding .NET Windows Service)
  /tests
    /AppBridge.ControlPlane.Tests
    /AppBridge.ControlPlane.Integration
  /docs (symlink para /home/user/APPBRIDGE/docs)
  ```
- [ ] `.gitignore` com: `*.pfx`, `.env`, `secrets.json`, `user-secrets/`
- [ ] GitHub Actions skeleton: build, test, lint (sem deploy ainda)

**Saída esperada:** Repo pronto, CI/CD básico, primeiro commit

---

#### T-00.3 · Implementar Vault de Segredos Locais
**Prioridade:** 🟠 Alta  
**Responsável:** Backend lead  
**Duração:** 1 dia  
**Checklist:**
- [ ] Configurar `dotnet user-secrets` por ambiente (Development/Staging/Production)
- [ ] Chaves necessárias no MVP-0a:
  - `AdminAccount:Username` (conta que assina RDP)
  - `Database:ConnectionString` (PostgreSQL)
  - `AzureAd:TenantId`, `AzureAd:ClientId`, `AzureAd:ClientSecret`
  - `RdpSigning:CertificatePath` (path local, dev only)
  - `RdpSigning:CertificatePassword` (será mínimo 64 chars aleatórios)
- [ ] Documentar em `docs/SETUP-DEV.md` como carregar secrets

**Saída esperada:** Nenhum secret em `.appsettings.json` ou código

---

#### T-00.4 · Configurar Varredura de Segredos Automática (PS-04)
**Prioridade:** 🟠 Alta  
**Responsável:** DevOps  
**Duração:** 4 h  
**Checklist:**
- [ ] GitHub Action com `detect-secrets` + `git-secrets`
- [ ] `.secrets.baseline` criado (inicialmente vazio)
- [ ] Bloquear PR se segredo detectado (com exceções configuráveis)
- [ ] Documentar em `SEGURANCA.md` §7 (PS-04)

**Saída esperada:** GitHub Actions rodando em cada PR, nenhum commit com secret escapa

---

#### T-00.5 · Criar ADR-0015 (Encadeamento Criptográfico da Trilha)
**Prioridade:** 🟠 Alta  
**Responsável:** Arquiteto + Frederico  
**Duração:** 3 dias  
**Conteúdo mínimo:**
- Contexto: R-021, AM-12, PS-03
- Alternativas:
  - Merkle tree incremental (`hash(H(prev) || row)`)
  - HCPA (Helger Lipmaa's log structure)
  - Carimbo de tempo independente (RFC 3161)
  - Assinatura batch com terceiro (caro)
- Decisão: qual implementar e em que fase (MVP-0a stub vs. MVP-1 real)
- Consequências: custo de CPU, espaço de armazenamento, operação
- Requisitos: RF-020 (assinatura), RNF-022 (verificabilidade)

**Saída esperada:** ADR-0015 aceito, caminho de implementação claro

---

### 📋 Critério de Conclusão de E-00
- [ ] Reunião realizada, decisões documentadas
- [ ] ADRs novos aceitos (se houver)
- [ ] Repo criado, CI/CD rodando
- [ ] Zero secrets em código
- [ ] Docentes setup confirmado: `git clone`, `dotnet user-secrets list`, `dotnet build` rodam sem erro

**Bloqueador?** SIM · Nada de E-01 até E-00 concluído.

---

## FASE 1: MEDIÇÕES CRÍTICAS (T-005)

### ⚡ E-01a · Medições de Viabilidade (R-009, R-013)

**Objetivo:** Validar PRE-12, PRE-23 antes de começar features  
**Dependências:** E-00 concluído, dogfood configurado (T-102 a T-106)  
**Duração:** 2 semanas (durante setup da infraestrutura)  
**Paralelo com:** E-02 (não bloqueado por E-01a, mas resultado informa design)

#### T-01a.1 · Medir Confiabilidade do Connection Broker (R-009, PRE-23)
**Prioridade:** 🔴 Crítica  
**Responsável:** QA + Backend  
**Duração:** 5 dias  
**Teste de viabilidade:** Verificar se Connection Broker detecta fim de sessão em < 60 s

**Procedimento:**
```
1. Abrir app A (RDS sessão 1001)
2. Abrir app B (RDS sessão 1001, reconectado)
3. Fechar app A
4. Esperar até Connection Broker reportar fim
5. Cronometrar: Δt = T_broker_report - T_close
6. Repetir 100 vezes, registrar p50/p95/p99
```

**Critério de aceite:**
- [ ] p95 < 60 s (RNF-030)
- [ ] p99 < 90 s (margem de segurança)
- [ ] Nenhuma sessão fica "zumbi" (invisível no broker)

**Resultado esperado:**
- Se passar: Prosseguir com RF-062 como planejado
- Se falhar: Adicionar `SessionReconciler` agressivo com expiração de inatividade < 30 s, abrir risco R-009 como aceito (mitigado parcialmente)

**Artefato:** `docs/auditoria/2026-MM-DD-SNNN-conexao-broker-confiabilidade.md`

---

#### T-01a.2 · Medir Latência de Assinatura RDP (R-013, PRE-12)
**Prioridade:** 🔴 Crítica  
**Responsável:** Backend + QA  
**Duração:** 4 dias  
**Teste de viabilidade:** Verificar se `rdpsign` consegue ≤ 1 s p95

**Procedimento:**
```
1. Invocar rdpsign.exe 100 vezes, sequencial
2. Cronometrar cada invocação
3. Registrar p50/p95/p99
4. Repetir teste com 5 threads paralelos
5. Repetir com 10 threads (simulando 10 apps abertos simultâneos)
```

**Critério de aceite:**
- [ ] p95 ≤ 1 s (PRE-12 sequencial)
- [ ] p95 ≤ 1.5 s com 5 threads (permite 5 apps simultâneos)
- [ ] p95 ≤ 2 s com 10 threads

**Resultado esperado:**
- Se passar: Pool de workers é overkill, chamar sequencial é ok
- Se falhar (p95 > 2 s com 10 threads): Abrir ADR novo, trocar por biblioteca nativa ou pool de processes

**Artefato:** `docs/auditoria/2026-MM-DD-SNNN-rdpsign-latencia.md`

---

#### T-01a.3 · Teste de Revogação Manual (R-014)
**Prioridade:** 🟠 Alta  
**Responsável:** QA + Ops  
**Duração:** 2 dias  
**Teste de viabilidade:** Procedimento operacional de demissão funciona?

**Procedimento:**
```
1. Usuário "demo-user" abre Domínio (sessão RDS 2001)
2. Admin revoga acesso via (stub de) AppBridge
3. AppBridge nega novo lançamento
4. Admin abre RDS Console no host, encontra sessão 2001
5. Admin encerra sessão
6. Verifica que evento 4779 aparece em eventvwr
7. Verify: arquivo aberto em C:\user foi fechado sem corrupção
```

**Critério de aceite:**
- [ ] Admin consegue localizar sessão em ≤ 1 minuto
- [ ] Encerramento não causa corrupção de arquivo aberto
- [ ] Trilha registra `session_terminated_reason: manual_admin`

**Resultado esperado:**
- Roteiro operacional é viável
- Documentar em `docs/operacao/T-106-revogacao-manual-mvp0.md`
- Adicionar checklist ao treino da Ops

**Artefato:** `docs/operacao/T-106-revogacao-manual-mvp0.md`

---

#### T-01a.4 · Suite de Testes: Violação Multi-Tenant (AM-07, R-019)
**Prioridade:** 🟠 Alta  
**Responsável:** Backend + QA  
**Duração:** 3 dias  
**Teste de viabilidade:** Tentativas de travessia de tenant são prevenidas?

**Cenários:**
```
Teste 1: GET /v1/tenants/outro-tenant-id
  → Esperado: 404 (não 403)
  → Trilha deve registrar: tentativa de acesso a tenant alheio, bloqueado

Teste 2: POST /v1/applications com tenant_id no body
  → Esperado: erro (parâmetro ignorado)
  → Trilha deve registrar: tentativa de injeção de tenant_id, bloqueado

Teste 3: Rolar back e tentar ler DB diretamente (simulando compromisso)
  → Esperado: DbContext global filter impede retorno de linha alheio
  → FK composta recusa inserção com tenant_id errado

Teste 4: Token de outro tenant (falso) em Authorization
  → Esperado: 401 ou erro de validação
  → Trilha deve registrar: falha de autenticação
```

**Critério de aceite:**
- [ ] Todos 4 cenários falham conforme esperado
- [ ] Trilha registra cada tentativa com `tenant_id`, IP, timestamp
- [ ] Nenhum vazamento de informação (404 vs. 403, erro genérico)

**Resultado esperado:**
- Suite de testes de regressão para toda a vida do projeto
- Executor: CI/CD rodando antes de merge em main

**Artefato:** `tests/AppBridge.ControlPlane.Tests/Security/MultiTenantViolationTests.cs`

---

### 📋 Critério de Conclusão de E-01a
- [ ] T-01a.1: Resultado documentado (broker confiável? sim/não)
- [ ] T-01a.2: Resultado documentado (rdpsign rápido? sim/não)
- [ ] T-01a.3: Roteiro operacional pronto, testado
- [ ] T-01a.4: Suite de testes em CI/CD

**Se algum teste falhar:** Registrar em `STATUS.md` §8 (riscos) e preparar mitigação para E-02.

**Bloqueador?** SIM para features · Não para setup de infra (E-00 paralelo).

---

## FASE 2: FUNDAÇÃO (E-02)

### 🏗️ E-02 · Control Plane · Fundações

**Objetivo:** Setup de banco, autenticação, autorização, trilha, health check  
**Dependências:** E-00 concluído, E-01a iniciado (paralelo)  
**Duração:** 4 semanas (~21 pontos @ 5–6 pts/semana)  
**Paralelo com:** E-01a (medições)

#### T-02.1 · Setup de Banco de Dados (PostgreSQL + EF Core)
**Prioridade:** 🔴 Crítica  
**Responsável:** Backend  
**Duração:** 5 dias  
**Pontos:** 5  
**Checklist:**
- [ ] Migrations iniciais:
  - `Users` (uuid, email, full_name, created_at, deleted_at)
  - `Tenants` (uuid, name, subdomain, created_at, deleted_at, owner_user_id)
  - `Applications` (uuid, tenant_id, name, display_name, icon_url, created_at, deleted_at)
  - `ApplicationPermissions` (uuid, tenant_id, user_id, application_id, granted_at, revoked_at)
  - `Sessions` (uuid, tenant_id, user_id, application_id, session_id, started_at, ended_at, prelaunch_flag)
  - `AuditLog` (uuid, tenant_id, user_id, action, resource_type, resource_id, before, after, timestamp, ip_address, user_agent)
- [ ] DbContext com:
  - Global filter de `tenant_id` (ADR-0004, ADR-0011)
  - Soft delete para dados de tenant (deleted_at)
  - Rastreamento automático de `created_at`, `updated_at`
  - FK composta `(tenant_id, user_id)` onde aplicável
- [ ] Migrations automáticas em startup (Develop) / Manual (Prod)
- [ ] Seed data: 1 tenant local, 3 usuários, 2 apps

**Saída esperada:**
- Banco rodando, migrations ok
- EF Core scaffolded, sem N+1 queries óbvias
- Testes de isolamento multi-tenant verde

---

#### T-02.2 · Autenticação via Entra ID / AD DS (ADR-0001)
**Prioridade:** 🔴 Crítica  
**Responsável:** Backend + Frederico  
**Duração:** 7 dias  
**Pontos:** 7  
**Checklist:**
- [ ] Endpoint `POST /v1/auth/login`:
  - Aceita `username`, `password`
  - Valida contra AD DS (ou Entra ID em hybrid modo)
  - Retorna JWT (HS256, válido por 60 min, renovável)
  - Registra tentativa na `AuditLog` (sucesso ou falha)
- [ ] Limitação de taxa (RNF-010): 5 tentativas/IP/5min, bloqueio 15 min
- [ ] Token contém:
  - `sub` (user_id em UUID v7)
  - `tenant_id` (inferido do usuário, não aceita como parâmetro)
  - `email`
  - `exp`, `iat`
  - **Nunca:** senha, privilege escalation, temp secrets
- [ ] Refresh token: `POST /v1/auth/refresh` com cookie HTTP-only
- [ ] Logout: `POST /v1/auth/logout`, invalida refresh token

**Saída esperada:**
- Launcher pode fazer login
- Token é válido por toda sessão (60 min)
- Trilha registra toda tentativa

---

#### T-02.3 · Autorização por Papel (RBAC)
**Prioridade:** 🟠 Alta  
**Responsável:** Backend  
**Duração:** 4 dias  
**Pontos:** 4  
**Papéis MVP-0a:**
- `user` — lançar apps que tem permissão (RF-003)
- `admin` — gerenciar apps, usuários, logs (Painel Admin, MVP-1)
- `provider_operator` — suporte multi-tenant (cabeçalho `X-AppBridge-Acting-Tenant`, AM-15)

**Checklist:**
- [ ] Tabela `UserRoles` (user_id, role, granted_by, granted_at)
- [ ] Middleware de autorização: atributo `[Authorize(Role = "admin")]`
- [ ] Acesso a tenant alheio com `X-AppBridge-Acting-Tenant`:
  - Exige papel `provider_operator`
  - Registra em `AuditLog` com flag `acting_as_provider: true`
  - Nenhuma mudança sem confirmação adicional (POST exige confirmação)
- [ ] Teste: tentar ler tenant alheio sem papel → 401 ou 403 (decidir)

**Saída esperada:**
- Launcher autentica como `user` e vê só seus apps
- Admin painel será scaffolding em MVP-1

---

#### T-02.4 · Trilha de Auditoria Bloqueante (ADR-0007)
**Prioridade:** 🔴 Crítica  
**Responsável:** Backend  
**Duração:** 5 dias  
**Pontos:** 5  
**Checklist:**
- [ ] Tabela `AuditLog` com:
  - UUID v7 (não auto-increment, não sequencial — ADR-0011)
  - tenant_id (obrigatório)
  - user_id (quem fez)
  - action (`login`, `launch_application`, `revoke_access`, `update_permission`, etc.)
  - resource_type, resource_id (o que foi afetado)
  - before/after (estado anterior/posterior para UPDATE, NULL para CREATE)
  - timestamp (UTC, sincronizado com NTP — RNF-020)
  - ip_address (capturado de X-Forwarded-For ou connection.RemoteIpAddress)
  - user_agent (capturado do header)
- [ ] Interceptor no EF Core: Toda `SaveChanges()` atomicamente grava em AuditLog
- [ ] **Bloqueante:** Se `SaveChanges()` falhar na trilha, transação inteira falha (sem mudança em `Users`, `Applications`, etc.)
- [ ] Retenção configurável por tenant (RNF-018):
  - Categoria "segurança" (login, permissões): 60 meses
  - Categoria "operação" (launch): 12 meses
  - Categoria "admin" (modificações): 24 meses
- [ ] Expurgo automático por retenção, registrado como ação `audit_log_purge_run`

**Saída esperada:**
- Todo acesso é registrado de forma imutável
- Retenção é auditável (trilha registra expurgos)
- Performance: add/query ≤ 10 ms

---

#### T-02.5 · Health Check com Dependências (RNF-034, Oportunidade 5)
**Prioridade:** 🟠 Alta  
**Responsável:** Backend  
**Duração:** 1 dia  
**Pontos:** 1  
**Checklist:**
- [ ] Endpoint `GET /v1/health`:
  ```json
  {
    "status": "healthy",
    "timestamp": "2026-MM-DDTHH:mm:ssZ",
    "checks": {
      "database": "healthy",
      "active_directory": "healthy",
      "signing_certificate": "healthy",
      "disk_space": "80% (warning)"
    }
  }
  ```
- [ ] Status:
  - `healthy`: todos os checks passaram
  - `degraded`: 1+ check em aviso (ex: disco > 85%)
  - `unhealthy`: 1+ check crítico falhou
- [ ] Usado por:
  - Kubernetes liveness probe (se for containerizado)
  - Monitoring (Prometheus, New Relic, etc.)
  - Launcher (heartbeat, oportunidade 1)

**Saída esperada:**
- Base para monitoramento do piloto (RNF-034, 99,5% uptime)

---

#### T-02.6 · Rate Limiting (RNF-010, PD-05)
**Prioridade:** 🟠 Média  
**Responsável:** Backend  
**Duração:** 2 dias  
**Pontos:** 2  
**Checklist:**
- [ ] Middleware de rate limit por IP (default: 100 req/min)
- [ ] Endpoints críticos mais restritivos:
  - `/v1/auth/login`: 5 req/5min (já definido em T-02.2)
  - `/v1/applications/{id}/launch`: 10 req/min (evita DOS em geração de RDP)
- [ ] Retornar HTTP 429 com cabeçalho `Retry-After`
- [ ] Registrar em trilha: bloqueio por rate limit
- [ ] Configurável por tenant (para piloto com SLA diferente)

**Saída esperada:**
- Proteção contra DOS
- Comportamento previsível

---

### 📋 Critério de Conclusão de E-02
- [ ] Banco com 6+ tabelas, migrations automáticas ok
- [ ] Autenticação contra AD DS funciona
- [ ] Autorização por papel implementada
- [ ] Trilha bloqueante, retenção configurável
- [ ] Health check retorna JSON válido
- [ ] Rate limiting em todos endpoints críticos
- [ ] Testes de isolamento multi-tenant verdes
- [ ] Documentação em `docs/API.md` §1–6 atualizada

**Artefato:** `src/AppBridge.ControlPlane/` com models, migrations, services

---

## FASE 3: CLOSURES ARQUITETURAIS (E-03)

### 🔧 E-03 · Closures Arquiteturais

**Objetivo:** Fechar gaps de design antes de features  
**Dependências:** E-02 concluído, E-01a resultados conhecidos  
**Duração:** 2 semanas (~12 pontos)

#### T-03.1 · Rastreamento de Prelaunch no Modelo (Gap 1)
**Prioridade:** 🟠 Alta  
**Responsável:** Backend  
**Duração:** 2 dias  
**Pontos:** 2  
**Checklist:**
- [ ] Adicionar coluna em `Sessions`:
  ```sql
  ALTER TABLE sessions ADD COLUMN launch_purpose VARCHAR(50) 
    DEFAULT 'real' 
    CHECK (launch_purpose IN ('real', 'prelaunch'));
  ```
- [ ] Endpoint `GET /v1/applications/{id}/usage`:
  - Retorna uso **excluindo prelaunchs**
  - Parâmetro opcional `?include_prelaunch=true` para admin
  - Documenta em `API.md` §6.2 que prelaunch é "preparação", não uso

**Saída esperada:**
- Relatório de licenças limpo
- RF-062 conta corretamente

---

#### T-03.2 · Operação de Cancelamento de Prelaunch (Gap 2)
**Prioridade:** 🟠 Alta  
**Responsável:** Backend  
**Duração:** 3 dias  
**Pontos:** 3  
**Checklist:**
- [ ] Interface `ISessionBackend`:
  ```csharp
  Task CancelSessionAsync(string sessionId, string reason, CancellationToken ct);
  ```
- [ ] Implementação RDS:
  - Loga em `AuditLog` com ação `session_cancelled_reason_{reason}`
  - Marca `Sessions.ended_at = now`
  - **Não encerra sessão no RDS** (isso é T-03.3)
- [ ] Chamar em `ISessionService.StartSessionAsync()` se falha no prelaunch
- [ ] Teste: prelaunch falha → `CancelSessionAsync()` é chamado → trilha registra → `SessionReconciler` limpa em < 60 s

**Saída esperada:**
- Prelaunch seguro contra vazamento de contagem

---

#### T-03.3 · Interface ISessionBackend Completa
**Prioridade:** 🟠 Alta  
**Responsável:** Backend  
**Duração:** 4 dias  
**Pontos:** 4  
**Checklist:**
- [ ] Operações:
  - `StartSessionAsync(applicationId, userId)` → sessionId
  - `StartPrelaunchSessionAsync(userId)` → prelaunch sessionId (sem app)
  - `CancelSessionAsync(sessionId, reason)`
  - `GetSessionAsync(sessionId)` → estado (active, ended, prelaunch)
  - `ListUserSessionsAsync(userId)` → todas as sessões do usuário
  - `TerminateSessionAsync(sessionId)` → força encerramento (MVP-1, RF-008)
- [ ] Implementação de teste: mock que retorna UUIDs
- [ ] Documentação em `ARQUITETURA.md` §4.2

**Saída esperada:**
- API simples, testável, extensível para Agent + multi-backend (AVD, etc.)

---

#### T-03.4 · SessionReconciler Implementado
**Prioridade:** 🟠 Alta  
**Responsável:** Backend  
**Duração:** 3 dias  
**Pontos:** 3  
**Checklist:**
- [ ] Hosted service que roda a cada 30 s
- [ ] Lógica:
  1. Consulta todas sessões `Sessions.ended_at IS NULL`
  2. Consulta RDS Connection Broker: quais estão de verdade ativas?
  3. Diferença = sessões "zumbi" (BD diz ativa, RDS diz not found)
  4. Marca como `ended_at = now`, registra como `reconciled_missing`
- [ ] Expiração por inatividade:
  - Sessão sem atividade > 12 h (PRE-22 + RDS timeout) é marcada como expirada
  - Registra como `reconciled_stale_expired`
- [ ] Teste: força zumbi, espera 30+ s, verifica que foi limpo

**Saída esperada:**
- Proteção contra contagem inflável (R-009 mitigado)
- Relatório de reconciliação em trilha

---

#### T-03.5 · Configuração de Cabeçalho X-AppBridge-Acting-Tenant
**Prioridade:** 🟠 Média  
**Responsável:** Backend  
**Duração:** 2 dias  
**Pontos:** 2  
**Checklist:**
- [ ] Middleware que lê cabeçalho `X-AppBridge-Acting-Tenant`
- [ ] Validação (ADR-0012 §4):
  - Exige papel `provider_operator`
  - Verifica que tenant_id existe
  - Registra em contexto de request
- [ ] DbContext usa `X-AppBridge-Acting-Tenant` se presente, senão `user.tenant_id`
- [ ] Toda request com cabeçalho é auditada com flag `acting_as_provider`
- [ ] Teste: admin tenta sem papel → erro; com papel → sucesso

**Saída esperada:**
- Suporte para operador do provedor no piloto
- R-019 (travessia de tenant) mitigado

---

### 📋 Critério de Conclusão de E-03
- [ ] `Sessions.launch_purpose` implementado, query filtra prelaunchs
- [ ] `CancelSessionAsync()` chamado em falha de prelaunch
- [ ] `ISessionBackend` com 6+ operações
- [ ] `SessionReconciler` rotor a cada 30 s
- [ ] `X-AppBridge-Acting-Tenant` valida papel e audia
- [ ] Todos 4 testes verdes

**Artefato:** `src/AppBridge.ControlPlane/Services/SessionService.cs` + `SessionReconciler.cs`

---

## FASE 4: FEATURES CORE (E-04)

### ⚡ E-04 · Catálogo, Login, Assinatura de RDP

**Objetivo:** MVP-0a mínimo: 1 usuário, 1 app, ponta a ponta  
**Dependências:** E-02, E-03 concluídos  
**Duração:** 5 semanas (~34 pontos)

#### T-04.1 · Endpoint de Catálogo (RF-001)
**Prioridade:** 🔴 Crítica  
**Responsável:** Backend  
**Duração:** 3 dias  
**Pontos:** 3  
**Checklist:**
- [ ] `GET /v1/applications` (autenticado):
  - Retorna apps com permissão para o usuário
  - Campos: uuid, name, display_name, icon_url, is_available
  - Paginação por cursor (ADR-0012 §3)
  - Status 200 · Campo `total_count` informativo
- [ ] Filtro opcional `?search=Dominio` (case-insensitive)
- [ ] Seed: 3 apps para teste (Domínio, Alterdata, Excel)

**Saída esperada:**
- Launcher consegue listar apps do usuário

---

#### T-04.2 · Assinatura de `.rdp` (RF-019, RNF-002, RNF-003)
**Prioridade:** 🔴 Crítica  
**Responsável:** Backend + Ops  
**Duração:** 5 dias  
**Pontos:** 5  
**Contexto:** O MVP-0a terá assinatura por `rdpsign.exe`. (Resultado de T-01a.2 dirá se pool é necessário.)

**Checklist:**
- [ ] Classe `RdpFileSigner : IRdpFileSigner`:
  - Método `SignAsync(rdpContent, sessionId, userId, ttl=60s)`
  - Invoca `rdpsign.exe /sha256`
  - Retorna arquivo `.rdp` assinado (binário)
  - Registra em trilha: ação `rdp_file_signed`
- [ ] Configuração:
  - Caminho do certificado (arquivo `.pfx`)
  - Senha (de vault de segredos)
  - TTL padrão: 60 s (RF-020)
- [ ] Teste: assina, valida assinatura com `mstsc` (offline test)

**Saída esperada:**
- Arquivo `.rdp` pronto para entregar ao launcher

---

#### T-04.3 · Geração de Arquivo RDP Temporário (RF-019, RNF-002)
**Prioridade:** 🔴 Crítica  
**Responsável:** Backend  
**Duração:** 4 dias  
**Pontos:** 4  
**Checklist:**
- [ ] Classe `RdpFileGenerator`:
  - Recebe: sessionId, serverName, username, domain, applicationName
  - Gera conteúdo RDP (RFC 2137 compatible):
    ```
    full address:s:<serverName>:3389
    username:s:<domain>\<username>
    remoteapplicationname:s:<applicationName>
    remoteapplicationprogram:s:<appPath>
    remoteapplicationmode:i:1
    screen mode id:i:2
    desktopwidth:i:1920
    desktopheight:i:1080
    redirectprinters:i:0
    redirectlocaldrive:i:0
    redirectlocation:i:0
    redirectsmartcard:i:0
    redirectaudio:i:1
    usbdevicestoredirect:*
    [configurações ADR-0008]
    ```
  - Aplica política de redirecionamento (ADR-0008):
    - Permitido: impressora, token USB, área de transferência, áudio
    - Negado: unidades locais, COM/LPT, entrada de áudio, USB genérico (exceto hardware token)
- [ ] Assinatura: passa para `RdpFileSigner`
- [ ] TTL: remove arquivo após 60 s (RF-020)

**Saída esperada:**
- Arquivo `.rdp` válido, com política aplicada

---

#### T-04.4 · Endpoint de Launch (RF-006, RF-020)
**Prioridade:** 🔴 Crítica  
**Responsável:** Backend  
**Duração:** 4 dias  
**Pontos:** 4  
**Checklist:**
- [ ] `POST /v1/applications/{applicationId}/launch`:
  - Autenticado (JWT)
  - Idempotente (Idempotency-Key no header, ADR-0012 §3)
  - Validações:
    1. Usuário tem permissão em application? (senão 403)
    2. Aplicativo está disponível? (senão 503)
    3. Há licença disponível? (senão 429 + retry-after)
  - Cria sessão no RDS via `ISessionBackend.StartSessionAsync()`
  - Cria entrada em `Sessions` com `launched_at`, `launch_purpose='real'`
  - Gera `.rdp` assinado
  - Registra em trilha: `launch_requested`, `session_started`
  - Retorna HTTP 200 + corpo:
    ```json
    {
      "session_id": "uuid",
      "rdp_file_url": "/v1/sessions/{session_id}/rdp-file",
      "expires_at": "2026-MM-DDTHH:mm:ssZ",
      "prelaunch_url": null
    }
    ```
- [ ] `GET /v1/sessions/{sessionId}/rdp-file`:
  - Retorna arquivo `.rdp` (binary/octet-stream)
  - Após download, arquivo é removido (ou após 60 s)
  - Acesso de outro usuário → 404 (não 403)

**Saída esperada:**
- Launcher consegue abrir aplicativo

---

#### T-04.5 · Integração com mstsc (Launcher · MVP-1, mas planejar agora)
**Prioridade:** 🟠 Média  
**Responsável:** Launcher dev (não implementar, mas planejar)  
**Duração:** Spec (1 dia)  
**Pontos:** 0  
**Nota:** MVP-0a não tem launcher funcional, mas precisa da arquitetura.

**Spec:**
- Launcher recebe URL `appbridge://launch/<app-id>`
- Faz request a `/v1/applications/{app-id}/launch`
- Recebe URL `/v1/sessions/{session-id}/rdp-file`
- Escreve `.rdp` em `%TEMP%\appbridge-{uuid}.rdp`
- Invoca `mstsc.exe %TEMP%\appbridge-{uuid}.rdp`
- Após 60 s, deleta arquivo (ou ao fechar mstsc)

---

#### T-04.6 · Teste End-to-End: Login → Catalog → Launch
**Prioridade:** 🟠 Alta  
**Responsável:** Backend + QA  
**Duração:** 2 dias  
**Pontos:** 2  
**Checklist:**
- [ ] Teste de integração (não unitário):
  1. POST `/v1/auth/login` com usuário do AD
  2. Recebe JWT válido
  3. GET `/v1/applications` com JWT
  4. Vê 3 apps
  5. POST `/v1/applications/app-1/launch`
  6. Recebe URL de `.rdp`
  7. GET da URL, recebe arquivo binário válido
  8. Trilha registra tudo

**Saída esperada:**
- MVP-0a loop funciona (ainda sem Launcher, sem atalhos)

---

### 📋 Critério de Conclusão de E-04
- [ ] Endpoints GET /v1/applications, POST /v1/applications/{id}/launch funcionando
- [ ] Arquivo `.rdp` assinado, válido
- [ ] Trilha bloqueante em toda operação
- [ ] Teste E2E verde
- [ ] Documentação em `API.md` §4 atualizada

---

## FASE 5: LAUNCH (E-05)

### 🚀 E-05 · Atalhos, UI, Testes de Aceitação

**Objetivo:** MVP-0a completo: desktop shortcuts, Menu Iniciar, UI de teste  
**Dependências:** E-04 concluído  
**Duração:** 4 semanas (~21 pontos)

#### T-05.1 · Criar Atalhos no Desktop
**Prioridade:** 🔴 Crítica  
**Responsável:** Launcher  
**Duração:** 5 dias  
**Pontos:** 5  
**Nota:** MVP-0a terá shortcut criado manualmente; MVP-1 será automático via atualização.

**Checklist:**
- [ ] Script PowerShell que cria shortcut:
  ```powershell
  $shell = New-Object -ComObject WScript.Shell
  $shortcut = $shell.CreateShortcut("$env:USERPROFILE\Desktop\Dominio.lnk")
  $shortcut.TargetPath = "appbridge://launch/app-uuid"
  $shortcut.IconLocation = "C:\path\to\icon.ico"
  $shortcut.Save()
  ```
- [ ] Ícone: baixado de `/v1/applications/{id}/icon` (endpoint em T-05.3)
- [ ] Teste: double-click no atalho → launcher ativa, faz login, abre app

---

#### T-05.2 · Handler de Protocolo Custom (appbridge://)
**Prioridade:** 🔴 Crítica  
**Responsável:** Launcher  
**Duração:** 3 dias  
**Pontos:** 3  
**Checklist:**
- [ ] Registrar protocolo no Windows:
  ```reg
  HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.appbridge\UserChoice
  [HKEY_CLASSES_ROOT\appbridge]
  @="URL:AppBridge Protocol"
  "URL Protocol"=""
  [HKEY_CLASSES_ROOT\appbridge\shell\open\command]
  @="C:\path\to\AppBridge.Launcher.exe \"%1\""
  ```
- [ ] Launcher intercepta URL: `appbridge://launch/app-uuid`
- [ ] Extrai UUID, chama API de launch

---

#### T-05.3 · Endpoint de Ícone da Aplicação
**Prioridade:** 🟠 Média  
**Responsável:** Backend  
**Duração:** 2 dias  
**Pontos:** 2  
**Checklist:**
- [ ] `GET /v1/applications/{id}/icon`:
  - Retorna imagem (PNG, 128×128)
  - Cabeçalho `ETag` para cache
  - Ou redireciona (302) para URL externa se for S3, etc.
- [ ] Seed: ícones para 3 apps

---

#### T-05.4 · UI de Teste (stub de Painel Admin)
**Prioridade:** 🟠 Média  
**Responsável:** Frontend (Blazor)  
**Duração:** 4 dias  
**Pontos:** 4  
**Checklist:**
- [ ] Scaffolding Blazor em `/admin`
- [ ] Dashboard mínimo:
  - Lista de apps
  - Usuários online (stub)
  - Logs recentes (últimas 100 linhas de trilha)
  - Health check
- [ ] Login obrigatório (papel `admin`)
- [ ] Botão "Logout"

**Nota:** E-04 do ROADMAP (Painel Admin completo) é MVP-1.

---

#### T-05.5 · Testes de Aceitação (MVP-0a Dogfood)
**Prioridade:** 🔴 Crítica  
**Responsável:** QA  
**Duração:** 4 dias  
**Pontos:** 4  
**Checklist:**
- [ ] Cenários:
  1. **Happy path:** Login → Catálogo (3 apps) → Launch Domínio → mstsc abre com RDP válido
  2. **Sem permissão:** Usuário 2 login → Catálogo (0 apps) → Permissão negada
  3. **Teto de licença:** Launch 1 app × 10 vezes → 10ª tentativa é 429 (sem licença)
  4. **Revogação:** User 1 launch → Admin revoga → User 1 não consegue novo launch
  5. **Token expirado:** Token válido por 60 min → espera 61 min → novo login necessário
  6. **Atalho desktop:** Double-click em `Dominio.lnk` → launcher abre e lança app
- [ ] Trilha: toda ação está registrada
- [ ] Resultado: documentado em `docs/auditoria/2026-MM-DD-SNNN-testes-mvp0a.md`

**Saída esperada:**
- MVP-0a funcional end-to-end
- Dogfood começa em out/2026

---

#### T-05.6 · Performance Baseline (RNF-027, RNF-028, RNF-029)
**Prioridade:** 🟠 Alta  
**Responsável:** QA  
**Duração:** 2 dias  
**Pontos:** 2  
**Checklist:**
- [ ] Medir (com prelaunch):
  - Catálogo: p50/p95 latência
  - Launch: p50/p95 latência (do request até RDP validado)
  - Assinatura: p50/p95 (já medido em T-01a.2)
- [ ] Resultado esperado (PRE-11, PRE-12):
  - Catálogo p95 ≤ 300 ms
  - Launch + assinatura p95 ≤ 1 s
  - Abertura total ≤ 5 s com prelaunch (fica para MVP-1 medir)
- [ ] Baseline para regressão futura

---

### 📋 Critério de Conclusão de E-05
- [ ] Atalhos criados no desktop
- [ ] Protocolo `appbridge://` registrado e funciona
- [ ] UI (painel admin stub) carrega
- [ ] 6 cenários de aceitação verdes
- [ ] Dados de performance coletados
- [ ] Zero bugs críticos identificados

**Artefato:** Launcher funcional (ou stub de launcher), painel admin stub, testes automatizados

---

## FASE 6: OPORTUNIDADES RÁPIDAS (E-06)

### ✨ E-06 · Melhorias Rápidas

**Objetivo:** Value-add de baixo custo, paralelo a E-04/E-05  
**Dependências:** E-02 concluído  
**Duração:** 2 semanas (~8 pontos)  
**Nota:** Fazer se tempo permitir; não bloqueia MVP-0a.

#### T-06.1 · Heartbeat Proativo do Launcher (Oportunidade 1)
**Prioridade:** 🟢 Baixa  
**Responsável:** Backend + Launcher  
**Duração:** 1 dia  
**Pontos:** 1  
**Contexto:** Launcher sincroniza catálogo a cada 5 min; adicionar health check nesse request.

**Checklist:**
- [ ] Launcher: adicionar `GET /v1/me` à sincronização
- [ ] Backend: endpoint que retorna:
  ```json
  {
    "user_id": "uuid",
    "tenant_id": "uuid",
    "is_authenticated": true,
    "expires_at": "2026-MM-DDTHH:mm:ssZ"
  }
  ```
- [ ] Launcher detecta expiração antes de tentar launch

**Saída esperada:**
- Melhor UX: erro de token antecipado (RNF-042)

---

#### T-06.2 · Reconciliação Manual de Licenças (Oportunidade 2)
**Prioridade:** 🟢 Baixa  
**Responsável:** Backend + UI  
**Duração:** 2 dias  
**Pontos:** 2  
**Checklist:**
- [ ] Backend: `POST /v1/applications/{id}/reset-usage-count`:
  - Exige papel `admin`
  - Zera contador
  - Registra em trilha: `usage_count_reset_by_admin`, reason (free-text)
  - Retorna contagem anterior
- [ ] UI: botão em painel admin, modal de confirmação com motivo

**Saída esperada:**
- Fallback operacional se bug de contagem acontecer

---

#### T-06.3 · Varredura de Segredos Automática (Oportunidade 4)
**Prioridade:** 🟢 Baixa  
**Responsável:** DevOps  
**Duração:** 2 dias  
**Pontos:** 2  
**Checklist:**
- [ ] GitHub Action no `.github/workflows/`:
  - Roda `detect-secrets scan --all-files`
  - Valida contra `.secrets.baseline`
  - Falha se novo segredo detectado
- [ ] Documentação em `docs/SETUP-DEV.md` sobre como adicionar false positives

**Saída esperada:**
- Proteção automática contra vazamento

---

#### T-06.4 · Teste Ofensivo de Violação Multi-Tenant (Oportunidade 3, já em E-03)
**Prioridade:** 🟢 Baixa  
**Responsável:** Backend  
**Duração:** 3 dias (mas ligado a E-03, fazer em paralelo)  
**Pontos:** 3  
**Checklist:** Ver T-03.4 (incluído em E-03, mas pode ser paralelo)

---

### 📋 Critério de Conclusão de E-06
- [ ] Oportunidades 1, 2, 4 implementadas (não bloqueiam MVP-0a)
- [ ] Teste ofensivo em E-03

---

## 📊 MATRIZ DE DEPENDÊNCIAS

```
E-00 (Setup)
  ├─→ E-01a (Medições) [paralelo]
  ├─→ E-02 (Fundação) ─→ E-03 (Gaps) ─→ E-04 (Features) ─→ E-05 (Launch)
  └─→ E-06 (Oportunidades) [paralelo a E-02/E-03]
```

**Caminho crítico:** E-00 → E-02 → E-03 → E-04 → E-05  
**Paralelizáveis:** E-01a, E-06 rodam alongside E-02/E-03/E-04

---

## 🕐 CRONOGRAMA ESTIMADO

| Fase | Épico | Pontos | Sprints | Data Inicio | Data Fim |
|------|-------|--------|---------|-------------|----------|
| Setup | E-00 | 0 | 1 | 2026-08-15 | 2026-08-22 |
| Medições | E-01a | 8 | 2 | 2026-08-22 | 2026-09-05 |
| Fundação | E-02 | 21 | 5 | 2026-08-22 | 2026-09-26 |
| Gaps | E-03 | 12 | 3 | 2026-09-26 | 2026-10-17 |
| Features | E-04 | 34 | 7 | 2026-10-17 | 2026-12-05 |
| Launch | E-05 | 21 | 4 | 2026-12-05 | 2026-12-31 |
| Oportunidades | E-06 | 8 | 2 | Paralelo | 2026-12-31 |

**Data alvo MVP-0a:** Meados de out/2026 (E-04 Sprint 2) = demonstração funcional  
**Data alvo dogfood:** out/2026 (após E-05)

**Velocidade assumida:** 8 pontos/semana (solo, 40 h/semana dedicadas)  
**Se dedicação 50% (20 h/semana):** ~4 pts/semana, dobracoluna prazos.

---

## ✅ CRITÉRIOS DE ACEITE POR FASE

### E-00 · Setup
- [ ] Todos ADRs novos aceitos
- [ ] Repo criado, CI/CD rodando, zero secrets
- [ ] Primeiro commit feito sem erro

### E-01a · Medições
- [ ] T-01a.1: Resultado do Connection Broker documentado
- [ ] T-01a.2: Resultado de rdpsign documentado
- [ ] T-01a.3: Roteiro operacional testado
- [ ] T-01a.4: Suite de testes multi-tenant verde

### E-02 · Fundação
- [ ] Banco com migrations, EF Core setup
- [ ] Autenticação contra AD funciona
- [ ] Autorização por papel funciona
- [ ] Trilha bloqueante, retenção ok
- [ ] Health check retorna JSON válido
- [ ] Rate limiting protege endpoints críticos

### E-03 · Gaps
- [ ] `Sessions.launch_purpose` rastreia prelaunch
- [ ] `CancelSessionAsync()` implementado
- [ ] `ISessionBackend` com 6+ operações
- [ ] `SessionReconciler` rotor
- [ ] `X-AppBridge-Acting-Tenant` valida papel

### E-04 · Features
- [ ] Endpoints GET `/v1/applications`, POST `/v1/applications/{id}/launch` funcionando
- [ ] Arquivo `.rdp` assinado, TTL 60 s, removido após uso
- [ ] Trilha registra tudo
- [ ] Teste E2E: login → catálogo → launch → RDP válido

### E-05 · Launch
- [ ] Atalhos desktop funcionam
- [ ] Protocolo `appbridge://` registrado
- [ ] Painel admin stub carrega
- [ ] 6 cenários de aceitação verdes
- [ ] Performance medida, documentada

### E-06 · Oportunidades
- [ ] Heartbeat, reconciliação manual, varredura de segredos (nice-to-have)

---

## 🎯 RECOMENDAÇÕES FINAIS

1. **Prioridade #1: E-00 + E-01a**  
   Decisão sobre R-020, R-021 não pode demorar. Medições de viabilidade (R-009, R-013) informam design de E-02.

2. **Prioridade #2: E-02 + E-03**  
   Fundação sólida (banco, auth, trilha) + closures (prelaunch rastreável, cancelamento) são pré-requisitos de features.

3. **Dedique tempo a T-005 (medições)**  
   Não deixe para dogfood (out/2026). Descobrir que rdpsign é lento ou Connection Broker não é confiável em set/2026 é tarde demais.

4. **Teste ofensivo cedo (E-03)**  
   Multi-tenant é R-019 (crítico). Testar antes de features economiza refactoring massivo.

5. **Dogfood em out/2026, não jan/2027**  
   MVP-0b começa dez/2026–jan/2027. Feedback de out/2026 é crítico para piloto de abr/2027.

---

## 📝 PRÓXIMOS PASSOS

1. [ ] Revisar backlog com Frederico
2. [ ] Ajustar estimativas baseado em velocidade histórica
3. [ ] Priorizar: E-00 semanal, E-01a paralelo com E-02
4. [ ] Criar issues no GitHub com checklist acima
5. [ ] Agendar retro semanal para medir velocidade real

