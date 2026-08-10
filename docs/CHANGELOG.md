# Changelog

Todas as mudanças relevantes do projeto AppBridge são registradas aqui.
Formato: [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) · Versionamento: SemVer
independente por componente (RP-03).

## [Não publicado]

### Adicionado — suíte nomeada de violação de tenant, V-02 (T-206, S010)
- `tests/AppBridge.ControlPlane.Infrastructure.Tests/TenantViolationTests.cs` — local explícito e
  nomeado da suíte para **V-02** (`SEGURANCA.md` §7, AM-07/AM-14, ADR-0004 item 9). T-203 e T-204 já
  provavam os dois mecanismos (filtro de leitura, FK composta de escrita), mas só com `Application`
  e `application`/`host_pool`. Esta tarefa fecha duas lacunas de forma:
  - Nenhum teste anterior exercitava `SetTenantFilter` sozinho — o caminho de filtro que entidades
    de trilha sem `deleted_at` percorrem (distinto de `SetTenantAndSoftDeleteFilter`). Fechado com
    um caso em `AccessEvent`.
  - Nenhum teste anterior cobria uma FK composta **anulável** (`redirection_policy.application_id`)
    nem uma tabela com **duas FKs independentes para o mesmo tipo principal**
    (`application_permission.granted_by`/`revoked_by`, ambas para `user_account`) — o desenho mais
    propenso a esconder um erro de configuração por cópia-e-cola.
  - Cobertura deliberadamente não exaustiva das 13 tabelas/15 FKs — registrado no próprio arquivo,
    não implícito.

### Adicionado — `AuditWriter` transacional (T-205, S010)
- **`IAuditWriter`/`AuditWriter`** (`Infrastructure/Auditing`): caminho único pelo qual as operações
  de segurança (RF-036, RF-037, RF-039, RF-041, RF-042) gravam seu registro de trilha.
  `ExecuteAsync` adiciona a entrada de auditoria, aplica a mutação de estado da concessão (`grant`,
  deliberadamente síncrona e só-de-banco — a assinatura do método impede que um efeito colateral
  externo entre no limite transacional) e faz um único `SaveChangesAsync`. Qualquer falha lança
  `AuditWriteFailedException` sem persistir nada — nem a auditoria, nem a concessão.
- **Verificado com uma falha de gravação simulada**: um `SaveChangesInterceptor` de teste lança
  exatamente no ponto em que o EF Core emitiria o SQL, e a suíte confirma que nem a linha de
  auditoria nem a mutação da concessão sobrevivem. A falha é sempre logada antes do relançamento
  (ADR-0007 condição 2). 3 novos testes em `AuditWriterTests.cs`.
- `AuditWriteFailedException` como tipo próprio, para que o endpoint que futuramente chamar
  `IAuditWriter` (T-301 em diante) responda com o `503 AUDIT_UNAVAILABLE` estável de `API.md` em vez
  de um 500 genérico.

### Adicionado — chaves estrangeiras compostas com `tenant_id` (T-204, S010)
- **15 chaves estrangeiras compostas** `(tenant_id, x_id) -> tabela(tenant_id, id)`, cobrindo toda
  referência entre tabelas de tenant listada em `MODELO-DE-DADOS.md` — inclusive três que o modelo já
  documentava mas o código ainda não marcava com `TODO(T-204)`
  (`redirection_policy.application_id`, `application_permission.granted_by`/`revoked_by`,
  `launch.session_id`, `access_event.user_account_id`), corrigidas junto.
- **6 chaves alternativas** `UNIQUE (tenant_id, id)` nas entidades que são alvo de referência
  (`application`, `group`, `host_pool`, `session_host`, `session`, `user_account`) — a "chave
  candidata" do exemplo do próprio ADR-0011 §4.
