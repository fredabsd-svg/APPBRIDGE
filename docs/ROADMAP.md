# ROADMAP e BACKLOG — AppBridge
> Entregável 7 de 7 da fase de Design · Sessão S001 · 2026-08-08
> Status: **✅ aprovado por Frederico em 2026-08-08** (RP-04)
> Depende de: todos os entregáveis anteriores e ADR-0001 a ADR-0012
> Alterado após aprovação: ADR-0013 (marcos, §2 e §5), ADR-0014 (portão G-01) e **ADR-0016**
> (backlog único; incorpora T-207, T-506 e T-1106 da revisão S008)

---

## 1. Como ler este documento

### 1.1 Estimativa relativa

Pontos na escala 1 · 2 · 3 · 5 · 8 · 13. Para permitir a checagem de capacidade da §4, ancoro a
escala:

> `PREMISSA:` (PRE-25) **1 ponto ≈ meio dia de trabalho focado** de um desenvolvedor que conhece a
> stack. É âncora de calibração, não promessa. A velocidade real é desconhecida — este é o primeiro
> projeto na stack — e só a primeira semana de implementação a revela.

### 1.2 Definição de preparado (uma tarefa pode começar quando)

Tem requisito rastreado · tem critério de aceite verificável · as decisões que ela depende estão em
ADR aceito · nenhuma premissa bloqueante em aberto.

### 1.3 Definição de pronto (RP-08, RNF-050)

Código com plano de teste · testes passando · **documentação atualizada** · rastreabilidade
requisito↔código · nenhuma pendência de segurança nova sem registro.

---

## 2. Linha do tempo — vigente

> **Replanejada e aprovada em 2026-08-08 (ADR-0013, opção A).** A §4 mostra por quê; a §5 detalha o
> recorte. A linha do tempo original de P8 está preservada abaixo para comparação.

| Marco | Data vigente | Conteúdo |
|-------|-------------|----------|
| **M1 · Design fechado** | ✅ **2026-08-08** | 7 entregáveis aprovados, 13 ADRs aceitos |
| **M2a · MVP-0a — esqueleto ambulante** | meados de out/2026 | Um usuário, um aplicativo, ponta a ponta, com `.rdp` assinado, trilha e V-01/V-05/V-06 |
| **M2b · MVP-0b — dogfood real** | dez/2026 a jan/2027 | CS-01 a CS-04 integralmente |
| **M2c · MVP-1 (subconjunto do piloto)** | fev a mar/2027 | **Escopo a definir — B-009, R-025** |
| **M3 · Piloto Caminho B** | **abr–jun/2027** | CS-05, 3–5 escritórios pagantes, portões G-01 a G-05 cumpridos |

| Marco original (P8) | Data | Situação |
|---------------------|------|----------|
| Design fechado | fim de ago/2026 | Antecipado |
| MVP-0 completo | meados de out/2026 | **Substituído por M2a + M2b** (ADR-0013) |
| Piloto | 1º tri/2027 | **Substituído por M3 em abr–jun/2027** (ADR-0013) |

---

## 3. Backlog MVP-0

### E-01 · Infraestrutura base — 34 pts · **caminho crítico**

> Não é código, e é a maior fonte de risco de calendário (T-006). Depende de terceiros, de compra e
> de disponibilidade de máquinas e pessoas.

| ID | Tarefa | Critério de aceite | Est. |
|----|--------|--------------------|------|
| T-101 | Adquirir host, Windows Server 2025 e RDS CALs por usuário | Nota fiscal e licenças ativadas; PRE-04 e PRE-18 confirmadas | 3 |
| T-102 | Hyper-V com **AB-DC01 e AB-RDS01 separados** | Duas VMs ativas; `whoami /priv` na AB-RDS01 não mostra usuário final com logon local no DC (ADR-0002, RNF-007) | 5 |
| T-103 | Promover AB-DC01 a controlador de domínio, com sufixo de UPN roteável | Domínio funcional; UPN compatível com o Entra (ADR-0001, riscos) | 5 |
| T-104 | Instalar a pilha RDS na AB-RDS01 e publicar um RemoteApp de teste | RemoteApp abre por `.rdp` manual | 5 |
| T-105 | FSLogix + AppLocker/WDAC em allowlist | Perfil em container; binário não publicado é bloqueado (V-08, RNF-006, RNF-011) | 5 |
| T-106 | **Ingressar as estações no domínio** e aplicar as GPOs: delegação de credenciais restrita, política de redirecionamento, impressão digital do certificado | Estação abre RemoteApp sem pedir senha; disco local não redireciona; `.rdp` não assinado é recusado (ADR-0008, ADR-0009, ADR-0010) | 8 |
| T-107 | Malha privada com ACL restringindo alcance ao 3389; **varredura externa** | V-01 executada e registrada; nenhuma porta RDP visível (CS-04, ADR-0003) | 3 |

**Aceite do épico:** um usuário real abre um RemoteApp a partir da sua estação, sem digitar senha,
sem `mstsc` manual, com a porta 3389 comprovadamente fechada para a internet.

### E-02 · Fundação do Control Plane — 26 pts

| ID | Tarefa | Critério de aceite | Est. |
|----|--------|--------------------|------|
| ~~T-201~~ | ✅ Esqueleto ASP.NET Core, health check, log estruturado com `correlationId` | `/health` responde; um lançamento é rastreável ponta a ponta pelo log (RNF-039, RNF-040) | 3 |
| ~~T-202~~ | ✅ EF Core + PostgreSQL + primeira migração **já com `tenant_id` em todas as tabelas** | Migração aplica e reverte (RNF-052, ADR-0011) | 5 |
| ~~T-203~~ | ✅ `TenantContext` + filtro global no `DbContext` | Consulta sem cláusula explícita não retorna dado de outro tenant (ADR-0004) | 5 |
| ~~T-204~~ | ✅ **Chaves estrangeiras compostas com `tenant_id`** | Tentativa de gravar referência cruzada é recusada **pelo banco** (ADR-0011 §4) | 3 |
| ~~T-205~~ | ✅ `AuditWriter` transacional | Falha simulada de gravação **nega** a operação (V-05, ADR-0007) | 5 |
| ~~T-206~~ | ✅ **Teste automatizado de violação de tenant** | V-02 na suíte; leitura e escrita cruzadas falham (ADR-0004 item 9) | 3 |
| ~~T-207~~ | ✅ **Coluna `purpose` na tabela `launch`** (enum `user_initiated \| prelaunch`) e filtro de prelaunch nas consultas de metering | Contagem de RF-062 **não soma prelaunchs**; teste cobre o caso (ADR-0016, Gap 1) | 2 |