- **13 chaves estrangeiras simples** `tenant_id -> tenant(id)`, uma por tabela com escopo de tenant —
  já declaradas como `uuid FK` em `MODELO-DE-DADOS.md`, agora de fato ligadas ao banco. `ON DELETE
  RESTRICT` em todas: dado de tenant nunca é fisicamente removido (ADR-0011 §3), então a restrição só
  dispararia diante de um `DELETE` que não deveria acontecer.
- **Verificado com PostgreSQL real, nas duas direções**: uma escrita cruzada de tenant foi tentada e
  recusada com o nome de constraint exato (`fk_application_host_pool`); a mesma escrita, correta,
  foi aceita. 2 novos testes em `TenantForeignKeyTests.cs` fixam essa prova como regressão.
- `TenantIsolationTests.cs` corrigido: usava um `host_pool_id` fabricado que só era inofensivo por
  não haver FK ainda — passou a falhar corretamente após esta tarefa, e foi ajustado para criar um
  `HostPool` real por tenant.

### Adicionado — isolamento por tenant no `DbContext` (T-203, S010)
- **`ITenantContext`/`TenantContext`** (`AppBridge.ControlPlane.Infrastructure.Tenancy`) — o tenant
  corrente para a unidade de trabalho, resolvido do token por middleware que T-301 adiciona; `null`
  até algo o definir.
- **`AppBridgeDbContext` ganhou um filtro global de consulta**, aplicado por reflexão a cada tipo de
  entidade: `ITenantScoped` + `AuditedEntity` recebe `TenantId == contexto.TenantId && DeletedAt IS
  NULL`; só uma das duas condições recebe só a cláusula correspondente. A parte de `DeletedAt` não é
  uma decisão nova — é a consequência que ADR-0011 §5 já registrava ("junto com o filtro de
  tenant"), agora executada.
- **Verificado com PostgreSQL real**: consulta sem `Where` só devolve a linha do tenant certo;
  contexto sem tenant resolvido devolve zero linhas (isolamento falha fechado, não falha aberto);
  linha com exclusão lógica some da consulta padrão e reaparece com `IgnoreQueryFilters()` — a
  mesma via que o papel de operador do provedor (RF-075, MVP-1) usará de forma nominal e auditada.
  4 novos testes em `TenantIsolationTests.cs`.

### Corrigido — corrida entre suítes de teste que migram o mesmo banco (T-203, S010)
- Rodar `SchemaTests` e `TenantIsolationTests` juntos falhava com `relation "application" does not
  exist` — corrida, não bug de isolamento: as duas suítes migram o mesmo banco `appbridge_test`
  para cima e para baixo, e o xUnit paraleliza classes de teste por padrão. Corrigido serializando
  o assembly de teste (`CollectionBehavior(DisableTestParallelization = true)`).

### Adicionado — persistência do Control Plane (T-202, S010)
- **`src/AppBridge.ControlPlane.Domain`** — 15 entidades fiéis a `MODELO-DE-DADOS.md`, com hierarquia
  de base para colunas de auditoria (`AuditedEntity`, `AppendOnlyEntity`) e escopo de tenant
  (`ITenantScoped`). `Tenant` e `SigningCertificate` são as duas exceções deliberadas, documentadas
  no próprio código.
- **`src/AppBridge.ControlPlane.Infrastructure`** — `AppBridgeDbContext` (EF Core 10 + Npgsql),
  convenções próprias de `snake_case` e de conversão de enum (sem dependência nova), e mapeamento do
  `RowVersion` para a coluna de sistema `xmin` do PostgreSQL como token de concorrência otimista.
  Migração `InitialCreate`: 15 tabelas, `tenant_id NOT NULL` em todas exceto as duas exceções, CHECK
  constraints e índices parciais nomeados conforme o modelo de dados.
- **Verificado na prática, não só lido**: migração aplicada e revertida contra PostgreSQL real
  (`appbridge_dev`); as duas CHECK e a unicidade de `tenant.slug` testadas com dado real e rejeição
  confirmada pelo nome da constraint. `tests/AppBridge.ControlPlane.Infrastructure.Tests` — 8 testes,
  banco dedicado `appbridge_test`, migração para cima e para baixo dentro do próprio teste.
- `docs/SETUP-DEV.md` — novo documento: ambiente de desenvolvimento local (SDK, PostgreSQL, variáveis
  de ambiente, comandos de migração e teste), referenciado pelas mensagens de erro do próprio código.

### Corrigido — conflito de versão do EF Core (T-202, S010)
- Build de `tests/AppBridge.ControlPlane.Infrastructure.Tests` emitia `MSB3277`:
  `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 trazia `Microsoft.EntityFrameworkCore.Relational`
  10.0.4 transitivamente, em conflito com a 10.0.10 usada diretamente. Sem versão mais nova do Npgsql
  disponível no NuGet nesta data. Fixado `Microsoft.EntityFrameworkCore.Relational` em 10.0.10 no
  `.csproj` da Infrastructure, com comentário explicando o motivo.

### Adicionado — primeiro código do Control Plane (T-201, S010)
- **`src/AppBridge.ControlPlane.Api`** — esqueleto ASP.NET Core em .NET 10 (`AppBridge.slnx`).
  `CorrelationIdMiddleware` resolve/gera o `X-Correlation-Id` e grava início e fim de cada
  requisição no log, com o ID no escopo (RNF-039, verificado por teste que captura o log real, não
  apenas a resposta HTTP). `GET /v1/health` via `Microsoft.Extensions.Diagnostics.HealthChecks`,
  extensível para as dependências reais que as tarefas seguintes forem adicionando (PostgreSQL em
  T-202, AD DS em T-301, certificado em T-502), sem checks de fachada criados hoje sem lastro.
- `tests/AppBridge.ControlPlane.Api.Tests` — 7 testes automatizados.
- Vulnerabilidade `NU1903` (GHSA-v5pm-xwqc-g5wc) em `Microsoft.OpenApi` 2.0.0 fixada em 2.11.0
  antes do primeiro commit.

### Corrigido — sequenciamento E-01/E-02 (S010)
- `STATUS.md` e `ROADMAP.md` diziam que o Control Plane (E-02) "depende da infraestrutura existir"
  (E-01). Vale para o deploy real e para testes de integração contra AD DS/RDS — não para o
  esqueleto de código, que só precisa de PostgreSQL de desenvolvimento. E-02 passa a correr **em
  paralelo** com a aquisição de hardware (T-101), não depois dela. Não é mudança de escopo.
- Ambiente de desenvolvimento instalado: .NET 10 SDK 10.0.302, PostgreSQL 16 local.

### Reconciliado — backlog único (ADR-0016, S009)
- **`main` mesclado ao branch.** As duas linhas de trabalho voltaram a ser uma.
- **`ROADMAP.md` passa a ser o backlog único.** `BACKLOG_MVP0A_PRIORIZADO.md` vira anexo histórico e
  `ANALISE_BUGS_E_MELHORIAS.md` recebe errata com as 14 referências incorretas; nenhum dos dois corpos
  foi reescrito.
- **Os dois gaps foram corrigidos nas fontes:** `MODELO-DE-DADOS.md` §7.1 ganhou a coluna `purpose` na
  tabela `launch`, e `ARQUITETURA.md` §4.2 ganhou `CancelSessionAsync` em `ISessionBackend`.
- `ROADMAP.md` incorporou **T-207** (2 pts), **T-506** (3 pts) e **T-1106** (2 pts, PS-04 antecipada).
  Total do MVP-0: **241 → 248 pontos**; MVP-0a: **~95 → ~100**.
- Bloqueio B-010 encerrado; risco R-026 fechado. Novos riscos **R-030** (o MVP-0a real aproxima-se de
  130 pontos, ~35% acima do que o ADR-0013 assumiu) e **R-031** (caminho de falha do prelaunch).

### Revisado — issues e backlog paralelo (S008)
- `docs/operacao/revisao-issues-e-backlog-paralelo.md` — revisão dos 32 issues abertos e dos dois
  documentos que os originaram. Confirma **dois gaps reais na documentação aprovada** (coluna
  `purpose` ausente no modelo de dados e falta de operação de cancelamento em `ISessionBackend`),
  identifica **3 contradições com ADRs aceitos** (uma delas regressão de segurança sobre a chave de
  assinatura), a **ausência de issues para o épico de infraestrutura**, uma **colisão de número de
  ADR** e **14 referências de requisito incorretas**.
- Riscos R-026 a R-029 e bloqueios B-010 e B-011 registrados. Nenhum issue do GitHub foi alterado.

### Corrigido
- Logs das sessões S004 a S007 estavam datados 2026-08-08; os commits correspondentes são de 09 e 10
  de agosto. Arquivos renomeados e datas internas corrigidas.

### Adicionado — processo (ADR-0015, S007)
- `scripts/check-docs.sh` — **primeiro arquivo executável do repositório**: checagem mecanizável da
  documentação com 7 verificações (cabeçalho contraditório, contagem de ADRs, ADR inexistente,
  requisito órfão, link morto, prefixo reservado, log de sessão ausente). Sai com código 1 em caso de
  achado, pronto para integração contínua quando houver pipeline.

### Alterado — processo (ADR-0015)
- **`CLAUDE.md`, regra RA-02**: o fechamento de sessão passa a exigir uma quarta etapa — a checagem de
  consistência, mecânica e humana. Deriva documental passa a ser tratada como violação de RA-06 tanto
  quanto mudança silenciosa. **Primeira alteração do prompt mestre**, precedida de ADR conforme RP-07.

### Corrigido — auditoria de consistência (S006)
- **Cabeçalhos dos 7 entregáveis** atualizados de "submetido — aguardando aprovação" para "aprovado",
  alinhando-os ao `STATUS.md`.
- **`VISAO.md` emendado por ADR-0014** (nova §11 de emendas): NO-04 restringido para licenciamento
  sempre do cliente; RM-05 com severidade e mitigação revistas; **R-001 reescrito** — de risco
  crítico de invalidar o Caminho B para exposição alocada ao cliente, com resíduo declarado.
- Contagem de ADRs, dependências declaradas em `ARQUITETURA.md` e `SEGURANCA.md`, e a descrição de
  `license_notes` em `MODELO-DE-DADOS.md` alinhadas às decisões vigentes.

### Adicionado — execução (S005)
- `docs/operacao/E-01-infraestrutura/` — material de execução do épico de infraestrutura, primeiro do
  caminho crítico do MVP-0a: visão do épico com ordem e dependências, especificação de aquisição de
  T-101 (dimensionamento justificado, licenças e as perguntas de SPLA para T-003) e roteiro de
  implantação de T-102 a T-107, que traduz os ADR-0002, 0003, 0008, 0009 e 0010 em configuração
  concreta, com verificação por passo e V-01/V-04/V-08 como critério de aceite.
- Premissas PRE-27 (RAM por sessão) e PRE-28 (tamanho do container FSLogix), ambas a medir no dogfood.

### Alterado — licenciamento (ADR-0014, S004)
- **O licenciamento dos aplicativos hospedados passa a ser responsabilidade do cliente**, que
  adquire, instala e usa suas próprias licenças. O AppBridge não consulta fornecedor nem intermedia
  licença. Restringe o não-objetivo NO-04, que admitia licenciamento "pelo cliente ou pelo provedor".
- **Portão G-01 redefinido**: deixa de ser confirmação escrita do fornecedor e passa a ser
  **declaração de titularidade e conformidade assinada pelo cliente**, anexa ao contrato.
- `matriz-licenciamento.md` reorientada: de resultado de consulta para **registro do que o cliente
  declarou**, com uso em dimensionamento, metering (RF-063) e evidência.
- `ROADMAP.md`: consulta a fornecedor sai do caminho crítico e das dependências externas.
- **R-001 reescrito** — a decisão aloca a responsabilidade, mas não elimina o fato: quem instala não
  altera o que a licença permite. Severidade de Crítica para Alta. Resíduo do Caminho B registrado:
  termos que restringem execução em infraestrutura operada por terceiro.

### Removido
- `docs/operacao/T-001-licenciamento/carta-modelo.md`, `carta-dominio.md` e `carta-alterdata.md` —
  as cartas de consulta formal aos fornecedores, criadas em S003 e tornadas desnecessárias por
  ADR-0014.

### Adicionado — execução (S003)
- `docs/operacao/T-001-licenciamento/` — primeira pasta de material operacional, distinta da
  documentação de design: instruções de condução, carta-modelo parametrizada, cartas prontas para
  Domínio/Thomson Reuters e Alterdata, e a matriz de licenciamento que é o entregável de T-001
  (portão G-01, risco R-001). As cartas separam explicitamente o uso pelos próprios colaboradores do
  licenciado (C-1) da hospedagem por prestador para terceiros (C-2), e tratam o suporte técnico como
  pergunta própria, por ser o modo mais comum de inviabilização na prática.

### Aprovado — fase de design encerrada (2026-08-08)
- **Entregáveis 2 a 7 aprovados por Frederico**: `REQUISITOS.md`, `ARQUITETURA.md`,
  `MODELO-DE-DADOS.md`, `API.md`, `SEGURANCA.md` e `ROADMAP.md`. Com `VISAO.md`, já aprovado, os 7
  entregáveis da fase de design estão concluídos e 13 ADRs aceitos.

### Alterado — replanejamento (ADR-0013)
- **Cronograma replanejado na opção A**, escolhida por Frederico: o MVP-0 passa a ser entregue em
  duas etapas — **MVP-0a** (esqueleto ambulante, meados de out/2026) e **MVP-0b** (dogfood real,
  dez/2026 a jan/2027) — e o piloto do Caminho B vai para **abr–jun/2027**, mantendo 3–5 escritórios.
  Nenhum RF ou RNF foi alterado: a mudança é de marcos e agrupamento de entrega.
- `ROADMAP.md` §2 e §5 atualizados com a linha do tempo vigente; a linha original de P8 fica
  preservada para comparação.
- `STATUS.md` passa de fase de design para implementação do MVP-0a.
- Bloqueios B-003, B-004 e B-008 encerrados. Risco R-024 fechado.

### Registrado
- Risco **R-025** e bloqueio **B-009**: o MVP-1 tornou-se o gargalo — cerca de três meses entre o fim
  do dogfood e o início do piloto para os épicos E-13 a E-18. É preciso definir o subconjunto mínimo
  exigido pelo piloto.

### Adicionado
- `docs/ROADMAP.md` — entregável 7 e último da fase de design: 12 épicos e 54 tarefas de MVP-0 com
  critério de aceite verificável e estimativa relativa, checagem de capacidade contra a janela de
  P8, recomendação de divisão em MVP-0a e MVP-0b, ordem de corte decidida antecipadamente, portões
  de entrada do piloto (G-01..G-05), épicos de MVP-1 a V3, caminho crítico com dependências externas
  e critérios de aceite da fase. Submetido, aguardando aprovação.
- `docs/SEGURANCA.md` — entregável 6 da fase de design: ativos (A-01..A-08), fronteiras de confiança,
  modelo STRIDE com 33 ameaças (AM-01..AM-33) mapeadas a controles e com estado declarado
  (mitigado / parcial / aceito / pendente), 10 pendências de segurança (PS-01..PS-10), gestão de
  segredos, 9 verificações obrigatórias como critério de aceite (V-01..V-09), escopo excluído e
  resumo executivo separando o que o produto sustenta do que ainda não sustenta. Submetido,
  aguardando aprovação.
- `docs/API.md` — entregável 5 da fase de design: contrato v0 do Control Plane em estilo OpenAPI —
  autenticação e sessão, catálogo, lançamento, trilha de auditoria, administração, metering, Agent e
  cofre; catálogo de erros com código estável por situação; esqueleto OpenAPI 3.1 do endpoint de
  lançamento; rastreabilidade endpoint→requisito nos dois sentidos. Submetido, aguardando aprovação.
- `docs/adr/ADR-0012` — convenções da API: `/v1` no caminho, Problem Details (RFC 9457) com código
  estável, `Idempotency-Key` obrigatório no lançamento, `tenant_id` derivado exclusivamente do token,
  `404` para recurso de outro tenant e paginação por cursor.
- `docs/MODELO-DE-DADOS.md` — entregável 4 da fase de design: entidades do Control Plane em cinco
  domínios (tenancy, identidade, catálogo, sessão e trilha), diagramas ER do núcleo MVP-0 e do cofre
  de certificados, inventário de dados pessoais com base legal e retenção por coluna (LGPD),
  volumetria estimada para 500 usuários, regras de migração e rastreabilidade entidade→requisito nos
  dois sentidos. Submetido, aguardando aprovação.
- `docs/adr/ADR-0011` — convenções do modelo de dados: UUID v7, `timestamptz` em UTC, exclusão lógica
  para dado de tenant e proibida para trilha, e chave estrangeira composta com `tenant_id` como
  segunda linha de defesa do isolamento.
- `docs/ARQUITETURA.md` — entregável 3 da fase de design: C4 níveis 1 a 3, contrato da fronteira de
  portabilidade `ISessionBackend`, cinco diagramas de sequência (login, lançamento, prelaunch,
  publicação e revogação), tabela de modo degradado por falha, rastreabilidade componente→requisito,
  premissas PRE-21..PRE-23 e riscos arquiteturais R-013..R-018. Submetido, aguardando aprovação.
- `docs/adr/ADR-0009` — assinatura do `.rdp` via `rdpsign.exe` atrás de interface, com a consequência
  assumida de que o Control Plane é componente Windows nesta fase.
- `docs/adr/ADR-0010` — estações ingressadas no domínio e delegação de credenciais por GPO restrita
  aos session hosts, para que o lançamento não peça senha; senha de domínio nunca passa pelo Control
  Plane.
- `docs/adr/ADR-0001` a `ADR-0008` — oito decisões de arquitetura aceitas, por delegação de Frederico:
  identidade (AD DS base com Entra híbrido), topologia do MVP-0 (DC e session host em VMs separadas),
  acesso externo por rede privada em malha, isolamento multi-tenant híbrido, ratificação da stack com
  gatilho de reversão do launcher, antecipação do metering mínimo para MVP-1, auditoria bloqueante com
  política de retenção em três categorias, e política base de redirecionamento de periféricos.
- `docs/REQUISITOS.md` — entregável 2 da fase de design: 76 requisitos funcionais em escopo
  (RF-001..RF-076) mais 10 declarados fora de escopo (RF-077..RF-086), e 53 requisitos não-funcionais
  (RNF-001..RNF-053), com fase, classificação MoSCoW e origem rastreável para cada linha. Inclui os
  RNFs de segurança exigidos por RP-06 (§3.1) e os de auditoria exigidos por RA-07 (§3.2: log de
  acesso, trilha de uso de certificado, trilha administrativa e retenção configurável). Submetido,
  aguardando aprovação.
- `docs/VISAO.md` — entregável 1 da fase de design: problema e dores (PR-01..05), público-alvo
  (PA-01..03), proposta de valor (VP-01..04), diferenciais estratégicos (DIF-01..03), não-objetivos
  (NO-01..10), riscos de mercado (RM-01..10), riscos de viabilidade (R-001..006), premissas
  (PRE-01..06) e critérios de sucesso (CS-01..05). Submetido, aguardando aprovação.
- Estrutura documental de `/docs` conforme Seção 6.1 do prompt mestre (`adr/`, `auditoria/`).
- `CLAUDE.md` na raiz com o prompt mestre (regras RP-01..RP-09 e RA-01..RA-07).
- `docs/STATUS.md` — estado vivo do projeto, perguntas de descoberta P1–P8, riscos R-001..R-004.
- `docs/adr/TEMPLATE.md` — template de ADR (Seção 6.3).
- `docs/auditoria/2026-08-08-S001.md` — log da sessão S001.

### Alterado
- `docs/REQUISITOS.md` emendado pelos ADRs (registro completo em §8 do documento):
  - **RF-062, RF-063, RF-064** — fase alterada de V2 para **MVP-1** (ADR-0006).
  - **RNF-014** — fase alterada de V2 para **MVP-0**, com política base de redirecionamento (ADR-0008).
  - **RNF-022** — de Should não-bloqueante para **Must bloqueante** (ADR-0007).
  - **RNF-018** — prazos de retenção definidos por categoria de trilha (ADR-0007).
  - **RF-002, RF-003, RNF-007, RNF-009, RNF-036** — detalhados conforme ADR-0001 a ADR-0004.

### Aprovado
- `docs/VISAO.md` aprovado por Frederico em 2026-08-08.

### Registrado
- Premissas PRE-07..PRE-24; PRE-08 e PRE-09 resolvidas por ADR-0007.
- Pendências de projeto PD-01 (expurgo de exclusão lógica), PD-02 (Row-Level Security), PD-04
  (armazenamento das respostas de idempotência) e PD-05 (limites de taxa por endpoint).
- Risco R-019: o cabeçalho de travessia de tenant é o ponto mais sensível da API.
- Riscos R-023 (a infraestrutura é o caminho crítico e não é código) e **R-024 (o MVP-0 completo não
  cabe na janela de outubro)**. Bloqueio B-008: replanejamento e decisão sobre o piloto.
- Premissas PRE-25 (âncora de estimativa) e PRE-26 (dedicação ao projeto).
- Riscos R-020 (nada impede tecnicamente o provedor de assinar com o certificado do cliente),
  R-021 (a trilha é a palavra do provedor, sem verificação por terceiro) e R-022 (sem política de
  dependências). Bloqueios B-006 e B-007, ambos decisões de produto de Frederico.

### Resolvido
- PD-03 (armazenamento de ícones): arquivo referenciado por `icon_ref` e servido por
  `GET /v1/applications/{id}/icon` com `ETag`, em vez de binário no banco.
- Tarefas T-005 (medições obrigatórias no dogfood) e T-006 (ingresso das estações no domínio e GPOs).
- Riscos R-007 (densidade de requisitos Must no MVP-0), R-008 (parque em Windows 10 fora de suporte),
  R-009 (contagem de licenças incorreta), R-010 (rede privada adiando o RD Gateway), R-011 (área de
  transferência como caminho de exfiltração no Caminho B) e R-012 (auditoria bloqueante e disco cheio).
- Riscos arquiteturais R-013 (`rdpsign` como gargalo e amarra ao Windows), **R-014 (revogação não
  alcança sessão aberta no MVP-0)**, R-015 (prelaunch não medido), R-016 (pico de início às 8h),
  R-017 (reconciliação como única defesa da contagem) e R-018 (portabilidade não comprovada).
- Riscos R-004 (mitigado parcialmente) e R-005 (fechado) atualizados.
- Respostas de descoberta P1–P8 (escala, identidade, inventário, infraestrutura, acesso externo,
  cofre de certificados, isolamento, prazo e custo). Bloqueio B-001 encerrado.
- Direção de arquitetura fixada para identidade, topologia, acesso externo e isolamento —
  **pendente de ratificação em ADR-0001..0004** (RP-07). Nenhuma alteração de escopo ou stack ocorreu.
- Risco R-005: acúmulo de controlador de domínio e RD Session Host na mesma máquina.