> **T-201 concluída em 2026-08-10 (S010).** `src/AppBridge.ControlPlane.Api` — .NET 10, `AppBridge.slnx`.
> Health check em `/v1/health`, extensível: cada dependência real (PostgreSQL, AD DS, certificado de
> assinatura, `ISessionBackend`) registra seu próprio `IHealthCheck` quando o código que a acessa
> existir, em vez de um stub sem lastro criado hoje. **Nota corrigida em T-301:** esta previsão
> original dizia "PostgreSQL em T-202" — impreciso; T-202 só construiu o schema, sem nenhum
> consumidor em tempo de execução no `Program.cs`. O `IHealthCheck` de PostgreSQL só chegou em T-301,
> a primeira tarefa que de fato conecta ao banco a partir da Api.
> `CorrelationIdMiddleware` grava duas linhas de log por requisição (início e fim), com o
> `CorrelationId` no escopo — **verificado na prática**, não só declarado: um teste captura o log
> real e confirma que o ID aparece nas duas linhas, e que duas requisições concorrentes não
> misturam seus IDs. 7 testes, build sem warning, sem vulnerabilidade conhecida
> (`Microsoft.OpenApi` pinado em 2.11.0 — GHSA-v5pm-xwqc-g5wc).
>
> **Correção de sequenciamento (não é mudança de escopo — RP-07 não se aplica; é ajuste de ordem de
> execução):** `STATUS.md` §3 dizia que E-02 "depende da infraestrutura existir" (E-01). Isso vale
> para o host RDS de produção — não para o esqueleto do Control Plane, que só precisa de um
> PostgreSQL de desenvolvimento. Ambiente de dev instalado nesta sessão: .NET 10 SDK 10.0.302 e
> PostgreSQL 16 local. E-02 segue **em paralelo** com a aquisição de T-101, não depois dela; o que
> continua bloqueado por T-101 é o *deploy* real e os testes de integração contra AD DS/RDS
> verdadeiros (T-503, T-602, e a futura implementação real de `IIdentityProvider` que ADR-0017 §5
> deixou explicitamente fora de T-301).
>
> **T-202 concluída em 2026-08-10 (S010).** `AppBridge.ControlPlane.Domain` (15 entidades, fiéis a
> `MODELO-DE-DADOS.md`) e `AppBridge.ControlPlane.Infrastructure` (EF Core 10 + Npgsql, convenções
> próprias de `snake_case`, conversor de enum e mapeamento de `xmin` do PostgreSQL como token de
> concorrência — sem dependência nova para isso). Migração `InitialCreate` gera as 15 tabelas com
> `tenant_id NOT NULL` em todas exceto `tenant` e `signing_certificate` (as duas exceções
> deliberadas do modelo). **Verificado na prática, não só lido:** a migração foi aplicada e revertida
> de fato contra um PostgreSQL real (`appbridge_dev`), com inserção de dado válido e rejeição
> confirmada — pelo nome da constraint — das duas CHECK (`ck_retention_policy_minimum`,
> `ck_redirection_policy_exception_reason`) e da unicidade de `tenant.slug`. A suíte
> `SchemaTests.cs` (8 testes, banco `appbridge_test` dedicado) automatiza essas mesmas verificações
> e roda `IMigrator` para cima e para baixo dentro do teste — 8 de 8 passando.
>
> **Erro corrigido nesta tarefa:** o build do projeto de teste emitiu `MSB3277` — conflito entre
> `Microsoft.EntityFrameworkCore.Relational` 10.0.4 (trazido transitivamente por
> `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, ainda não realinhado com o EF Core 10.0.10 usado
> diretamente) e 10.0.10. Não havia versão mais nova do pacote Npgsql disponível no NuGet no momento
> desta sessão; corrigido fixando `Microsoft.EntityFrameworkCore.Relational` em 10.0.10 explicitamente
> no `.csproj` da Infrastructure, com comentário explicando o motivo — ponto a revisitar quando o
> Npgsql lançar uma versão alinhada.
>
> A chave estrangeira composta com `tenant_id` (ADR-0011 §4) fica **para T-204**, como planejado —
> cada referência entre entidades carrega um comentário `TODO(T-204)` apontando para a decisão.
>
> **T-203 concluída em 2026-08-10 (S010).** `ITenantContext`/`TenantContext` (Infrastructure) e um
> filtro global aplicado por reflexão a cada tipo de entidade em `OnModelCreating`: quem implementa
> `ITenantScoped` **e** deriva de `AuditedEntity` recebe `TenantId == contexto.TenantId &&
> DeletedAt == null`; quem só implementa uma das duas recebe só a cláusula correspondente. A
> combinação dos dois filtros no mesmo lugar não é invenção desta tarefa — é a consequência que
> ADR-0011 §5 já havia decidido ("toda consulta considere `deleted_at`... junto com o filtro de
> tenant"), executada agora que o `DbContext` finalmente tem de onde ler o tenant corrente.
> **Verificado com PostgreSQL real**, não só por leitura do código: consulta sem `Where` devolve
> só a linha do tenant certo; contexto sem tenant resolvido devolve **zero linhas**, não todas
> (isolamento falha fechado); linha com exclusão lógica some da consulta padrão e reaparece com
> `IgnoreQueryFilters()` — o mesmo mecanismo que o papel de operador do provedor (RF-075, MVP-1)
> vai usar de forma nominal e auditada, não uma trava sem saída. 4 novos testes em
> `TenantIsolationTests.cs`, 12 de 12 passando no projeto de Infraestrutura.
>
> **Erro de infraestrutura de teste corrigido nesta tarefa:** rodar `SchemaTests` e
> `TenantIsolationTests` juntos falhou com `relation "application" does not exist" — não é bug do
> filtro, é corrida: os dois conjuntos de teste migram o mesmo banco `appbridge_test` para cima e
> para baixo, e o xUnit paraleliza classes de teste por padrão. Corrigido serializando o assembly
> (`CollectionBehavior(DisableTestParallelization = true)`) — o banco real e compartilhado é um
> recurso inerentemente serial enquanto não houver Testcontainers.
>
> **T-204 concluída em 2026-08-10 (S010).** 15 chaves estrangeiras compostas `(tenant_id, x_id) ->
> tabela(tenant_id, id)` — todas as referências entre entidades de tenant listadas em
> MODELO-DE-DADOS.md, incluindo três que estavam documentadas no modelo mas sem o comentário
> `TODO(T-204)` no código (`redirection_policy.application_id`, `application_permission.granted_by`/
> `revoked_by`, `launch.session_id`, `access_event.user_account_id`) — corrigidas junto, não
> deixadas para trás. Seis chaves alternativas `UNIQUE (tenant_id, id)` nas entidades que são alvo de
> referência (`application`, `group`, `host_pool`, `session_host`, `session`, `user_account`) — a
> "chave candidata" que o próprio ADR-0011 §4 nomeia. Mais 13 chaves estrangeiras simples `tenant_id
> -> tenant(id)`, uma por tabela com escopo de tenant — declaradas em `MODELO-DE-DADOS.md` como
> `uuid FK` mas nunca antes ligadas ao banco. Todas com `ON DELETE RESTRICT`: um tenant, aplicativo
> ou usuário nunca é fisicamente removido enquanto tiver dado dependente (ADR-0011 §3), então a
> restrição nunca deveria disparar em uso normal — se disparar, é sinal de um `DELETE` que não
> deveria ter sido tentado.
>
> **Verificado com PostgreSQL real, nas duas direções:** uma escrita cruzada de tenant (aplicativo do
> tenant B apontando para o `host_pool` do tenant A) foi tentada por `psql` e recusada com o nome de
> constraint exato (`fk_application_host_pool`); a mesma escrita, com o par `tenant_id`/`host_pool_id`
> correto, foi aceita. 2 novos testes automatizados em `TenantForeignKeyTests.cs` fixam essa mesma
> prova como regressão. `TenantIsolationTests.cs` precisou de correção: usava um `host_pool_id`
> fabricado, válido antes de T-204 porque nada verificava — passou a falhar corretamente depois da
> FK, e foi corrigido para criar um `HostPool` real por tenant. **21 testes automatizados no total**
> no Control Plane (7 Api + 8 Schema + 4 TenantIsolation + 2 TenantForeignKey), todos passando.
>
> **T-205 concluída em 2026-08-10 (S010).** `IAuditWriter`/`AuditWriter`
> (`Infrastructure/Auditing`): um único caminho pelo qual toda operação de segurança (RF-036,
> RF-037, RF-039, RF-041, RF-042) grava seu registro de trilha — `ExecuteAsync` adiciona a entrada de
> auditoria, executa a mutação de estado da concessão (`grant`, síncrona e só-de-banco de propósito:
> a assinatura em si impede que um efeito colateral externo — assinar `.rdp`, chamar
> `ISessionBackend` — entre no limite transacional) e chama `SaveChangesAsync` uma única vez. Se
> qualquer parte falhar, nada é persistido e `AuditWriteFailedException` é lançada — o tipo próprio
> existe para que o endpoint que a chamar (T-301 em diante) responda com o `503 AUDIT_UNAVAILABLE`
> estável de `API.md`/ADR-0012, não um 500 genérico. A falha é sempre logada primeiro (ADR-0007
> condição 2), porque a própria trilha em banco é o que falhou.
>
> **Verificado com uma falha de gravação simulada, não hipotética**: um `SaveChangesInterceptor` de
> teste (`ThrowingSaveChangesInterceptor`) lança exatamente no ponto em que o EF Core emitiria o SQL
> — a janela específica que o ADR-0007 fecha (banco que lê mas não escreve). Com ele, `ExecuteAsync`
> lança `AuditWriteFailedException` e, lido de volta por um contexto limpo, **nem a linha de
> auditoria nem a mutação da concessão foram gravadas** — a negação é da operação inteira, não só da
> metade da auditoria. Um terceiro teste confirma a linha de log de erro antes do relançamento.
> 3 novos testes em `AuditWriterTests.cs`. **24 testes automatizados no total** no Control Plane
> (7 Api + 8 Schema + 4 TenantIsolation + 2 TenantForeignKey + 3 AuditWriter), todos passando.
>
> **Interrupção de ambiente nesta tarefa, sem relação com o código:** o PostgreSQL local havia parado
> entre sessões (`service postgresql status` → `down`); reiniciado (`service postgresql start`) antes
> de rodar os testes. Não é achado de produto — registrado porque `docs/SETUP-DEV.md` já orienta como
> subir o banco, mas não como diagnosticar que ele caiu.
>
> **T-206 concluída em 2026-08-10 (S010).** `TenantViolationTests.cs` — o local explícito e nomeado
> da suíte para **V-02** (`SEGURANCA.md` §7: "tentar ler e gravar dados de outro tenant e exigir
> falha", AM-07/AM-14). T-203 e T-204 já provavam os dois mecanismos, mas só com `Application` (leitura)
> e `application`/`host_pool` (escrita); esta tarefa fechou duas lacunas reais de forma, não de
> volume: (1) nenhum teste anterior havia exercitado `SetTenantFilter` sozinho — o caminho que
> entidades de trilha (sem `deleted_at`, ADR-0011 §3) percorrem, distinto de
> `SetTenantAndSoftDeleteFilter` — fechada com um caso em `AccessEvent`; (2) nenhum teste anterior
> cobria uma FK composta **anulável** nem uma tabela com **duas FKs independentes para o mesmo tipo
> principal** (`application_permission.granted_by`/`revoked_by`, ambas para `user_account` — o
> desenho mais propenso a esconder um erro de configuração por cópia-e-cola) — fechadas com
> `redirection_policy.application_id` e `application_permission.granted_by`. **Cobertura
> deliberadamente não exaustiva**: as 13 tabelas com escopo de tenant e as 15 FKs compostas não são
> testadas uma a uma — ADR-0004 item 9 pede casos que provem o mecanismo, não uma matriz
> combinatória, e os dois mecanismos já foram exercitados em cinco formas distintas de
> entidade/relacionamento entre este arquivo e T-203/T-204. **28 testes automatizados no total** no
> Control Plane (7 Api + 8 Schema + 4 TenantIsolation + 2 TenantForeignKey + 3 AuditWriter + 4
> TenantViolation), todos passando contra PostgreSQL real.
>
> **T-207 concluída em 2026-08-10 (S010).** A coluna `purpose` e o enum `LaunchPurpose` já existiam
> desde T-202 — o que faltava era o teste que o próprio critério de aceite pede. `LaunchPurposeMeteringTests.cs`
> grava três lançamentos `user_initiated` e dois `prelaunch` e confirma que uma contagem filtrada por
> `purpose = user_initiated` devolve 3, não 5 — exatamente o que ADR-0016 Gap 1 exige de qualquer
> consulta futura de RF-062. **Não é um serviço de metering** (RF-062 é MVP-1, ADR-0006, e ainda não
> tem endpoint): o teste prova a garantia na camada de dados que esse serviço vai usar, não simula o
> serviço em si — inventar um antes da hora seria escopo além do que T-207 pede (RP-05). **29 testes
> automatizados no total** no Control Plane (7 Api + 22 Infrastructure), todos passando. **E-02 ·
> Fundação do Control Plane está com todas as suas 7 tarefas concluídas.**

### E-03 · Identidade e autorização — 21 pts

| ID | Tarefa | Critério de aceite | Est. |
|----|--------|--------------------|------|
| ~~T-301~~ | ✅ `POST /auth/session`, com registro na mesma transação | Login gera `access_event`; falha de trilha devolve `503 AUDIT_UNAVAILABLE` | 8 |
| ~~T-302~~ | ✅ Vínculo identidade → conta AD por **SID** | Renomear a conta no AD não quebra o vínculo nem a trilha (RF-002) | 5 |
| ~~T-303~~ | ✅ Refresh, logout **(servidor)** e armazenamento no Credential Manager | Token renova sem login; logout invalida (RF-004..RF-006) | 5 |
| ~~T-304~~ | ✅ `AuthorizationService` com vigência de permissão | Permissão revogada nega o lançamento seguinte em ≤ 60 s (V-07, RNF-030) | 3 |

> **T-301 concluída em 2026-08-10 (S010).** `POST /v1/auth/session` implementado e verificado de
> ponta a ponta — construído sobre tudo que E-02 preparou (`AppBridgeDbContext`, `ITenantContext`,
> `IAuditWriter`), agora com consumidor real pela primeira vez.
>
> **Lacuna encontrada e fechada durante a implementação, com ADR próprio:** `API.md` já prometia
> `refreshToken` na resposta e `POST /v1/auth/refresh` (T-303), mas `MODELO-DE-DADOS.md` não tinha
> tabela para persistir um — sem estado do lado do servidor, `logout` (RF-006) não teria o que
> revogar. **ADR-0017** decide os dois pontos que faltavam: `accessToken` em JWT HS256 (chave via
> `APPBRIDGE_JWT_SIGNING_KEY`, nunca arquivo — RP-06), e `refreshToken` opaco guardado **só como hash
> SHA-256** (nunca o valor), em nova tabela `refresh_token` (`MODELO-DE-DADOS.md` §4.3), seguindo as
> convenções de sempre (ADR-0011: UUID v7, `timestamptz`, FK composta com `tenant_id`).
>
> **Nenhuma integração real com Entra ID/AD DS nesta tarefa** (ADR-0017 §5) — E-01 não tem hardware
> comprado, não existe domínio nem tenant Entra para validar contra. Escrever uma implementação "real"
> sem nada para testá-la violaria a disciplina deste projeto de rodar para verificar. `IIdentityProvider`
> é a interface (Infrastructure); `DevIdentityProvider` (Api, registrado **só sob `Development`**)
> existe para permitir rodar e testar o endpoint nesta sessão — ver R-032. `AddAuthentication().AddJwtBearer()`
> também já está registrado em `Program.cs`, sem nenhum endpoint protegido para exercitá-lo ainda —
> mesmo raciocínio de T-201 para o health check: o mecanismo entra quando a decisão de claims é
> tomada, não quando o primeiro consumidor aparece.
>
> **`AppBridgeDbContext`/`ITenantContext`/`IAuditWriter` finalmente registrados no `Program.cs`** —
> T-301 é a primeira tarefa com consumidor real em tempo de execução, exatamente como antecipado ao
> fechar E-02. O health check ganhou `postgresql` como primeira dependência real (a nota de T-201
> dizia "PostgreSQL em T-202" — impreciso; T-202 só construiu o schema, T-301 é quem de fato conecta
> em runtime, corrigido aqui).
>
> **Verificado rodando a aplicação de verdade** (`dotnet run`, não só os testes): login bem-sucedido
> grava `access_event` (`result = success`) e `refresh_token`, atualiza `last_login_at`, devolve
> `201` com os tokens; token inválido, usuário desconhecido (com tenant resolvido), usuário
> desabilitado e tenant suspenso devolvem os códigos exatos de `API.md` (`INVALID_IDENTITY_TOKEN`,
> `USER_DISABLED`, `TENANT_SUSPENDED`), cada um com o `access_event` de falha correspondente quando
> há tenant para atribuir. **13 novos testes automatizados** (6 em `AuthEndpointTests.cs`, contra o
> host real via `WebApplicationFactory` e PostgreSQL real — incluindo a falha de gravação simulada
> por `SaveChangesInterceptor` devolvendo `503 AUDIT_UNAVAILABLE`, a mesma técnica de T-205 agora
> provada na fronteira HTTP; 2 em `JwtSessionTokenIssuerTests.cs`, sem banco, provando que o token
> emitido valida com a mesma chave e falha com uma diferente; e as 3 suítes pré-existentes de T-201
> precisaram de ajuste — ver correções abaixo). **37 testes automatizados no total** no Control Plane
> (13 Api + 24 Infrastructure), todos passando.
>
> **Duas correções de teste encontradas rodando a suíte, nenhuma de produto:**
> 1. Os três arquivos de teste de T-201 (`HealthCheckTests`, `CorrelationIdMiddlewareTests`,
>    `RequestLoggingTests`) quebraram porque `Program.cs` passou a exigir `APPBRIDGE_DB_CONNECTION`/
>    `APPBRIDGE_JWT_SIGNING_KEY` para iniciar — correto, é uma dependência real agora. Corrigido
>    centralizando a configuração de teste em `ApiTestFactory.cs`, reaproveitada pelos quatro
>    arquivos de teste do projeto Api.
> 2. `ApiTestFactory` inicialmente injetava a configuração via `ConfigureAppConfiguration` (padrão
>    comum do `WebApplicationFactory`) — não funcionou, porque `Program.cs` lê a configuração
>    obrigatória **antes** de `Build()`, e o `ConfigureAppConfiguration` do `WebApplicationFactory`
>    só se aplica no ponto em que ele intercepta `Build()`, tarde demais para o `?? throw` logo após
>    `CreateBuilder(args)`. Corrigido definindo variáveis de ambiente reais no processo — que
>    `CreateBuilder` já lê como uma das suas próprias fontes padrão, de forma síncrona.
>
> **T-303 concluída em 2026-08-10 (S010) — escopo restrito ao servidor.** O título da tarefa mistura
> dois lados: `POST /v1/auth/refresh` e `POST /v1/auth/logout` (esta tarefa) e o armazenamento no
> Windows Credential Manager (RF-005), que já é **T-803** por direito próprio, em `E-08 · Launcher —
> fundação` — um projeto WinUI que não existe neste repositório. Construir o launcher agora para
> "completar" o título seria inventar escopo que T-303 não pede (RP-05); o armazenamento cliente
> continua para quando E-08 começar.
>
> `POST /v1/auth/refresh`: encontra o `refresh_token` pelo hash do valor apresentado (única forma de
> saber o tenant neste ponto — segundo uso legítimo de `IgnoreQueryFilters()`, depois do de T-301,
> ambos pela mesma razão de bootstrap), confere validade/revogação/expiração, **revoga o token
> apresentado e emite um novo** (rotação: reutilizar um token já trocado — a assinatura de um roubo —
> passa a falhar a partir da primeira troca), e reconfere `TenantStatus`/`UserAccountStatus` **de
> novo** (um usuário desabilitado depois de emitido o refresh token não pode continuar renovando
> sessão). **Não passa por `IAuditWriter`** — decisão registrada, não esquecimento: ADR-0007 Parte 1
> não lista RF-004 entre os eventos bloqueantes, e `MODELO-DE-DADOS.md` §7.2 não categoriza refresh
> como tipo de `access_event` (só autenticação, logout e fim de sessão). `POST /v1/auth/logout`: a
> mesma busca, mas **grava `access_event` (`logout`) via `IAuditWriter`** — este sim está na
> categorização de §7.2 — e é idempotente por desenho: token desconhecido ou já revogado devolve
> `204` igual a um logout que revogou de verdade, sem distinguir os casos (mesmo raciocínio
> anti-enumeração de ADR-0012 §5).
>
> **Bug real encontrado e corrigido, não só de T-303**: inspecionar `refresh_token.created_at`
> durante a verificação mostrou `-infinity` — `CreatedAt`/`UpdatedAt` são `init`-only por desenho
> (imutabilidade de domínio), mas **nada em código nenhum jamais os definia**, então todo `INSERT`
> desde T-202 gravava `DateTimeOffset.MinValue` silenciosamente. Corrigido no único lugar que
> resolve para sempre: `AppBridgeDbContext.SaveChanges(Async)` agora carimba `CreatedAt` em toda
> entidade `Added` e `UpdatedAt` em toda `Modified`, via `entry.Property(...).CurrentValue` — que
> continua funcionando sobre uma propriedade `init` porque o rastreador de mudanças do EF Core opera
> abaixo da restrição de tempo de compilação do C#, o mesmo mecanismo que já materializa entidades
> vindas do banco. Mesma disciplina de "mecanismo, não lembrete" de T-203/T-204/T-205.
>
> **Segundo bug encontrado e corrigido no mesmo lote, em código já publicado (T-301)**: nenhuma das
> duas requisições (`LoginRequest.IdentityToken`, e agora `RefreshTokenRequest.RefreshToken`) exigia
> a presença do campo — um corpo sem ele vinculava `null` silenciosamente (o C# não-anulável não é
> garantia de tempo de execução sem `required`), e a próxima linha de código lançava
> `NullReferenceException`, virando um `500` genérico em vez de um `400` claro. Corrigido marcando os
> dois campos como `required`; verificado enviando `{}` de propósito e confirmando `400 Bad Request`,
> não mais uma exceção não tratada.
>
> **Verificado rodando a aplicação de verdade** (`dotnet run` + `curl` + `psql`): sessão completa —
> login, refresh (token novo, token antigo revogado), reuso do token antigo recusado, logout,
> segundo logout idempotente, refresh após logout recusado, corpo malformado devolvendo `400`. **12
> novos testes automatizados**: 9 em `RefreshLogoutEndpointTests.cs` (cada um fazendo login de
> verdade pelo endpoint real antes de exercitar refresh/logout, não montando um token à mão), 3 em
> `AuditColumnStampingTests.cs` (`CreatedAt` carimbado na inserção, `UpdatedAt` na modificação, e o
> valor sobrevive a uma releitura real do PostgreSQL — não bastaria não lançar exceção, porque
> `-infinity` também "funciona" sem erro). **49 testes automatizados no total** (22 Api + 27
> Infrastructure), todos passando.
>
> **Achado à parte, sem relação com código:** o cabeçalho `### E-04 · Catálogo — 11 pts` tinha
> desaparecido do arquivo — removido sem querer pela edição que registrou a conclusão de T-301 (a
> âncora do texto substituído incluía a linha do título, e o texto novo não a repôs). A tabela de
> T-401 a T-404 continuava presente, só sem o título da seção. Corrigido nesta sessão, ao notar a
> ausência ao navegar o arquivo para esta mesma nota — reforça por que revisar o `diff` antes de
> commitar, não só confiar que um `Edit` bem-intencionado preservou tudo ao redor.

> **T-304 concluída em 2026-08-10 (S010).** `IAuthorizationService`/`AuthorizationService`
> (`AppBridge.ControlPlane.Infrastructure/Authorization/`) — o componente `AuthorizationService`
> que `ARQUITETURA.md` §5.2 já documentava (`RF-007, RF-021, RF-039 | ADR-0004`), agora escrito.
> **Escopo confirmado antes de codificar**: `ROADMAP.md` não tem nenhuma outra tarefa para
> conceder/revogar permissão via API (`ApplicationPermission`/`UserGroupMembership` já existem
> completas desde T-202/T-204) — T-304 é só a lógica de decisão, testada manipulando linhas
> diretamente, não um endpoint administrativo (isso pertence a E-04/E-05, que ainda não começaram).
>
> Contrato deliberadamente mínimo — um único método, `HasActivePermissionAsync(userAccountId,
> applicationId)`, devolvendo `bool`, sem enum de motivo de negação — porque um contrato mais rico
> serviria só ao futuro endpoint `/launch` (E-05), que ainda não existe; construir para ele agora
> seria escopo além do que T-304 pede (RP-05). Isolamento entre tenants não é reimplementado aqui:
> `UserGroupMemberships` e `ApplicationPermissions` já são `DbSet`s com filtro por tenant (ADR-0004,
> T-203), então uma consulta cruzando tenants simplesmente não encontra nada, sem código especial
> para isso — mecanismo já provado por T-203/T-206, não re-testado nesta tarefa.
>
> **A leitura de vigência não usa cache** — `EffectiveFrom <= agora && (EffectiveTo == null ||
> EffectiveTo > agora)` é avaliada direto no banco a cada chamada — e é essa ausência de cache que
> torna o RNF-030 ("permissão revogada nega o lançamento seguinte em ≤ 60 s") verdadeiro por
> construção: os testes provam "nega na checagem imediatamente seguinte à revogação", sem precisar
> de espera de relógio nenhuma, porque não existe janela de staleness a cronometrar.
>
> **6 novos testes automatizados** em `AuthorizationServiceTests.cs`: permissão dentro da janela
> concede; revogar nega a checagem seguinte (a prova literal de V-07/RNF-030); nenhuma permissão
> nega; permissão de outro aplicativo não concede; permissão ainda não vigente (`EffectiveFrom` no
> futuro) nega; permissão já revogada no passado nega. **Verificado registrando `IAuthorizationService`
> no `Program.cs`** (ao lado de `IAuditWriter`/`ISessionTokenIssuer`) e subindo a aplicação real
> (`dotnet run`) para confirmar que a injeção de dependência resolve sem erro — sem consumidor ainda
> (isso é E-05), então não há endpoint para exercitar via `curl` nesta tarefa. **55 testes
> automatizados no total** (22 Api + 33 Infrastructure), todos passando.

> **T-302 concluída em 2026-08-10 (S010) — E-03 completo.** `MODELO-DE-DADOS.md` §4.1 já guardava
> `ad_object_sid` desde T-202 e já explicava por quê ("SID, não `sAMAccountName`: sobrevive a
> renomeação"), mas nenhum código lia, verificava ou atualizava esse campo — ele existia só como
> coluna. T-302 é o que faz o vínculo que ADR-0001 item 4 promete ("o vínculo... já existe desde o
> primeiro dia") funcionar de verdade dentro do fluxo de login.
>
> **Desenho:** `IdentityValidationResult` (`IIdentityProvider`) ganhou `Upn`/`DisplayName`
> opcionais — os valores atuais do diretório, lidos frescos a cada validação, não em cache.
> `AuthEndpoints.Login` agora chama `SyncDirectoryAttributes` depois de resolver o usuário: se o
> `Upn`/`DisplayName` que o provedor devolveu diverge do que está gravado, atualiza **a mesma
> linha**, na mesma transação que já grava `LastLoginAt`/`RefreshToken`. **`AdObjectSid` nunca é
> escrito por este caminho** — é `required` na provisão, MVP-0 não tem endpoint de provisão ainda
> (isso é E-04+), e é exatamente o campo que `MODELO-DE-DADOS.md` já documentava como imune a
> renomeação; sincronizar algo que uma renomeação legítima não muda seria inventar um mecanismo
> sem motivo (RP-05).
>
> **Por que não trocar a chave de busca do login para SID**: `ExternalSubject` (Entra `oid`) já é,
> por desenho do próprio Entra ID, estável a renomeação — ADR-0001 descreve os dois papéis como
> distintos (`external_subject` é "quem autentica no Control Plane"; `ad_object_sid` é "qual conta
> abre a sessão RDS"). Trocar a chave de resolução misturaria os dois papéis sem que nenhum
> requisito pedisse isso. Nenhuma verificação/negação de divergência de SID foi construída — `
> API.md` não documenta um código de erro para esse caso, e inventar um agora seria alterar o
> contrato de API sem que a tarefa pedisse (RA-06).
>
> `DevIdentityProvider` ganhou uma forma estendida de token —
> `dev:{externalSubject}:{adDomain}:{upn}:{displayName}` — que simula uma leitura fresca do
> diretório sem tocar no formato de três partes que todo teste anterior desta sessão já usa
> (`Split(':', 5)`; `parts.Length < 3` continua a única condição de invalidez, então tokens de 3
> partes continuam se comportando exatamente como antes).
>
> **Verificado rodando a aplicação de verdade** (`dotnet run` + `curl` + `psql`), o próprio cenário
> do critério de aceite: login, "renomeação" (segundo login com UPN/nome novos, mesmo
> `external_subject`/SID), e conferência direta no banco — **uma única linha** de `user_account`
> (mesmo `id`), `ad_object_sid` inalterado, `upn`/`display_name` atualizados, e os **dois**
> `access_event` de login (antes e depois da renomeação) apontando para o mesmo `user_account_id` —
> a trilha não quebrou.
>
> **2 novos testes** em `UserAccountSidLinkTests.cs`: renomear atualiza `Upn`/`DisplayName` na
> mesma linha sem duplicar conta e sem quebrar a trilha; um login sem atributos de diretório no
> token (forma curta) não altera o que já estava gravado. **57 testes automatizados no total** (24
> Api + 33 Infrastructure), todos passando. **Nenhum bug encontrado durante a verificação.** Com
> T-302, **E-03 · Identidade e autorização está com as 4 tarefas concluídas.**

### E-04 · Catálogo — 11 pts

| ID | Tarefa | Critério de aceite | Est. |
|----|--------|--------------------|------|
| ~~T-401~~ | ✅ Seed de aplicativos em JSON/tabela | Catálogo carregado sem painel (RF-012) | 3 |
| ~~T-402~~ | ✅ `GET /applications` com filtro por autorização | Aplicativo não autorizado **não aparece** (RF-011) | 3 |
| ~~T-403~~ | ✅ `ETag` / `If-None-Match` | Segunda sincronização devolve `304` (RF-015) | 2 |
| ~~T-404~~ | ✅ Endpoint de ícone | Serve PNG com cache; resolve PD-03 | 3 |

> **T-401 concluída em 2026-08-10 (S010).** `CatalogSeeder`
> (`AppBridge.ControlPlane.Infrastructure/Catalog/`) grava direto nas tabelas `application`/
> `host_pool` via EF Core — sem arquivo JSON separado, porque nada além do próprio seed leria um, e
> RF-012 trata "JSON ou tabela" como formas alternativas de um mesmo requisito ("sem painel
> administrativo"), não como exigência de as duas existirem. Dataset fixo do dogfood
> (`VISAO.md` §1/PA-01): Domínio Contábil e Alterdata, ambos `Published`, num único `HostPool`
> ("Pool Principal") criado sob demanda.
>
> **Sem endpoint HTTP novo, de propósito** — um verbo de CLI (`dotnet run -- seed-catalog
> <ad-domain>`) antes de `app.Run()`, não uma rota. Expor isso como endpoint seria, na prática, o
> começo do próprio painel administrativo que RF-012 diz que o MVP-0 não tem (esse painel é RF-043,
> MVP-1).
>
> **Idempotente por desenho** — verificado rodando duas vezes seguidas contra `appbridge_dev` real:
> a segunda chamada não duplica `application` nem `host_pool` (a checagem de existência usa o mesmo
> escopo do índice único `uq_application_tenant_alias_pool`, MODELO-DE-DADOS.md §5.1). Tenant
> desconhecido devolve código de saída `1` com mensagem no `stderr`, sem alterar nada no banco
> (verificado separadamente do `stdout`, já que `dotnet run` sempre devolve `0` quando encadeado
> num pipe — o próprio código de saída do `dotnet run` só reflete o do processo publicado quando
> lido sem pipe no meio).
>
> **Escopo deliberadamente não inclui** `IconRef` (isso é T-404, dono do endpoint de ícone) nem
> `ApplicationPermission`/`Group` (isso é T-402, que precisa desses dados como fixture do próprio
> teste de autorização, não como responsabilidade do seed de catálogo). RF-013 lista "grupo de
> permissão" entre os campos mínimos do catálogo, mas o critério de aceite de T-401 é
> especificamente "catálogo carregado sem painel" — os outros campos chegam com as tarefas que os
> usam, não antecipados aqui.
>
> **2 novos testes** em `CatalogSeederTests.cs`: `SeedAsync` popula os dois aplicativos, publicados,
> com `HostPoolId` válido, num único `HostPool`; rodar duas vezes não duplica nem `application` nem
> `host_pool`. **59 testes automatizados no total** (24 Api + 35 Infrastructure), todos passando.
> **Nenhum bug encontrado durante a verificação desta tarefa.**

> **T-402 concluída em 2026-08-10 (S010).** `GET /v1/applications` (API.md §3) — a primeira rota
> `[Authorize]` do Control Plane. Escopo restrito ao que a linha do `ROADMAP.md` pede: a listagem
> filtrada por autorização. `ETag`/`If-None-Match` (RF-015) é T-403; o endpoint de ícone (PD-03) é
> T-404; `GET /v1/applications/{id}` (detalhe) não tem tarefa própria no roadmap e não foi
> construído — `API.md` já o documenta, mas documentar não é mandato de implementar em toda tarefa
> da mesma seção (mesmo raciocínio de T-301 não ter implementado `/refresh` só porque `API.md` já o
> descrevia).
>
> **Peça de infraestrutura nova, não antecipada por nenhuma tarefa anterior**:
> `TenantResolutionMiddleware` (`Api/Middleware/`). Até aqui, todo endpoint resolvia
> `TenantContext.TenantId` consultando o banco dentro do próprio handler (login resolve por
> `Tenant.AdDomain`; refresh/logout, pelo hash do token) — não havia ainda uma rota que exigisse
> **um token de sessão já emitido** como a única fonte de tenant. A claim `tenant_id` já existe no
> JWT desde `JwtSessionTokenIssuer` (ADR-0017 §1); o middleware só lê essa claim e carimba
> `TenantContext` depois de `UseAuthentication()` e antes de `UseAuthorization()`/execução do
> endpoint — o comentário em `ITenantContext.cs`/`TenantContext.cs` já previa isso desde T-203
> ("T-301's auth middleware sets it early in the pipeline"), mas nenhuma tarefa antes de T-402 tinha
> uma rota que precisasse dele de verdade.
>
> **`IAuthorizationService` ganhou um segundo método**, `GetAuthorizedApplicationIdsAsync` — a forma
> em lote que `CatalogService` precisa (`ARQUITETURA.md` §4 desenha `CatalogService --> 
> AuthorizationService`), reaproveitando a mesma janela de vigência de `HasActivePermissionAsync`
> (T-304) em vez de duplicá-la. O endpoint ainda aplica seu próprio filtro de `Application.Status ==
> Published` por cima — um aplicativo pode estar autorizado e ainda não publicado.
>
> **Bug real encontrado durante a verificação manual — mas na minha própria semeadura via `psql`,
> não no código**: a primeira tentativa de popular `application`/`group`/`user_group_membership`
> manualmente usou literais numéricos (`status=1`, `launch_mode=0`, `source=0`) como se as colunas
> fossem inteiras — na verdade são `text`, porque o EF Core converte esses enums para string
> minúscula (`'published'`, `'remote_app'`, `'local'`). O resultado gravado foi a string `"1"`, que
> nunca bate com `a.status = 'published'` na consulta real — o catálogo respondia `200` com
> `items: []` mesmo com a permissão certa concedida. Diagnosticado comparando o SQL gerado pelo EF
> Core (log estruturado) com o dado gravado via `psql \d application` (revelou o tipo `text`), não
> com um `Assert` — os testes automatizados usam `DbContext.Applications.Add(...)`, então nunca
> passariam por esse valor errado; só a semeadura manual, fora do EF Core, expôs a discrepância entre
> "o que eu digitei" e "o que o conversor de enum realmente grava". Corrigido a mão no dado de
> verificação (não é bug de produção); registrado aqui porque é exatamente o tipo de erro que se
> repetiria em qualquer script de seed manual futuro fora do `CatalogSeeder`.
>
> **Verificado rodando a aplicação de verdade**: tenant/usuário/aplicativo/grupo/permissão semeados
> via `psql` (com o valor de enum corrigido), login real, `GET /v1/applications` sem token → `401`;
> com token → `Domínio Contábil` aparece, `Alterdata` (nunca autorizado) não aparece.
>
> **8 novos testes**: 3 em `AuthorizationServiceTests.cs` (`GetAuthorizedApplicationIdsAsync` —
> devolve só o autorizado vigente; exclui permissão revogada; usuário sem vínculo nenhum devolve
> vazio) e 5 em `CatalogEndpointTests.cs` (sem token → `401`; autorizado e publicado aparece;
> não autorizado não aparece; autorizado mas `Draft` não aparece; token de um tenant nunca vê
> aplicativo autorizado de outro tenant). **67 testes automatizados no total** (29 Api + 38
> Infrastructure), todos passando.

> **T-403 concluída em 2026-08-10 (S010).** `ETag`/`If-None-Match` em `GET /v1/applications`.
> **Decisão de desenho**: o `ETag` é um hash de conteúdo (`SHA256` truncado, prefixo `cat-`) sobre a
> própria lista de itens já materializada para aquele usuário — não um contador de versão mantido à
> parte. Razão: o catálogo de um usuário muda por dois motivos independentes — uma linha de
> `Application` muda, ou o conjunto de `ApplicationPermission` dele muda — e um contador de versão
> teria que ser atualizado corretamente nos dois casos sem nunca dessincronizar; hashear o resultado
> já materializado captura os dois de graça, porque é literalmente o que seria enviado.
>
> `If-None-Match` é comparado por igualdade de string exata contra o `ETag` calculado (mais o caso
> trivial `*`); sem suporte a validadores fracos (`W/"..."`) — não pedido, e o hash de conteúdo já é
> um validador forte por natureza.
>
> **Verificado rodando a aplicação de verdade** (`dotnet run` + `curl`): primeira requisição devolve
> `200` com `ETag: "cat-4fdb3c35dee7d96f"` e o corpo esperado; segunda requisição com
> `If-None-Match` igual devolve `304 Not Modified`, mesmo `ETag`, **corpo de 0 bytes** (`curl -w
> "%{size_download}"` confirmou).
>
> **3 novos testes** em `CatalogEndpointTests.cs`: primeira requisição tem `ETag`, segunda idêntica
> devolve `304`; `If-None-Match` desatualizado devolve `200` com o catálogo atual; conceder uma nova
> permissão (sem tocar em nenhuma linha de `Application`) muda o `ETag` e o `If-None-Match` antigo
> volta a devolver `200` com o catálogo atualizado — prova direta de que o `ETag` reflete permissão,
> não só conteúdo de aplicativo. **70 testes automatizados no total** (32 Api + 38 Infrastructure),
> todos passando. **Nenhum bug encontrado durante a verificação desta tarefa.**

> **T-404 concluída em 2026-08-10 (S010) — E-04 · Catálogo completo.** `GET
> /v1/applications/{id}/icon` (API.md §3, resolve PD-03). `IIconStorage`/`FileSystemIconStorage`
> (`AppBridge.ControlPlane.Infrastructure/Catalog/`) resolvem `Application.IconRef` para bytes sob
> um diretório raiz configurado (`APPBRIDGE_ICON_STORAGE_PATH`) — PD-03 já estava "resolvida" em
> `API.md` (sistema de arquivos, não banco), mas nenhuma peça de código ainda existia para isso;
> T-404 é essa peça.
>
> **Proteção contra travessia de caminho** (`../../etc/passwd`-style) no `FileSystemIconStorage`:
> `icon_ref` é hoje definido só pelo `CatalogSeeder` (administrador, não entrada de usuário), mas
> resolver sob a raiz configurada e rejeitar qualquer caminho que escape dela custa duas linhas e
> fecha a superfície antes de ela existir de verdade, não depois.
>
> **Mesma técnica de `ETag` de conteúdo de T-403**, agora sobre os bytes do ícone — dois arquivos
> idênticos hasheiam igual, então não há rastreamento de "mudou?" separado do próprio arquivo.
> `Cache-Control: public, max-age=604800, immutable` (uma semana): o hash de conteúdo já é a
> verificação de frescor real, então uma janela longa não custa nada que o cliente não devesse já
> estar aproveitando via `If-None-Match`.
>
> **Sem filtro de autorização (RF-011) neste endpoint, deliberado**: um ícone é metadado de
> apresentação, não o aplicativo em si, e a seção de `API.md` que o documenta não pede o filtro que
> `GET /v1/applications` aplica à listagem. Isolamento entre tenants continua automático (filtro
> global do `DbContext`, ADR-0004) — um `id` de outro tenant simplesmente não é encontrado, `404`
> igual a um `id` inexistente (mesmo raciocínio anti-enumeração de ADR-0012 §5).
>
> **`CatalogSeeder` (T-401) atualizado** para preencher `IconRef` com os dois ícones do dogfood
> (`dominio-contabil.png`, `alterdata.png`) — deixado em aberto de propósito em T-401, "isso é T-404,
> dono do endpoint de ícone". Os dois arquivos são placeholders PNG 64×64 gerados nesta sessão
> (`assets/catalog-icons/`), sem dependência de Pillow/ImageMagick — construídos por codificação
> manual dos chunks PNG (`IHDR`/`IDAT`/`IEND`) via `zlib` da biblioteca padrão do Python. **Não são a
> identidade visual final** — isso é decisão de produto para quando o painel (RF-043, MVP-1) existir.
>
> **`ApiTestFactory` ganhou uma terceira variável obrigatória** (`APPBRIDGE_ICON_STORAGE_PATH`),
> apontando para um diretório temporário próprio por execução de teste, com os dois PNGs do seed
> escritos nele — autocontido, sem depender do diretório de trabalho do processo de teste coincidir
> com o layout do repositório.
>
> **Verificado rodando a aplicação de verdade** (`dotnet run -- seed-catalog` + `curl` + `diff`):
> catálogo semeado, permissão concedida, ícone buscado — os bytes devolvidos batem **byte a byte**
> (`diff`) com o arquivo original em `assets/catalog-icons/dominio-contabil.png`; segunda requisição
> com o mesmo `If-None-Match` devolve `304` com 0 bytes; `id` inexistente devolve `404`.
>
> **11 novos testes**: 4 em `FileSystemIconStorageTests.cs` (lê bytes de um arquivo sob a raiz;
> `null` para arquivo inexistente; recusa travessia de caminho relativa e absoluta) — sem PostgreSQL,
> é comportamento puro de sistema de arquivos — e 7 em `CatalogIconEndpointTests.cs` (sem token →
> `401`; serve PNG com `ETag`/`Cache-Control`; segunda requisição idêntica → `304` sem corpo;
> aplicativo sem `IconRef` → `404`; `IconRef` que não resolve a arquivo → `404`; `id` desconhecido →
> `404`; token de outro tenant não lê o ícone → `404`). **81 testes automatizados no total** (39 Api
> + 42 Infrastructure), todos passando. **Nenhum bug encontrado durante a verificação desta tarefa**
> (um bug de traversal foi *prevenido* no desenho, não encontrado depois). **Com T-404, E-04 ·
> Catálogo está completo — as 4 tarefas concluídas.**

### E-05 · Lançamento — 32 pts · **coração do produto**

| ID | Tarefa | Critério de aceite | Est. |
|----|--------|--------------------|------|
| ~~T-501~~ | ✅ `RdpDescriptorBuilder` aplicando a política de redirecionamento | `.rdp` gerado nega unidades locais e permite impressora (ADR-0008) | 5 |
| ~~T-502~~ | ✅ `IRdpFileSigner` + `RdpSignExeSigner` | `.rdp` assinado e aceito pela estação; **falha de assinatura devolve `503`** (V-06, RNF-002, ADR-0009) | 8 |
| ~~T-503~~ | ✅ `ISessionBackend` + `RdsSessionBackend` (resolução de host e descritor) | Nenhuma regra de negócio referencia tipo do RDS (RNF-035) | 8 |
| ~~T-504~~ | ✅ `POST /launches` com autorização, trilha e `Idempotency-Key` | Repetir a chave não cria segundo lançamento nem segunda contagem (ADR-0012 §3) | 5 |
| ~~T-505~~ | ✅ Catálogo de erros com códigos estáveis | Cada situação da tabela de `API.md` §9 devolve o código correto | 3 |
| ~~T-506~~ | ✅ `CancelSessionAsync` em `ISessionBackend`/`RdsSessionBackend` — operação construída e testada; **wiring no caminho de falha do prelaunch fica para T-601** (ver nota abaixo) | Prelaunch que falha após criar a sessão **não deixa sessão contando licença**; teste força a falha (ADR-0016, Gap 2) — **metade construída agora, metade aguarda T-601** | 3 |

> **Redirecionamento de E-01 para T-501 em 2026-08-10/11 (S010), registrado por transparência.**
> Frederico pediu "segue com E-01" — mas E-01 é infraestrutura física/operacional (comprar host,
> instalar Hyper-V/AD DS/RDS reais, configurar GPOs, ingressar estações, rodar varredura externa
> contra o IP público do escritório): nada disso é executável a partir deste ambiente de
> desenvolvimento Linux em sandbox. As especificações (`docs/operacao/E-01-infraestrutura/`) já
> estavam prontas desde S005 — não havia nada de documentação para revisar sem informação nova. Após
> pergunta de esclarecimento ("o que fazer, já que não posso executar E-01 daqui?"), a resposta foi
> "você decide"; escolhi seguir com **T-501** — a primeira tarefa de E-05 que não depende de
> `IRdpFileSigner` (T-502) nem `ISessionBackend` (T-503), nenhum dos dois construído ainda.
>
> **T-501 concluída.** `IRdpDescriptorBuilder`/`RdpDescriptorBuilder`
> (`AppBridge.ControlPlane.Infrastructure/Rdp/`) — puro e sem estado, sem I/O: monta o texto do
> `.rdp` **não assinado** a partir de `RdpConnectionParameters` (host, alias do RemoteApp, nome de
> exibição), a informação mínima que um futuro `ISessionBackend.BuildConnectionDescriptorAsync`
> (T-503) forneceria. Cada linha de redirecionamento traça direto para uma linha da tabela de
> ADR-0008: impressora e token/smart card A3 permitidos (`redirectprinters`, `redirectsmartcards`),
> área de transferência bidirecional permitida (`redirectclipboard`), unidades locais e demais
> Plug-and-Play negados (`drivestoredirect`/`devicestoredirect` vazios), portas COM negadas
> (`redirectcomports`), áudio de saída permitido e entrada negada (`audiomode`/`audiocapturemode`).
> Nenhuma propriedade além dessas foi incluída — não inventei configuração que ADR-0008 não decidiu.
>
> **Namespace `AppBridge.ControlPlane.Infrastructure.Launch` colidia com a entidade `Launch`**
> (`Domain.Trail.Launch`, de T-202): o compilador resolvia `DbSet<Launch>` em `AppBridgeDbContext`
> para o namespace novo em vez do tipo, porque um namespace aninhado do mesmo nome dentro do
> namespace-pai (`Infrastructure`) sombreia um tipo de fora sem precisar de `using`. Renomeado para
> `AppBridge.ControlPlane.Infrastructure.Rdp` antes de qualquer commit — build limpo confirmou.
>
> **10 novos testes** em `RdpDescriptorBuilderTests.cs`, um por propriedade (nega unidade local,
> permite impressora, permite smart card, permite área de transferência, nega COM, permite áudio de
> saída, nega áudio de entrada, nega PnP genérico, monta RemoteApp com alias/nome corretos, usa
> `\r\n` mesmo rodando em Linux — o formato `.rdp` é lido por um cliente Windows independentemente do
> SO que o gerou). Registrado em `Program.cs` (`Singleton`, sem estado, mesmo padrão de
> `ISessionTokenIssuer`) e verificado subindo a aplicação real sem erro de resolução de DI — sem
> consumidor ainda (T-504). **91 testes automatizados no total** (39 Api + 52 Infrastructure), todos
> passando.

> **T-502 concluída em 2026-08-11 (S010), aprovada como "interface + fake testável" — decisão
> explícita, não invenção de escopo.** `rdpsign.exe` (ADR-0009) é um executável Windows real que
> não existe nesta sandbox Linux, a mesma classe de limitação já registrada para E-01. Perguntei
> antes de codificar; a resposta foi construir a interface e uma implementação real
> (`RdpSignExeSigner`), testadas contra um **fake controlado**, não contra o binário verdadeiro —
> deixando explícito, em código e documentação, que a assinatura real só pode ser verificada com um
> host Windows (E-01).
>
> `IRdpFileSigner`/`RdpSignExeSigner` (`AppBridge.ControlPlane.Infrastructure/Rdp/`) seguem
> ADR-0009 item 1 à risca: uma única responsabilidade, receber o `.rdp` e devolver o conteúdo
> assinado — nenhuma outra parte do sistema sabe como a assinatura acontece. Implementação: grava o
> conteúdo num arquivo temporário, invoca `rdpsign.exe /sha256 <thumbprint> <arquivo>` num processo
> separado, com timeout, lê o arquivo de volta (assinado in-place, como o `rdpsign` real opera) e
> sempre apaga o temporário — sucesso ou falha. Falha (exit code ≠ 0, processo que não inicia, ou
> timeout) vira `RdpSigningFailedException`, nunca um resultado degradado (RNF-002) — o mapeamento
> para `503 SIGNING_UNAVAILABLE` de `API.md` é responsabilidade de quem chamar isso (T-504), do
> mesmo jeito que `AuditWriteFailedException` já funciona para `AuditWriter`.
>
> **Os testes usam três scripts `fake-rdpsign-*.sh`** (`tests/.../fixtures/`) que imitam o contrato
> de linha de comando do `rdpsign.exe` real (`/sha256 <thumbprint> <arquivo>`) sem serem ele:
> sucesso (grava uma linha com o thumbprint recebido, prova que o argumento certo chegou), falha
> (código de saída 1, mensagem em `stderr`) e travamento (`sleep 30`, prova que o timeout mata o
> processo em vez de travar o chamador). O que isso verifica é a orquestração do processo — não a
> validade de uma assinatura RDP real, que nenhum teste aqui pode provar.
>
> `APPBRIDGE_RDP_SIGNING_THUMBPRINT` (obrigatória) e `APPBRIDGE_RDPSIGN_PATH` (opcional, padrão
> `rdpsign.exe` via `PATH`) registradas em `Program.cs`; `ApiTestFactory` ganhou uma quarta variável
> de ambiente obrigatória com um valor fixo de teste (nenhum teste de Api ainda chama
> `IRdpFileSigner` — isso é T-504).
>
> **4 novos testes** em `RdpSignExeSignerTests.cs`: assinatura bem-sucedida devolve o conteúdo com o
> thumbprint correto e não deixa arquivo temporário para trás; código de saída não-zero vira
> `RdpSigningFailedException` com o `stderr` na mensagem; processo travado é morto e reportado como
> timeout em bem menos que o `sleep 30` do fake (prova que o timeout de verdade funciona, não só que
> existe no código); executável inexistente vira `RdpSigningFailedException` na inicialização.
> **Verificado subindo a aplicação real** com a variável nova definida — DI resolve sem erro.
> **95 testes automatizados no total** (39 Api + 56 Infrastructure), todos passando. **Nenhum bug
> encontrado durante a verificação desta tarefa.**

> **T-503 concluída em 2026-08-13 (S010) — correção de rota registrada antes de codificar, não
> depois.** A resposta anterior desta sessão presumiu que T-503 precisaria do mesmo padrão de
> "interface + fake" de T-502, por falar com "um Connection Broker real". Ao reler o escopo literal
> do `ROADMAP.md` — **"resolução de host e descritor"**, não a interface `ISessionBackend` inteira —
> ficou claro que essa suposição estava errada: escolher um `SessionHost` e montar os parâmetros de
> conexão são operações de **leitura da nossa própria tabela `session_host`** (T-204), não uma
> chamada a um Connection Broker real. Não existe, para este escopo específico, nenhuma dependência
> Windows a contornar. Corrigido antes de escrever qualquer código — a aprovação do usuário ("mesmo
> padrão") foi para a estratégia de lidar com dependência inexistente nesta sandbox, e essa
> dependência simplesmente não existe para T-503 como o `ROADMAP.md` o escopa.
>
> `ISessionBackend`/`RdsSessionBackend`
> (`AppBridge.ControlPlane.Infrastructure/Sessions/`) — **apenas os dois membros que o critério de
> aceite de T-503 pede**: `ResolveHostAsync(applicationId)` (escolhe um `SessionHost`
> `Online` no `HostPool` do aplicativo, ordenado por `Id` — UUID v7 ordena por criação, ADR-0011,
> escolha determinística sem precisar de dado de carga real) e
> `BuildConnectionDescriptorAsync(host, application)` (produz exatamente o
> `RdpConnectionParameters` que `IRdpDescriptorBuilder.Build` de T-501 consome — a costura entre as
> duas tarefas). Os demais membros que `ARQUITETURA.md` §4.2 lista (`ListActiveSessionsAsync`,
> `CancelSessionAsync`, `TerminateSessionAsync`, `PublishApplicationAsync`, `GetHostHealthAsync`)
> pertencem a tarefas que ainda não começaram (T-506, T-601/602, MVP-1, V2) e entram na interface
> quando cada uma precisar — mesmo padrão de extensão que T-402 aplicou a
> `IAuthorizationService` (T-304 já tinha deixado o contrato deliberadamente mínimo). Construir
> stubs para trabalho de meses à frente seria escopo inventado (RP-05).
>
> **Sem tenant explícito nas assinaturas** — mesmo formato de `IAuthorizationService`: isolamento
> automático pelo filtro global do `DbContext` (ADR-0004), já que `Application`/`SessionHost` são
> ambos `TenantScopedEntity`. Sem parâmetro de usuário em `ResolveHostAsync`, mesmo
> `ARQUITETURA.md` §4.2 desenhando `ResolveHostAsync(tenant, user, app)` no C4: afinidade de sessão
> por usuário não existe ainda (isso é `SessionRegistry`, T-601) e nada em T-503 o usaria — adicionar
> um parâmetro morto por fidelidade literal ao diagrama seria pior do que estendê-lo quando T-601
> precisar dele de verdade.
>
> **6 novos testes** em `RdsSessionBackendTests.cs`, todos contra PostgreSQL real, sem fake: resolve
> um host `Online` do pool certo; exclui `Draining`/`Offline`; escolhe o host mais antigo
> deterministicamente quando há mais de um elegível; nenhum host elegível devolve `null`; aplicativo
> desconhecido devolve `null`; o descritor de conexão monta exatamente os campos que
> `RdpConnectionParameters` espera. **Verificado subindo a aplicação real** — DI resolve sem erro.
> **101 testes automatizados no total** (39 Api + 62 Infrastructure), todos passando. **Nenhum bug
> de produção encontrado** — a única correção desta tarefa foi de escopo, feita antes de escrever
> código, não um bug encontrado depois.

> **T-504 concluída em 2026-08-13 (S010) — "coração do produto" (ARQUITETURA.md §5.2), E-05 tem
> agora seu endpoint central.** `POST /v1/launches` é o primeiro consumidor real, junto, de
> `IAuthorizationService` (T-304), `ISessionBackend` (T-503), `IRdpDescriptorBuilder` (T-501) e
> `IRdpFileSigner` (T-502) — as quatro peças que as quatro tarefas anteriores de E-05 construíram
> isoladamente se encontram aqui pela primeira vez.
>
> **PD-04 resolvida** ("onde ficam as respostas de idempotência durante os 60 s de validade",
> `API.md` §11, pendência aberta desde a fase de design): **memória em processo**, não tabela nem
> cache distribuído. Justificativa: o MVP-0/dogfood roda uma única instância do Control Plane,
> coabitando o session host (ADR-0002) — não existe uma segunda instância que pudesse perder o que a
> primeira gravou. Essa premissa quebra no dia em que o Control Plane rodar em mais de uma
> instância (V2+); revisar então, atrás da mesma interface (`IIdempotencyStore`), não antes.
>
> **Toda saída de `POST /v1/launches` — concedida ou negada, por qualquer motivo — grava uma linha
> em `Launch` via `IAuditWriter`.** RF-037 está na lista bloqueante do Part 1 de ADR-0007 (junto com
> RF-036/RF-039/RF-041/RF-042) — mesma disciplina de "um único caminho de código, nenhum ramo que
> possa esquecer a garantia transacional" que T-301 já tinha estabelecido para negativas de login.
> **A única exceção deliberada é `AUDIT_UNAVAILABLE` em si**: quando a própria gravação da trilha
> falha, nada foi persistido — não há o que uma repetição pudesse duplicar — então essa é a única
> resposta que **não** entra no cache de idempotência: a próxima tentativa do cliente deve tentar de
> novo de verdade, não receber de volta uma falha transitória congelada por 60 s.
>
> **Réplica byte a byte, não apenas equivalente.** O cache de idempotência guarda a resposta exata
> já serializada (corpo + status + content-type), não um sinalizador "já processado" que reconstrói
> a resposta na hora — reconstruir custaria uma segunda assinatura real (chamada de processo
> desperdiçada) e poderia produzir bytes ligeiramente diferentes do que o cliente já recebeu.
> `LaunchProblems` devolve um record simples (`LaunchProblemBody`), não
> `Microsoft.AspNetCore.Mvc.ProblemDetails` como `AuthProblems` — a serialização manual que o cache
> exige não passa de forma confiável pelo conversor específico de `ProblemDetails` que
> `Results.Problem` usa por baixo dos panos, então este endpoint serializa toda resposta (sucesso ou
> erro) pelo mesmo caminho próprio, garantindo que o que foi cacheado e o que seria gerado ao vivo
> são idênticos por construção.
>
> **Escopo deliberadamente restrito ao que o critério de aceite pede**: `sessionReused` sempre
> `false` (não existe rastreamento real de sessão — isso é `SessionRegistry`, T-601); `host.
> displayName` é a string genérica fixa `"Servidor de aplicativos"`, igual ao exemplo de `API.md`,
> nunca o FQDN real (RNF-043 proíbe vazar detalhe interno); sem verificação de teto de licença
> (`409 QUOTA_EXHAUSTED` é MVP-1, ADR-0006) nem de limite de taxa (`429 RATE_LIMITED`, RNF-010,
> infraestrutura que não existe). Nenhum dos dois nunca será emitido por este código ainda — honesto
> por construção, não por omissão silenciosa.
>
> **Bug real pego rodando a suíte inteira, não só os testes novos** — reforça, de novo, por que
> `dotnet test` sem filtro é o passo que fecha cada tarefa, não `dotnet test --filter`. A primeira
> versão de `ApiTestFactory` ganhou um parâmetro opcional (`string? rdpSignExecutable = null`) para
> as duas tarefas escolherem o executável de assinatura fake — compilou limpo, os testes novos
> passaram. Só ao rodar a suíte completa, `HealthCheckTests`/`CorrelationIdMiddlewareTests` (que
> usam `IClassFixture<ApiTestFactory>`) quebraram: o xUnit instancia um tipo usado como fixture de
> classe por reflexão e exige um construtor **verdadeiramente sem parâmetros** — um valor padrão em
> C# não conta. Corrigido para dois construtores (um sem parâmetro nenhum, outro com); isso também
> quebrou ("só pode haver um único construtor público"), porque o xUnit exige exatamente **um**
> construtor público no tipo inteiro, não zero-ou-mais com uma forma aceitável. Solução final: um
> único construtor público sem parâmetro, e um método estático `ApiTestFactory.WithRdpSigner(...)`
> chamando um construtor **privado** por trás — mantém exatamente um construtor público, satisfaz o
> xUnit, e ainda permite ao teste de falha de assinatura pedir um executável diferente.
>
> **Verificado rodando a aplicação de verdade** (`dotnet run` + `curl` + `psql`): tenant, usuário,
> host `online`, aplicativo publicado e permissão semeados; login real; primeiro `POST /v1/launches`
> devolve `201` com um `.rdp` que, decodificado, mostra a política de ADR-0008 linha por linha e a
> marca do assinador fake; **segunda requisição com a mesma `Idempotency-Key` devolve resposta
> idêntica byte a byte** (`diff` confirmou) e **a tabela `launch` continua com exatamente 1 linha**
> — a prova literal do critério de aceite; terceira requisição, mesma chave e corpo diferente,
> devolve `409 IDEMPOTENCY_CONFLICT`. Dados de verificação limpos do banco ao final.
>
> **13 novos testes** em `LaunchEndpointTests.cs` (sem token → `401`; sem `Idempotency-Key` → `400`;
> lançamento concedido devolve `.rdp` assinado e grava `Launch(Outcome=Granted)`; repetir a mesma
> chave e corpo devolve resposta idêntica e só uma linha; repetir com corpo diferente devolve `409`
> sem segunda linha; sem permissão devolve `403` e grava `DeniedPermission`; aplicativo desconhecido
> ou não publicado devolve `404` sem gravar linha nenhuma — o FK composto nem deixaria; sem host
> `Online` devolve `422` e grava `DeniedHostUnavailable`; `purpose` inválido devolve `400` sem
> gravar; falha de assinatura — via a fábrica `WithRdpSigner` apontando para o fixture que falha —
> devolve `503` e grava `ErrorSigning`; `purpose=prelaunch` é gravado corretamente). **113 testes
> automatizados no total** (51 Api + 62 Infrastructure), todos passando.

> **T-506 concluída em 2026-08-13 (S010) — com uma lacuna de escopo real, registrada por
> transparência, não uma correção limpa como a de T-503.** O texto original do Gap 2
> (`ANALISE_BUGS_E_MELHORIAS.md`, revisão S008) descreve a correção como "chamar
> `CancelSessionAsync` ao detectar falha de prelaunch em `StartSessionAsync`" e testar que
> "`SessionReconciler` limpa em < 60 s". **Nenhum dos dois existe hoje**: não há
> `StartSessionAsync` nem qualquer outro ponto do código que crie uma linha `Session` — T-504
> deixou `Launch.SessionId` deliberadamente nulo, adiado para T-601 (`SessionRegistry`), e o
> `Session` do `MODELO-DE-DADOS.md` §6.2 segue com zero linhas em todo o codebase construído até
> aqui. A sessão RDS de verdade é criada do lado do cliente (`mstsc` conectando), de forma
> assíncrona, **depois** de o Control Plane já ter respondido `POST /v1/launches` (ARQUITETURA.md
> §5.3) — não existe, na arquitetura atual, nenhum ponto síncrono no lado do Control Plane onde
> "a sessão foi criada e então algo falhou" seja um estado observável.
>
> Isso não é a mesma situação de T-503 (onde reler o escopo revelou que a tarefa **não precisava**
> de uma dependência): aqui a tarefa **precisa** de uma dependência real — rastreamento de criação
> de sessão — que só T-601 introduz. Construir uma criação de `Session` sintética dentro de
> `POST /v1/launches` só para T-506 ter algo para cancelar seria escopo de T-601 antecipado sem
> ADR, e inventaria uma sequência ("cria sessão → falha → cancela") que a arquitetura aprovada não
> tem hoje.
>
> **Decisão**: construir a operação em si, de verdade e testada — não adiar tudo. `CancelSessionAsync(sessionId, reason)` entra em `ISessionBackend`/`RdsSessionBackend`
> (`AppBridge.ControlPlane.Infrastructure/Sessions/`): marca `Session.EndedAt`/`EndReason` (não
> passa por `IAuditWriter` — `IAuditWriter`'s próprio comentário exclui explicitamente o fim de
> sessão, RF-038, do seu escopo transacional; `EndedAt`/`EndReason` já *é* o registro durável).
> Idempotente: cancelar uma sessão já encerrada é no-op, não erro — dois chamadores concorrentes
> (esta operação e o futuro `SessionReconciler`, T-602) não podem lançar exceção um no outro.
> Lança `SessionNotFoundException` (novo tipo) para id desconhecido no tenant atual. **O que fica
> de fora**: a chamada dentro do caminho de falha de `POST /v1/launches` — isso move para o
> critério de aceite de T-601 (linha acima), o único lugar que vai ter uma sessão de verdade para
> cancelar.
>
> **5 novos testes** em `RdsSessionBackendTests.cs`, contra PostgreSQL real: cancela sessão ativa e
> grava `EndedAt`/`EndReason` corretos; cancelar sessão já encerrada é no-op e não sobrescreve o
> motivo original (simula a corrida com `SessionReconciler`); id desconhecido lança
> `SessionNotFoundException`. **Verificado com a suíte completa, não filtrada** — **116 testes
> automatizados no total** (51 Api + 65 Infrastructure), todos passando. Nenhum bug de produção
> encontrado; a única coisa incomum desta tarefa foi a lacuna arquitetural em si, já presente
> desde que o Gap 2 original foi escrito (S008) e agora documentada explicitamente em vez de
> escondida atrás de um wiring fabricado.

> **T-505 concluída em 2026-08-13 (S010) — e um bug de produção real encontrado, não só um
> critério de aceite conferido.** Verificar "cada situação da tabela de `API.md` §9 devolve o
> código correto" significou primeiro descobrir quais situações já existiam sem passar pela tabela
> nenhuma. Rodando a aplicação real (`dotnet run`) e mandando corpo malformado para
> `POST /v1/auth/session`: a resposta trazia o **stack trace .NET inteiro**, incluindo caminho
> absoluto de arquivo-fonte, dentro do corpo JSON devolvido ao cliente — violação direta de
> RNF-043 ("nenhuma resposta de erro contém... exceção"). Isso não é só um problema de ambiente de
> teste: `IIdentityProvider` só tem implementação sob `Development` (ADR-0017 §5, nenhuma real
> existe — bloqueado por E-01), então a instância real do dogfood **também roda em Development** e
> **também vazava isso**. Achados, todos verificados subindo a aplicação real com `curl`, não só
> inferidos do código:
>
> 1. **Corpo malformado ou campo obrigatório ausente** em qualquer endpoint `POST` — stack trace
>    completo no corpo, `400` sem `appbridgeCode`. Corrigido com `GlobalExceptionHandler`
>    (`IExceptionHandler`, `Api/Middleware/`): `BadHttpRequestException` (o que o *binding* de
>    corpo do minimal API lança) vira `400 MALFORMED_REQUEST`; qualquer outra exceção vira
>    `500 INTERNAL_ERROR`. A exceção real vai para o log estruturado via `ILogger`, com o mesmo
>    `correlationId` que o chamador recebe — RNF-043 cumprido sem perder a rastreabilidade que
>    RNF-039 exige. Registrado em `Program.cs` via `AddExceptionHandler`/`AddProblemDetails` +
>    `app.UseExceptionHandler()` logo após `CorrelationIdMiddleware` — isso também **suprime** a
>    página de exceção automática do ASP.NET Core em Development, que era a origem literal do
>    vazamento.
> 2. **Token ausente, inválido ou expirado em qualquer rota `[Authorize]`** (`GET /v1/applications`,
>    `.../icon`, `POST /v1/launches`) devolvia `401` **sem corpo nenhum** — nenhum `appbridgeCode`,
>    nenhum `correlationId`, nada que distinguisse "nunca autenticou" de "token expirou" de
>    qualquer outro motivo. `API.md` §9 já documentava `401 SESSION_EXPIRED` para exatamente essa
>    situação; faltava implementá-la. Corrigido com `JwtBearerEvents.OnChallenge` (`Program.cs`),
>    escrevendo o mesmo formato `ProblemDetails` que `AuthProblems`/`GlobalProblems` usam em
>    qualquer outro lugar.
> 3. **`GET /v1/applications/{id}/icon`** devolvia `Results.NotFound()` puro (sem corpo) para
>    aplicativo inexistente/sem ícone — mesma falta de `appbridgeCode`/`correlationId`. Corrigido com
>    `CatalogProblems.ApplicationNotFound`, mesma forma de `ProblemDetails` de `AuthProblems`.
> 4. **`LaunchProblems.InvalidPurpose` usava o código `INVALID_PURPOSE`, que não existe em nenhum
>    lugar de `API.md` §9.** O catálogo se declara "chave estável" (§9, abertura) — um código que
>    não está nele quebra essa promessa tanto quanto um código documentado que nunca é devolvido.
>    `purpose` fora de `user_initiated`/`prelaunch` é exatamente a situação genérica que o catálogo
>    já cobre (`400 MALFORMED_REQUEST`, "corpo inválido... erro de programação") — renomeado para
>    reusar o código existente em vez de manter um inventado.
>
> **Três novas classes** em `Api/Endpoints/`: `GlobalProblems` (`MALFORMED_REQUEST`,
> `SESSION_EXPIRED`, `INTERNAL_ERROR` — situações que não pertencem a um grupo de endpoint só) e
> `CatalogProblems` (`APPLICATION_NOT_FOUND` para `/v1/applications/*`), seguindo a mesma forma de
> `AuthProblems` (T-301/T-303). Códigos do catálogo que continuam **legitimamente fora do MVP-0**,
> não esquecidos: `PROVIDER_ROLE_REQUIRED` (RF-075, papel de provedor, MVP-1), `QUOTA_EXHAUSTED`
> (ADR-0006, MVP-1), `RETENTION_BELOW_MINIMUM`/`EXCEPTION_REASON_REQUIRED` (painel admin, MVP-1),
> `RATE_LIMITED` (RNF-010, sem infraestrutura de limite de taxa — mesma decisão já registrada em
> T-504) e `DIRECTORY_UNAVAILABLE` (só teria sentido com um `IIdentityProvider` real, que não existe
> — `DevIdentityProvider` não modela "diretório fora do ar", só "token válido ou não").
>
> **8 testes novos/fortalecidos** em `Api.Tests`: `GlobalExceptionHandlerTests.cs` (3, unitários
> contra o handler diretamente — nada no projeto hoje lança uma exceção não prevista para um teste
> HTTP de ponta a ponta provocar; prova que uma mensagem de exceção com segredo simulado não
> aparece no corpo, que `BadHttpRequestException` vira `400`, e que a ausência de correlationId no
> contexto não derruba o handler); um teste novo em `LaunchEndpointTests.cs` (corpo malformado em
> `POST /v1/launches` → `400 MALFORMED_REQUEST` sem stack trace no corpo — o teste que reproduz o
> bug real encontrado); testes existentes em `CatalogEndpointTests.cs`, `CatalogIconEndpointTests.cs`
> e `LaunchEndpointTests.cs` fortalecidos para verificar `appbridgeCode`, onde antes só verificavam
> o status HTTP. **120 testes automatizados no total** (55 Api + 65 Infrastructure), todos passando.
> **Com T-505, E-05 · Lançamento está completo — as 6 tarefas concluídas.**

### E-06 · Sessão e reconciliação — 16 pts

| ID | Tarefa | Critério de aceite | Est. |
|----|--------|--------------------|------|
| ~~T-601~~ | ✅ `SessionRegistry` — início, reutilização e vínculo com o lançamento | Segundo aplicativo reutiliza a sessão (RF-024) | 5 |
| ~~T-602~~ | ✅ `SessionReconciler` — **apenas a defesa `stale_expired`** (inatividade); `reconciled_missing` (consulta ao Connection Broker) fica para o MVP-1, por decisão já tomada em ADR-0006, não por limitação desta sandbox (ver nota abaixo) | Sessão sem `LastSeenAt` recente é fechada em até um ciclo (`stale_expired`) — a metade de PRE-23/R-009 que não depende do Connection Broker; a metade `reconciled_missing` permanece aberta até o MVP-1 | 8 |
| ~~T-603~~ | ✅ `GET /sessions/me` | Launcher exibe sessões ativas | 3 |

> **T-601 concluída em 2026-08-13 (S010) — e uma segunda correção sobre a nota que a própria sessão
> deixou no fim de T-506, feita ao modelar a transação antes de escrever código, não depois.**
>
> A nota de T-506 tinha atribuído a T-601 "chamar `CancelSessionAsync` no caminho de falha do
> prelaunch", supondo que construir `SessionRegistry` criaria uma janela síncrona onde uma sessão
> gravada pudesse ficar órfã se algo falhasse logo em seguida. Modelando a transação de verdade,
> essa suposição não se sustentou: ARQUITETURA.md §5.2 mostra "`SessionRegistry` + auditoria do
> lançamento" como **um único passo** no diagrama de sequência (a mesma transação, não dois). Segui
> essa leitura literalmente — `ISessionRegistry.RegisterAsync` só lê e marca mudanças rastreadas
> (nunca chama `SaveChangesAsync`), e `LaunchEndpoints` grava tudo — `Session` novo ou reutilizado
> **e** `Launch` — num único `SaveChangesAsync`, dentro do `grant` que `IAuditWriter.ExecuteAsync`
> já comita atomicamente (T-504). Se a gravação falhar (`AUDIT_UNAVAILABLE`), a mudança de `Session`
> rastreada nem chega a existir no banco — não há "sessão criada, lançamento falhou depois" possível
> **dentro de uma única requisição**, porque não há mais de uma gravação para uma falhar entre elas.
>
> A situação real que ADR-0016 Gap 2 descreve — uma sessão que este banco registra como ativa, mas
> que o `mstsc` do cliente nunca chegou a estabelecer de verdade no RDS (falha de rede, host que
> caiu depois de `ResolveHostAsync` tê-lo marcado `Online`, etc.) — acontece **depois** de o Control
> Plane já ter respondido (ARQUITETURA.md §5.2: "o Control Plane sai do caminho assim que o `mstsc`
> conecta"), de forma inteiramente assíncrona e do lado do cliente. `API.md` já é explícito que o
> cliente nunca é fonte da verdade sobre fim de sessão — não existe endpoint para ele avisar, e não
> deveria existir um agora só para isso. **Não há nada que `POST /v1/launches` possa observar
> sincronamente para essa falha específica.** Só um processo externo que consulte o Connection
> Broker de verdade pode descobrir isso — que é exatamente o que `SessionReconciler` (T-602) é.
> Reatribuí a chamada de `CancelSessionAsync` para lá (linha de T-602 acima), com a mesma
> transparência da correção original de T-503: nenhuma suposição vira código sem ser conferida
> primeiro.
>
> **Implementação**: `ISessionRegistry`/`SessionRegistry`
> (`AppBridge.ControlPlane.Infrastructure/Sessions/`) — deliberadamente **não** um membro de
> `ISessionBackend` (RNF-035 é especificamente sobre RDS; isto é leitura/escrita da nossa própria
> tabela `session`, mesma categoria de `IAuthorizationService`). `RegisterAsync(tenantId,
> userAccountId, host, sourceIp, workstationName)`: procura uma sessão ativa do usuário **no mesmo
> host** (sessões RDS são por host — dois aplicativos em pools diferentes nunca compartilham
> sessão), atualiza `LastSeenAt` e reutiliza se achar; senão, monta um `Session` novo (rastreado,
> não salvo) com `BackendSessionId = "pending:{guid}"` — `PREMISSA:` um identificador de verdade só
> existe depois de algo falar com o Connection Broker real, o que nenhum membro de `ISessionBackend`
> faz ainda; o prefixo torna o placeholder óbvio para quem inspecionar a linha, inclusive o próprio
> `SessionReconciler` quando for escrito.
>
> **Novo índice único parcial** `ix_session_active_per_user` em `(tenant_id, session_host_id,
> user_account_id) WHERE ended_at IS NULL` (migração `AddSessionActivePerUserIndex`) — o índice
> `ix_session_active` que T-204 já criava é só `(tenant_id, session_host_id)`, sem usuário; sem essa
> extensão, uma corrida entre dois lançamentos simultâneos do mesmo usuário antes de qualquer sessão
> existir poderia criar duas sessões "ativas" no mesmo host, e reutilização ficaria ambígua sobre
> qual estender. Não altera o índice documentado em `MODELO-DE-DADOS.md` §6.2 (que continua servindo
> a varredura por host de T-602) — soma um novo, mais estreito.
>
> `LaunchEndpoints.CreateLaunch` (T-504) ganhou a chamada — busca a sessão ativa antes da transação
> (uma leitura simples, mesma categoria dos lookups de `application`/`host` já existentes), grava
> `launch.SessionId` e devolve `sessionReused` de verdade em vez do `false` fixo que T-504 documentou
> como provisório.
>
> **8 novos testes**: 6 em `SessionRegistryTests.cs` (Infrastructure.Tests, PostgreSQL real — cria
> sessão nova na primeira chamada; segunda chamada do mesmo usuário no mesmo host reutiliza;
> reutilização atualiza `LastSeenAt`; host diferente não reutiliza; usuário diferente no mesmo host
> não reutiliza; sessão já encerrada não é reutilizada, uma nova é criada) e 2 em
> `LaunchEndpointTests.cs` (segundo lançamento do mesmo usuário reutiliza a sessão — o critério de
> aceite literal de RF-024 — e prelaunch seguido de lançamento real reutiliza a sessão do prelaunch,
> RF-023). **Verificado subindo a aplicação real** com `curl`: duas requisições `POST /v1/launches`
> seguidas devolveram `sessionReused: false` e depois `true`, confirmado também por `psql` — uma
> única linha em `session`, dois `Launch.session_id` apontando para ela. **128 testes automatizados
> no total** (57 Api + 71 Infrastructure), todos passando. Nenhum bug de produção encontrado — a
> única correção desta tarefa foi, de novo, de escopo, feita antes de escrever código.

> **T-602 concluída em 2026-08-14 (S010) — só metade do que a linha do backlog descrevia, por uma
> razão diferente de qualquer correção anterior desta sessão: não é a sandbox que bloqueia, é um ADR
> já aceito.**
>
> "`SessionReconciler` contra o Connection Broker, com `reconciled_missing` e `stale_expired`" tem
> duas defesas de naturezas diferentes. `stale_expired` só lê `Session.LastSeenAt`, uma coluna que
> este código já possui — nenhuma dependência de RDS, nada que ADR-0006 toque. `reconciled_missing`
> precisa consultar o Connection Broker de verdade para descobrir sessões que ele não lista mais —
> e **ADR-0006 já decidiu, com Frederico, que essa consulta pertence ao MVP-1**: "Essa consulta
> passa a ser parte do escopo do MVP-1 e deve ficar atrás da interface de backend de sessão"; a
> própria tabela de alternativas do ADR rejeitou explicitamente antecipar isso para o MVP-0 por
> risco de calendário ("já concentra 34 requisitos Must em ~2 meses"). Diferente de T-502
> (`rdpsign.exe` real não existe nesta sandbox, mas *deveria* existir no MVP-0 — daí "interface +
> fake"), aqui a peça que falta **não deveria existir ainda**, por decisão já tomada. Construir
> `ListActiveSessionsAsync` agora, mesmo atrás de um fake, seria decisão de arquitetura sem ADR
> (RP-07) — silenciosamente sobrepondo um corte de fase que ADR-0006 já fixou (RA-05: ADR aceito é
> imutável, uma mudança de fase pediria um ADR novo que o substituísse, não código).
>
> **Achado por auditoria, não por acaso**: `ARQUITETURA.md` §4.2 ainda listava
> `ListActiveSessionsAsync` como "MVP-0 · RF-038, RF-062" — RF-062 foi movido para MVP-1 por
> ADR-0006 (2026-08-08) e esse documento nunca foi atualizado para refletir isso. Violação de RA-06
> ("documento que deixou de refletir decisão registrada"), corrigida aqui: a linha agora diz
> "MVP-1 · RF-062, ADR-0006" e explica que RF-038 continua MVP-0 por outro caminho (`stale_expired`,
> que não usa essa operação).
>
> **Implementação (`stale_expired`)**: `ISessionReconciler`/`SessionReconciler`
> (`AppBridge.ControlPlane.Infrastructure/Sessions/`) — varre `Session` de **todos os tenants**
> (`IgnoreQueryFilters()`, mesma justificativa explícita de `AuthEndpoints.FindRefreshTokenAsync`,
> ADR-0004 item 7: uma varredura em segundo plano não tem um tenant ambiente) buscando sessões
> ativas com `LastSeenAt` mais antigo que a janela configurada, e chama
> `ISessionBackend.CancelSessionAsync(id, StaleExpired)` (T-506 — primeiro chamador real) para cada
> uma, reatribuindo o `TenantContext` compartilhado a cada sessão (o mesmo `DbContext`/`TenantContext`
> que a busca usa, para o filtro por tenant de `CancelSessionAsync` encontrar a linha certa).
> `SessionReconciliationHostedService` (`Api/BackgroundServices/`) roda isso a cada 5 minutos
> (`PREMISSA:` PRE-33) — sem isso, um reconciliador que ninguém chama não fecha nada; uma exceção
> num ciclo não derruba os próximos.
>
> **`PREMISSA:` janela de inatividade de 12 h** (PRE-32) — deliberadamente grosseira: não existe
> ainda nenhum heartbeat real (`LastSeenAt` só avança quando `SessionRegistry`, T-601, reutiliza uma
> sessão num segundo lançamento), então um usuário que abre um único aplicativo pela manhã e
> trabalha nele o dia todo nunca atualiza `LastSeenAt` de novo — uma janela mais curta fecharia
> sessões genuinamente em uso. Em MVP-0a isso tem baixo custo real: RF-064 (bloqueio por teto de
> licença), a única consequência visível ao usuário de uma contagem errada, é MVP-1 (ADR-0006) — o
> preço de errar aqui hoje é impreciso na trilha/no reaproveitamento de sessão de T-601, não
> trabalho bloqueado.
>
> **5 novos testes** em `SessionReconcilerTests.cs`, PostgreSQL real, multi-tenant de propósito
> (primeiro teste desta sessão a varrer mais de um tenant numa única chamada): fecha sessão parada
> há mais que a janela; não fecha sessão vista recentemente; não toca sessão já encerrada (não
> sobrescreve `EndReason`); varre duas sessões paradas em dois tenants diferentes na mesma chamada
> sem misturar dados entre eles; nenhuma sessão parada devolve zero sem alterar nada. **Dois bugs
> de teste pegos rodando contra PostgreSQL real** (nenhum de produção): (1) comparar
> `DateTimeOffset` por igualdade exata depois de um round-trip por `timestamptz` falha por
> diferença de nanosegundos — corrigido com a sobrecarga de tolerância do xUnit; (2) o cenário
> multi-tenant original tentava duas sessões ativas para o mesmo usuário no mesmo host — violava o
> próprio índice único que T-601 criou (`ix_session_active_per_user`), prova de que o índice
> funciona; corrigido usando um segundo usuário para a sessão "não deve fechar".
>
> **Verificado subindo a aplicação real e esperando um ciclo de verdade** (não só o boot): sessão
> semeada via `psql` com `last_seen_at` de 20 h atrás; após ~5 minutos, o log estruturado registrou
> "Session reconciliation closed 1 stale session(s)" e `psql` confirmou `ended_at`/`end_reason`
> corretos na linha. **133 testes automatizados no total** (57 Api + 76 Infrastructure), todos
> passando.

> **T-603 concluída em 2026-08-15 (S010) — última tarefa de E-06, sem correção de escopo desta
> vez.** `GET /v1/sessions/me` (`SessionsEndpoints.cs`) devolve as sessões ativas do próprio
> usuário — `EndedAt IS NULL`, isolamento automático por tenant (ADR-0004), sem parâmetro explícito
> de usuário ou tenant, mesmo formato de `GetApplications` (T-402). `API.md` §4 não detalha o
> formato de resposta (só "sessões ativas do próprio usuário, para o launcher indicar estado e
> apoiar a reconexão") — desenhei o mínimo que essa frase pede: `id`, `startedAt`, `lastSeenAt` e
> `host.displayName` fixo em `"Servidor de aplicativos"` (reaproveita `LaunchResponseHost` de
> T-504, mesma razão RNF-043). **Sem `applicationId`**: `Session` não registra qual aplicativo a
> originou (só `Launch` faz, via a FK que T-601 preenche) — inventar essa junção agora seria
> escopo que RF-024 ("indicar estado") nunca pediu.
>
> RF-027 (reconexão automática após queda de rede) segue MVP-1 (`REQUISITOS.md`) — este endpoint só
> expõe o dado que essa funcionalidade vai consumir depois, não implementa lógica de reconexão; o
> cabeçalho "MVP-0 · RF-024, RF-027" de `API.md` não é uma inconsistência a corrigir, é o mesmo
> tipo de relação que `GET /v1/applications`'s campo `available` já tem com um sinal de saúde que
> ainda não existe.
>
> **7 novos testes** em `SessionsEndpointTests.cs`: sem token → `401 SESSION_EXPIRED`; sem sessão
> ativa → lista vazia; sessão ativa aparece com os campos certos; sessão encerrada não aparece;
> sessão de outro usuário não aparece; sessão de outro tenant não aparece (mesmo com
> `ExternalSubject` idêntico entre os dois, prova de que o isolamento é por tenant, não por
> identidade); e um teste de ponta a ponta — `POST /v1/launches` de verdade seguido de
> `GET /v1/sessions/me` mostrando a sessão que ele criou, a primeira vez que os dois endpoints são
> exercitados em sequência num único teste. **Verificado subindo a aplicação real**: `curl` antes
> de qualquer lançamento devolveu `{"items":[]}`; depois de `POST /v1/launches` (201), a mesma
> chamada devolveu a sessão recém-criada. **140 testes automatizados no total** (64 Api + 76
> Infrastructure), todos passando. **Com T-603, E-06 · Sessão e reconciliação está completo** — as
> 3 tarefas concluídas, com a defesa `reconciled_missing` formalmente adiada para o MVP-1 por
> ADR-0006 (T-602).

### E-07 · Trilha e retenção — 13 pts

| ID | Tarefa | Critério de aceite | Est. |
|----|--------|--------------------|------|
| ~~T-701~~ | ✅ Tabelas `launch` e `access_event` append-only | Sem caminho de `UPDATE`/`DELETE` na aplicação (RNF-019) | 5 |
| T-702 | `GET /audit/launches` e `/audit/access-events` | Consulta com filtro e cursor (RF-040) | 3 |
| T-703 | `RetentionWorker` + `purge_run` | Expurgo respeita o mínimo e **registra a si mesmo** (RNF-018, ADR-0007) | 5 |

> **T-701 concluída em 2026-08-15 (S010) — sem correção de escopo.** RNF-019 já tinha metade do
> controle desde T-204/T-205: `AppendOnlyEntity` (`Domain/Common/`) não carrega `UpdatedAt`,
> `UpdatedBy` nem `DeletedAt` — "a ausência é o controle", como o próprio comentário da classe já
> dizia. O que faltava era a outra metade: nada impedia código de aplicação de carregar um `Launch`
> ou `AccessEvent` já persistido, mudar uma propriedade mutável (`Outcome`, `Result` etc. têm
> `set`, só os timestamps são `init`) e chamar `SaveChangesAsync` — o EF Core emitiria um `UPDATE`
> sem reclamar, e `context.Launches.Remove(...)` um `DELETE`.
>
> **Implementação**: `AppBridgeDbContext.EnforceAppendOnly()` (novo método privado, mesmo padrão de
> `StampAuditColumns`, T-205) percorre `ChangeTracker.Entries()` a cada `SaveChanges`/
> `SaveChangesAsync` e lança `AppendOnlyViolationException`
> (`Infrastructure/Auditing/`, nova) se encontrar qualquer `AppendOnlyEntity` em
> `EntityState.Modified` ou `EntityState.Deleted` — **antes** de qualquer SQL rodar, mesma
> disciplina "falha antes da escrita" que a auditoria já tinha para outra coluna. Cobre `Launch`,
> `AccessEvent` e `PurgeRun` (a tabela que T-703 vai popular) automaticamente, por herdarem de
> `AppendOnlyEntity` — nenhuma lista de tipos para manter.
>
> **A única remoção legítima — expurgo por retenção (T-703, ADR-0007) — nunca aparece aqui, por
> construção**: `ExecuteDeleteAsync`/SQL bruto são operações em lote que não passam pelo change
> tracker, então não há flag de bypass para esquecer de desligar depois; T-703 simplesmente não usa
> o caminho que este guard vigia. Nenhuma mudança em `AppendOnlyEntity` nem nas tabelas foi
> necessária — só o guard em si.
>
> **Escopo do RNF-019 é a aplicação, não o banco** (`SEGURANCA.md` AM-08 já registra isso
> explicitamente: "quem tiver acesso direto ao banco contorna" é risco residual aceito, PS-02) —
> por isso um guard em `SaveChanges`, não um `REVOKE UPDATE/DELETE` a nível de PostgreSQL, é a
> ferramenta certa aqui; nenhuma mudança de infraestrutura de banco foi cogitada.
>
> **6 novos testes** em `AppendOnlyEnforcementTests.cs`, PostgreSQL real (a prova é a linha no
> disco não mudar, não só a exceção em memória): `UPDATE` e `DELETE` de `Launch`, `AccessEvent` e
> `PurgeRun` lançam `AppendOnlyViolationException` e deixam a linha intacta; inserir uma linha nova
> continua funcionando (sanity — todo outro teste do projeto que grava `Launch`/`AccessEvent` já
> dependia implicitamente disso, agora está explícito). **Verificado subindo a aplicação real**:
> `POST /v1/auth/session` gravou `access_event` normalmente — o guard não bloqueia inserções.
> **146 testes automatizados no total** (64 Api + 82 Infrastructure), todos passando. Nenhum bug de
> produção encontrado — nada no código existente mutava um `Launch`/`AccessEvent`/`PurgeRun` já
> persistido, então o guard não quebrou nenhum caminho em uso.

### E-08 · Launcher — fundação — 26 pts

| ID | Tarefa | Critério de aceite | Est. |
|----|--------|--------------------|------|
| T-801 | Projeto WinUI 3 + MSIX + empacotamento | Instala sem privilégio administrativo; **testa PRE-17** (RNF-044) | 8 |
| T-802 | `ApiClient` com token, renovação e tratamento dos códigos de erro | Cada código produz mensagem em pt-BR acionável (RF-025, RNF-043) | 5 |
| T-803 | `CredentialStore` no Windows Credential Manager | Token não aparece em arquivo nem no SQLite (RF-005) | 3 |
| T-804 | Cache do catálogo em SQLite | Interface abre sem rede, com aviso de estado (RF-014) | 5 |
| T-805 | Interface do catálogo: lista, ícones, estado, latência | Usuário identifica seus aplicativos sem treinamento (RF-026, RNF-042) | 5 |

> **Gatilho do ADR-0005:** se T-801, T-901 e T-902 juntos passarem de **10 pontos reais**, o launcher
> migra para WPF via ADR novo. A medição é objetiva e a decisão já está tomada — só falta o dado.

### E-09 · Launcher — integração com o desktop — 16 pts

| ID | Tarefa | Critério de aceite | Est. |
|----|--------|--------------------|------|
| T-901 | Registro e tratamento de `appbridge://launch/<app>` | Duplo clique no atalho abre o aplicativo (RF-029) | 5 |
| T-902 | "Instalar meus aplicativos": atalhos no Desktop e Menu Iniciar | Atalhos com ícone correto, apontando para o protocolo (RF-030, RF-031) | 5 |
| T-903 | **Remoção de atalho na revogação** | Aplicativo revogado some do Desktop na sincronização seguinte (RF-032) | 3 |
| T-904 | Início com o Windows e ícone na bandeja | Launcher disponível sem o usuário abri-lo (RF-033) | 3 |

### E-10 · Launcher — lançamento e prelaunch — 19 pts

| ID | Tarefa | Critério de aceite | Est. |
|----|--------|--------------------|------|
| T-1001 | `LaunchCoordinator`: pede, grava com TTL, chama `mstsc`, **apaga** | Nenhum `.rdp` sobrevive ao lançamento nem ao TTL (RF-020, RF-022) | 5 |
| T-1002 | SessionPrimer publicado + `PrelaunchService` | Sessão pronta no logon; **valida PRE-22** — o prelaunch sustenta a jornada? (RF-023, R-015) | 8 |
| T-1003 | Medição do tempo de abertura | Número real de RNF-027 registrado, com e sem prelaunch; **valida PRE-11** | 3 |
| T-1004 | Reutilização de sessão no segundo aplicativo | Sem nova sessão, sem nova credencial (RF-024) | 3 |

### E-11 · Segurança e verificação — 18 pts

| ID | Tarefa | Critério de aceite | Est. |
|----|--------|--------------------|------|
| T-1101 | **PS-05** — permissões mínimas da conta de serviço no AD e no banco | Conta sem administração de domínio; documentado (RNF-005) | 3 |
| T-1102 | **PS-09** — limites de taxa por endpoint | `429` com `Retry-After` sob excesso (RNF-010, AM-23) | 3 |
| T-1103 | **PS-10** — procedimento de comprometimento do certificado de assinatura | Documento com passos de rotação e revogação (AM-02) | 2 |
| T-1104 | Executar V-01, V-04, V-05, V-06, V-07, V-08 e registrar | Todas as verificações de MVP-0 com resultado arquivado | 5 |
| T-1105 | Filtro de campos sensíveis no log | Nenhum segredo em log, verificado por amostragem (RNF-004) | 3 |
| **T-1106** | **PS-04 antecipada** — varredura automática de segredos no repositório | Detecção roda a cada alteração e falha o build ao encontrar (AM-20; era MVP-1, antecipada por ADR-0016) | 2 |

### E-12 · Operação e dogfood — 16 pts

| ID | Tarefa | Critério de aceite | Est. |
|----|--------|--------------------|------|
| T-1201 | Backup diário + **restauração testada** | V-09 executada em ambiente separado (RNF-033) | 5 |
| T-1202 | Alerta de espaço em disco e de saúde | Disco baixo alerta **antes** de virar indisponibilidade (R-012, AM-22) | 3 |
| T-1203 | **Roteiro operacional**, incluindo o procedimento de desligamento de usuário no MVP-0 | Escrito: desabilitar no AD **e** encerrar sessão manualmente no host (R-014, AM-30) | 3 |
| T-1204 | Semana de dogfood dirigido, com registro de defeitos e medições | CS-01, CS-02 e CS-03 evidenciados | 5 |

---

## 4. Checagem de capacidade — o número inconveniente

| Épico | Pontos |
|-------|--------|
| E-01 Infraestrutura | 34 |
| E-02 Fundação do Control Plane | 26 |
| E-03 Identidade | 21 |
| E-04 Catálogo | 11 |
| E-05 Lançamento | 32 |
| E-06 Sessão | 16 |
| E-07 Trilha | 13 |
| E-08 Launcher — fundação | 26 |
| E-09 Launcher — desktop | 16 |
| E-10 Launcher — lançamento | 19 |
| E-11 Segurança | 18 |
| E-12 Operação | 16 |
| **Total MVP-0** | **248** |

Com a âncora de PRE-25 (1 ponto ≈ meio dia): **≈ 124 dias de trabalho focado**.

> **Atualizado por ADR-0016:** o total subiu de 241 para 248 pontos com a incorporação de T-207,
> T-506 e T-1106, vindos da revisão S008.

A janela de P8 vai do fim de agosto a meados de outubro: **≈ 32 dias úteis**. E esses dias não são
integrais — Frederico dirige um escritório de contabilidade, o que reduz a dedicação a uma fração
`PREMISSA:` (PRE-26) de talvez 40% a 60%.

| Cenário | Dias disponíveis | Cobertura do escopo |
|---------|------------------|---------------------|
| Dedicação integral | 32 | 27% |
| Dedicação de 50% | 16 | 13% |
| **Se eu estiver errado por um fator de 2** (otimista) | 32 | 53% |

**Conclusão, sem rodeios: o MVP-0 completo não cabe até meados de outubro.** A conclusão é robusta —
mesmo que minha estimativa esteja errada pela metade, o escopo não entra. Isso não é falha de
planejamento: é o que R-006 e R-007 anteciparam, agora com número.

Fingir que cabe produziria o desfecho clássico — cortes decididos às pressas, e o que cai primeiro é
sempre teste, documentação e verificação de segurança, ou seja, exatamente o que distingue este
projeto de um script.

---

## 5. Divisão do MVP-0 — **decidida** (ADR-0013)

Preserva a data de outubro **redefinindo o que ela entrega**, e mantém a disciplina.

### MVP-0a · "Esqueleto ambulante" — ≈ 100 pts · alvo: meados de out/2026

**Um usuário, um aplicativo, um caminho, ponta a ponta e de verdade.**

E-01 completo (34) · E-02 completo (24) · T-301, T-304 (11) · T-401, T-402 (6) · T-501, T-502, T-503,
T-504 (26) · **T-207** (2) e **T-506** (3), os dois gaps do ADR-0016 — do launcher, apenas o mínimo
para disparar o lançamento, sem MSIX nem atalhos.

**Critério de aceite:** Frederico abre o Domínio Contábil pelo AppBridge, na própria estação, sem
digitar senha, com `.rdp` assinado, registro em trilha e 3389 comprovadamente fechado (V-01, V-05,
V-06).

**Por que este recorte e não outro:** ele valida cedo as três premissas que podem derrubar o desenho
— PRE-22 (prelaunch), PRE-23 (Connection Broker) e PRE-11 (tempo de abertura). Descobrir em outubro
que o prelaunch não sustenta a jornada é recuperável; descobrir em janeiro, na véspera do piloto, não é.

### MVP-0b · "Dogfood real" — ≈ 148 pts · alvo: dez/2026 a jan/2027

Todo o restante: launcher empacotado, atalhos, prelaunch, reconciliação, retenção, segurança e a
semana de dogfood dirigido. **Critério de aceite: CS-01 a CS-04 integralmente.**

### Efeito no piloto — **decidido: opção A**

M3 no 1º trimestre de 2027 seria inviável com dogfood terminando em janeiro. Frederico escolheu a
**opção A em 2026-08-08**: o piloto vai para **abr–jun/2027**, mantendo 3–5 escritórios.

| Opção | Consequência | Situação |
|-------|--------------|----------|
| **A — Piloto no 2º tri/2027** | Mais seguro. Dá folga para T-001 (G-01), T-002, T-003, PS-02 e PS-03 | ✅ **Escolhida** (ADR-0013) |
| B — Piloto reduzido no 1º tri: 1 escritório, sem cofre | Valida o modelo comercial cedo, com risco operacional maior | Recusada |
| C — Manter 3–5 escritórios no 1º tri | Levaria a produção um sistema sem dogfood completo, sem PS-02 e sem V-09 | Recusada |

> **Consequência que o adiamento cria — R-025.** Com o dogfood terminando em janeiro e o piloto
> começando em abril, sobram ~3 meses para os épicos E-13 a E-18. Pela mesma aritmética da §4, **o
> MVP-1 completo provavelmente não cabe nessa janela** — ele é o novo gargalo. É preciso definir o
> subconjunto mínimo exigido pelo piloto (**B-009**). Recomendação preliminar: priorizar **E-15**
> (encerramento de sessão, que fecha R-014/AM-30), **E-16** (metering mínimo, o único diferencial
> presente no piloto) e a parte de permissões do **E-13**; adiar favoritos (RF-017), atualização
> automática (RF-035) e exportação (RNF-021).

---

## 6. O que cortar, se ainda faltar prazo — nesta ordem

Ordem decidida agora, com a cabeça fria, e **não no meio do aperto**.

| Ordem | Item | Perda |
|-------|------|-------|
| 1º | T-403 `ETag` (2) | Sincronização mais cara; nada visível |
| 2º | T-904 bandeja (3) | Usuário abre o launcher manualmente |
| 3º | T-702 endpoint de consulta da trilha (3) | Consulta por SQL até o painel do MVP-1 |
| 4º | T-603 `GET /sessions/me` (3) | Launcher não mostra sessões ativas |
| 5º | T-1003 medição formal (3) | Perde-se o número de RNF-027 — **é perda de conhecimento, não de função** |
| 6º | T-805 latência (parte de 5) | Sem indicador de latência (RF-026 é Should) |

**Abaixo desta linha estão apenas requisitos Must, e cortar qualquer um exige ADR** (RP-07). Em
particular, **não cortar**: T-205 (auditoria transacional), T-206 (teste de tenant), T-502
(assinatura), T-1104 (verificações) e T-1201 (restauração testada) — são os itens que sustentam as
afirmações de `SEGURANCA.md` §10.

---

## 7. Fases seguintes — épicos

### MVP-1 · painel, metering e reconexão

| Épico | Conteúdo | Requisitos |
|-------|----------|-----------|
| E-13 | Painel Blazor: aplicativos, usuários, grupos, permissões | RF-043, RF-044 |
| E-14 | Trilha administrativa e consulta com exportação | RF-041, RF-046, RNF-017, RNF-021 |
| E-15 | **Encerramento de sessão** — fecha R-014/AM-30 | RF-008, RF-045 |
| E-16 | **Metering mínimo** (ADR-0006) | RF-062..RF-064 |
| E-17 | Reconexão robusta, atualização automática do cliente, favoritos | RF-027, RF-035, RF-017 |
| E-18 | Segurança: PS-01, PS-04, PS-06 | AM-02, AM-20, AM-32 |

### Portão de entrada do piloto — **nenhum é negociável**

| Portão | Item | Risco coberto |
|--------|------|---------------|
| **G-01** | **Declaração de titularidade e conformidade de licença assinada pelo cliente**, anexa ao contrato (ADR-0014). O cliente adquire, instala e usa suas próprias licenças | **R-001 (reescrito)** — a declaração aloca a responsabilidade; não elimina o resíduo do Caminho B |
| **G-02** | T-003 — cotação SPLA validando PRE-05 | R-003 |
| **G-03** | PS-02 e V-09 executados | AM-16, AM-25 |
| **G-04** | Decisão sobre PS-03 (encadeamento da trilha) | R-021 |
| **G-05** | Revisão do ADR-0003 (malha privada não é vendável) e de PS-08 (área de transferência) | R-010, R-011 |

> **G-01 mudou de natureza em ADR-0014.** Deixou de ser consulta a fornecedor e passou a ser
> declaração do cliente. Continua intransponível — cliente sem declaração assinada não entra no
> piloto —, mas não depende mais de terceiro sem prazo de resposta. O que ele **não** faz é eliminar
> o resíduo: alguns termos de licença restringem execução em infraestrutura operada por terceiro
> independentemente de quem detém a licença, e nesse caso a declaração não protege o provedor.

### V2 · Agent, cofre, metering completo, acesso externo

E-19 Agent · E-20 **Cofre de certificados** (bloqueado por T-002 e **B-006/PS-07**) · E-21 fila de
espera e relatórios · E-22 RD Gateway + MFA · E-23 cliente web · E-24 políticas pelo painel.

### V3 · Orquestrador, comercial

E-25 Orquestrador de atualizações · E-26 balanceamento · E-27 branding · E-28 cobrança · E-29 AVD
(prova de RNF-035, fecha R-018).

---

## 8. Caminho crítico e dependências externas

```mermaid
gantt
    dateFormat YYYY-MM-DD
    title Caminho crítico do MVP-0
    section Externo
    Registro de licenças (G-01)     :t1, 2026-08-11, 20d
    Aquisição de host e licenças    :crit, a1, 2026-08-11, 21d
    section Infraestrutura
    Domínio e VMs (T-102, T-103)    :crit, i1, after a1, 10d
    RDS, FSLogix, AppLocker         :i2, after i1, 10d
    Ingresso de estações (T-106)    :crit, i3, after i2, 10d
    section Software
    Fundação Control Plane          :s1, after a1, 12d
    Lançamento assinado             :crit, s2, after s1, 12d
    Launcher mínimo                 :s3, after s2, 10d
    section Validação
    MVP-0a ponta a ponta            :milestone, m1, after i3, 0d
```

**Dependências fora do controle do projeto:** prazo de entrega do hardware · ativação das licenças ·
disponibilidade das estações e das pessoas para T-106 ·
parecer do advogado em T-002 (bloqueia V2, não MVP-0).

---

## 9. Critérios de aceite da fase MVP-0

| # | Critério | Verificação |
|---|----------|-------------|
| CS-01 | Um dia inteiro de trabalho real via AppBridge, sem `mstsc` manual | T-1204 |
| CS-02 | Atualização feita uma vez no servidor reflete para todos | T-1204 |
| CS-03 | Todo lançamento gera registro auditável | T-702, V-05 |
| CS-04 | Nenhuma porta RDP exposta | **V-01, com resultado arquivado** |
| — | Verificações V-01, V-04 a V-08 executadas e registradas | T-1104 |
| — | Restauração de backup validada | T-1201, V-09 |

---

## 10. Riscos de execução

| ID | Risco | Mitigação neste plano |
|----|-------|----------------------|
| R-006 | Execução solo de projeto com quatro componentes | Divisão em MVP-0a/0b; ordem de corte decidida a frio (§6) |
| R-007 | Densidade de requisitos Must no MVP-0 | §4 quantifica; §5 replaneja |
| **R-023** | **A infraestrutura (E-01, 34 pts) é o caminho crítico e não é código** — depende de compra, de terceiros e da agenda das pessoas | Iniciar E-01 **antes** de qualquer linha de código; T-101 e G-01 podem começar hoje |
| R-001 | Licenciamento (reescrito por ADR-0014): responsabilidade é do cliente; resta o resíduo de termos que vedam infraestrutura operada por terceiro | G-01 na forma de declaração assinada; resíduo para o advogado de T-002 |
| R-015 | Prelaunch não medido | T-1002 e T-1003 dentro do MVP-0a/0b, não no fim |
| R-020 | Cofre sem impedimento técnico ao provedor | E-20 bloqueado por B-006/PS-07 |
| **R-030** | **O MVP-0a real pode ser maior que qualquer das duas estimativas.** A linha B estimou 96 pts **sem** o épico de infraestrutura; a linha A, ~95 pts **com** ele. O que cada uma cobre, somado, aproxima-se de **130 pts** | Reavaliar contra a data de out/2026 antes de assumir o marco M2a como firme |
| **R-025** | **O MVP-1 é o novo gargalo:** ~3 meses entre o fim do dogfood (jan/2027) e o piloto (abr/2027) para os épicos E-13 a E-18 | Definir subconjunto mínimo do piloto — **B-009**, com recomendação preliminar na §5 |

---

## 11. Premissas introduzidas por este documento

| ID | Premissa | Impacto se errada |
|----|----------|-------------------|
| **PRE-25** | 1 ponto ≈ meio dia de trabalho focado | Toda a §4 escala junto — mas a conclusão sobrevive a erro de 2× |
| **PRE-26** | Dedicação de 40% a 60% do tempo útil ao projeto | Se for menor, MVP-0a também não cabe em outubro e M2 precisa de nova data |
