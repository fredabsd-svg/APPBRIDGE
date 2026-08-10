# STATUS — AppBridge
> Estado vivo do projeto. Atualizado ao fim de toda sessão (RA-02).

**Última atualização:** 2026-08-10 · **Sessão atual:** S010 · **Fase:** implementação do MVP-0a — **código iniciado**

---

## 1. Onde estamos

> **FASE DE DESIGN ENCERRADA em 2026-08-08.** Os 7 entregáveis foram **aprovados por Frederico** e
> **17 ADRs** estão aceitos (13 no fechamento do design + ADR-0014 a ADR-0017, os quatro últimos
> nascidos durante a implementação do código). O replanejamento do cronograma foi aprovado na **opção A** e ratificado
> em **ADR-0013**: MVP-0 dividido em duas etapas, piloto em abr–jun/2027.
>
> **O código começou em 2026-08-10 (S010).** `src/AppBridge.ControlPlane.Api` — .NET 10, primeira
> tarefa (**T-201**) concluída, com testes. **T-202 a T-207 também concluídas na mesma sessão** — EF
> Core + PostgreSQL + primeira migração (aplicada e revertida contra banco real), o filtro global de
> isolamento por tenant (ADR-0004), as chaves estrangeiras compostas que impedem escrita cruzada de
> tenant no banco (ADR-0011 §4), o `AuditWriter` transacional que nega a operação inteira quando o
> registro de auditoria não pode ser gravado (ADR-0007), a suíte nomeada de V-02 (violação de
> tenant) e o teste de que a contagem de RF-062 não soma prelaunchs (ADR-0016 Gap 1). **E-02 ·
> Fundação do Control Plane está completo.**
>
> **T-301 também concluída na mesma sessão** — `POST /v1/auth/session`, o primeiro endpoint real de
> E-03. Encontrou e fechou uma lacuna real entre `API.md` e `MODELO-DE-DADOS.md` (nenhuma tabela
> para o `refreshToken` que o contrato já prometia): **ADR-0017** decide token de sessão em JWT
> HS256 e `refreshToken` opaco guardado só como hash, em nova tabela `refresh_token`. Nenhuma
> integração real com Entra ID/AD DS ainda (infraestrutura de E-01 não existe) — um
> `DevIdentityProvider` viabiliza rodar e testar o endpoint só sob `Development`.
>
> **T-303 também concluída** — `POST /v1/auth/refresh` (rotação: o token apresentado é revogado ao
> emitir um novo) e `POST /v1/auth/logout` (idempotente, grava `access_event`), escopo restrito ao
> servidor — o armazenamento no Windows Credential Manager (RF-005) é T-803, do launcher, que ainda
> não existe. **Dois bugs reais encontrados e corrigidos, não invenção de escopo**: `CreatedAt`/
> `UpdatedAt` nunca eram gravados em nenhuma entidade desde T-202 (todo `INSERT` persistia
> `-infinity` silenciosamente) — corrigido de uma vez em `AppBridgeDbContext.SaveChanges(Async)`;
> e os DTOs de requisição de T-301/T-303 não exigiam seus campos, deixando um corpo malformado virar
> `500` em vez de `400` — corrigido com `required`. **49 testes automatizados no total.** Ver
> `docs/SETUP-DEV.md` para o ambiente de desenvolvimento (.NET 10 SDK e PostgreSQL 16 locais). Ver §3
> para a correção de sequenciamento: E-02 não esperava mais o hardware do que a própria estrutura do
> código exigia.
>
> **T-304 também concluída** — `IAuthorizationService`/`AuthorizationService`
> (`AppBridge.ControlPlane.Infrastructure/Authorization/`), o componente que `ARQUITETURA.md` §5.2
> já documentava para decidir "permissão vigente?". Sem cache: lê `EffectiveFrom`/`EffectiveTo` de
> `ApplicationPermission` direto do banco a cada chamada, o que torna o RNF-030 (revogação nega o
> lançamento seguinte em ≤ 60 s) verdadeiro por construção, sem necessidade de espera de relógio nos
> testes. Nenhum endpoint consome o serviço ainda — isso é E-05, que ainda não começou; T-304 é só a
> lógica de decisão, registrada em `Program.cs` e verificada de pé (`dotnet run`) sem erro de
> resolução de DI. **55 testes automatizados no total.** Com T-304, **E-03 fica com apenas T-302
> (vínculo por SID) em aberto.**


## 2. Entregáveis da fase de design — ✅ concluída

| # | Entregável | Estado |
|---|-----------|--------|
| 1 | `docs/VISAO.md` | ✅ aprovado (2026-08-08) |
| 2 | `docs/REQUISITOS.md` | ✅ aprovado (2026-08-08) |
| 3 | `docs/ARQUITETURA.md` | ✅ aprovado (2026-08-08) |
| 4 | `docs/MODELO-DE-DADOS.md` | ✅ aprovado (2026-08-08) |
| 5 | `docs/API.md` | ✅ aprovado (2026-08-08) |
| 6 | `docs/SEGURANCA.md` | ✅ aprovado (2026-08-08) |
| 7 | `docs/ROADMAP.md` | ✅ aprovado (2026-08-08), replanejado por ADR-0013 |

### 2.1 Marcos vigentes (ADR-0013)

| Marco | Data | Conteúdo |
|-------|------|----------|
| M1 · Design fechado | ✅ 2026-08-08 | 7 entregáveis, 13 ADRs |
| **M2a · MVP-0a** | meados de out/2026 | Esqueleto ambulante: um usuário, um aplicativo, ponta a ponta |
| M2b · MVP-0b | dez/2026–jan/2027 | Dogfood real, CS-01 a CS-04 |
| M2c · MVP-1 (subconjunto) | fev–mar/2027 | **Escopo a definir — B-009** |
| M3 · Piloto Caminho B | **abr–jun/2027** | 3–5 escritórios, portões G-01 a G-05 cumpridos |

## 3. Próximos passos — implementação do MVP-0a

**Correção de sequenciamento (2026-08-10, S010) — não é mudança de escopo, é ordem de execução.**
A linha 7 desta tabela dizia que E-02 (Control Plane) "depende da infraestrutura existir". Isso vale
para o *deploy* real e para os testes de integração contra AD DS/RDS verdadeiros — não para o
esqueleto do código, que só precisa de um PostgreSQL de desenvolvimento. Corrigido: **E-02 corre em
paralelo com E-01**, não depois.

Ordem do que ainda é sequencial: **o que não é código continua vindo primeiro**, porque é caminho
crítico (R-023) e porque um resultado negativo em G-01 pouparia meses de construção — mas isso não
significa que o código espera.

| Ordem | Ação | Tarefa | Por que agora |
|-------|------|--------|---------------|
| 1 | **Registro de licenças** — preencher a matriz com o inventário do dogfood; minuta da declaração de titularidade para o piloto, junto com T-002 | T-001 / G-01 | Reorientada por ADR-0014: não depende mais de fornecedor, mas o resíduo de R-001 permanece |
| 2 | **Aquisição** de host, Windows Server 2025 e RDS CALs — 🟡 **especificação pronta** em `operacao/E-01-infraestrutura/T-101-especificacao-de-aquisicao.md`; falta cotar e comprar | T-101 | **Única dependência externa restante do início.** Prazo de entrega define se M2a se sustenta |
| 3 | **Cotação SPLA** | T-003 | Valida PRE-05 e a viabilidade econômica do piloto |
| 4 | VMs separadas, domínio, RDS, FSLogix, AppLocker — 🟡 **roteiro pronto** em `operacao/E-01-infraestrutura/roteiro-implantacao.md` | T-102 a T-105 | Épico E-01, caminho crítico. Depende do equipamento |
| 5 | Ingresso das estações e GPOs — roteiro pronto | T-106 | A tarefa que mais facilmente estoura o prazo; sequenciar cedo |
| 6 | Varredura externa | T-107 / V-01 | Comprova CS-04. Pode correr em paralelo a partir de T-102 |
| ~~—~~ | ~~**Fundação do Control Plane**~~ | E-02 | **Concluído em 2026-08-10 (S010)** — T-201 a T-207, 29 testes |
| ~~—~~ | ~~**`POST /v1/auth/session`**~~ | T-301 | **Concluído em 2026-08-10 (S010)** — ADR-0017 (token de sessão, `refresh_token`) |
| ~~—~~ | ~~**`POST /v1/auth/refresh` e `/logout`**~~ | T-303 | **Concluído em 2026-08-10 (S010)** — 49 testes no total; armazenamento cliente (RF-005) continua T-803 |
| ~~—~~ | ~~**`AuthorizationService`**~~ | T-304 | **Concluído em 2026-08-10 (S010)** — 55 testes no total; sem consumidor ainda (E-05) |
| **—** | **T-302** (vínculo por SID) segue E-03, em paralelo com E-01 pelo mesmo motivo já registrado acima | E-03 | Última tarefa aberta do épico |

**Decisões que ainda cabem a Frederico, em paralelo:** B-009 (subconjunto do MVP-1 exigido pelo
piloto), B-006 (PS-07, cofre) e B-007 (PS-03, encadeamento da trilha).

## 4. Bloqueios ativos

| ID | Bloqueio | Impede | Responsável |
|----|----------|--------|-------------|
| ~~B-001~~ | ~~Perguntas P1–P8 sem resposta~~ | — | **Encerrado em 2026-08-08** |
| ~~B-002~~ | ~~Aprovação de `VISAO.md`~~ | — | **Encerrado em 2026-08-08 — aprovado** |
| ~~B-003~~ | ~~Frase truncada em P4~~ | — | **Encerrado** — superado por ADR-0002, que fixou a topologia |
| ~~B-004~~ | ~~Aprovação dos entregáveis 2 a 7~~ | — | **Encerrado em 2026-08-08 — todos aprovados** |
| ~~B-008~~ | ~~Replanejamento do MVP-0 e do piloto~~ | — | **Encerrado em 2026-08-08 — opção A, ratificada em ADR-0013** |
| **B-009** | **Definir o subconjunto do MVP-1 exigido pelo piloto.** Sobram ~3 meses (fev–mar/2027) para os épicos E-13 a E-18, que provavelmente não cabem (R-025). Recomendação preliminar em `ROADMAP.md` §5: priorizar E-15, E-16 e a parte de permissões do E-13 | Planejamento do M2c e do piloto | Frederico |
| ~~B-010~~ | ~~Duas linhas divergentes e dois backlogs~~ | — | **Encerrado em 2026-08-10 — ADR-0016.** `main` mesclado ao branch; `ROADMAP.md` é o backlog único; T-207, T-506 e T-1106 incorporados |
| ~~B-011~~ | ~~Issue #5 contradiz o ADR-0009~~ | — | **Encerrado em 2026-08-10** — issue #5 corrigido: só `CertificateThumbprint`, sem PFX nem senha. Risco R-028 fechado |
| B-006 | **Decisão sobre PS-07** — o que impede tecnicamente o provedor de usar o certificado A1 de um cliente. Hoje: nada. As opções (segunda aprovação, senha sob custódia do titular, módulo de hardware) alteram o produto e custam | Entrada do cofre (DIF-01) em produção na V2 | Frederico + jurídico |
| B-007 | **Decisão sobre PS-03** — encadeamento criptográfico da trilha, para que ela seja verificável por terceiro. Alteraria ADR-0007 e exige ADR novo | Piloto do Caminho B | Frederico |
| ~~B-005~~ | ~~Questões abertas de `REQUISITOS.md` §7~~ | — | **Encerrado em 2026-08-08** — 3 de 5 decididas por ADR-0007/0008; as outras 2 dependem de levantamento (T-001, parque de estações), não de decisão |

## 5. Tarefas abertas

| ID | Tarefa | Origem | Responsável | Criticidade |
|----|--------|--------|-------------|-------------|
| T-001 | **Reorientada por ADR-0014** — o cliente adquire, instala e usa suas próprias licenças; **não se consulta fornecedor** (cartas removidas). Resta: preencher a matriz com o inventário declarado (app × versão × tipo de licença × quantidade) e, para o piloto, obter a **declaração de titularidade e conformidade** assinada por cada cliente | P3, ADR-0014 | Frederico | Alta — G-01 na nova forma |
| T-002 | Minuta do termo de autorização de custódia e uso de certificado digital, revisada por advogado | P6 | Frederico / jurídico | Alta — bloqueia DIF-01 em produção |
| T-003 | Cotação SPLA atual em revendedor, para validar PRE-05 (custo ≤ R$ 50/usuário/mês) | P8 | Frederico | Alta |
| T-004 | Verificar estágio comercial das ofertas de nuvem de Domínio/Thomson Reuters e Alterdata (PRE-06) | RM-04 | Frederico | Média |
| T-005 | **Medições obrigatórias no dogfood**, que a arquitetura não resolve no papel: PRE-22 (o prelaunch sustenta uma jornada de trabalho?), PRE-23 (o Connection Broker permite consultar e encerrar sessões com confiabilidade?), PRE-20 (token A3 funciona redirecionado?), PRE-11 (abertura ≤ 5 s?) | ARQUITETURA §8 | AppBridge (implementação) | **Alta — RNF-027, RF-008, RF-038, RF-062 dependem** |
| T-006 | Ingressar as estações do escritório no domínio e distribuir por GPO a delegação de credenciais, a política de redirecionamento e a impressão digital do certificado de assinatura | ADR-0008, ADR-0009, ADR-0010 | Frederico / implantação | **Alta — é a tarefa que mais facilmente estoura o prazo do MVP-0** |

## 6. Decisões de arquitetura (ADR)

Todas aceitas em 2026-08-08, por delegação de Frederico. **ADR aceito é imutável (RA-05)** — revisão
se faz com ADR novo que substitui o anterior.

| ADR | Tema | Decisão | Emenda gerada |
|-----|------|---------|---------------|
| [ADR-0001](adr/ADR-0001-identidade-ad-ds-com-entra-hibrido.md) | Identidade | AD DS como diretório-base e autoridade da conta de sessão; Entra ID como provedor de autenticação em modelo híbrido (Entra Connect Sync). Autorização é do AppBridge, não do diretório | RF-002, RF-003 |
| [ADR-0002](adr/ADR-0002-topologia-mvp0-dc-e-session-host-separados.md) | Topologia MVP-0 | DC e session host **em VMs separadas** sobre Hyper-V no mesmo host físico (Server Standard licencia 2 VMs, custo adicional zero). Control Plane pode coabitar no MVP-0, sai antes do piloto | RNF-007 |
| [ADR-0003](adr/ADR-0003-acesso-externo-mvp0-rede-privada.md) | Acesso externo MVP-0 | Rede privada em malha com ACL restringindo o alcance ao 3389 e aprovação nominal de dispositivo; zero publicação na internet; varredura externa obrigatória. **Não é solução comercial** — RD Gateway + MFA na V2 | RNF-009 |
| [ADR-0004](adr/ADR-0004-isolamento-multi-tenant-hibrido.md) | Multi-tenant | Híbrido: session host + OU + GPO dedicados por tenant; no Control Plane, filtro global no `DbContext` (não consulta a consulta) + teste de violação obrigatório | RNF-036 |
| [ADR-0005](adr/ADR-0005-ratificacao-da-stack-de-referencia.md) | Stack | Ratificada integralmente, com **gatilho de reversão** do launcher para WPF se bandeja + protocolo + atalhos custarem > 5 dias em WinUI 3 | — |
| [ADR-0006](adr/ADR-0006-antecipacao-do-metering-minimo-para-mvp-1.md) | Escopo (R-004) | **Metering mínimo antecipado de V2 para MVP-1** (RF-062..064). Fila e relatório histórico ficam em V2 | RF-062, RF-063, RF-064 |
| [ADR-0007](adr/ADR-0007-auditoria-bloqueante-e-retencao.md) | Auditoria | **Auditoria de evento de segurança é bloqueante** — mesma transação que concede o acesso. Retenção em 3 categorias (12/24/60 meses), configurável por tenant com mínimos | RNF-018, RNF-022 |
| [ADR-0008](adr/ADR-0008-politica-base-de-redirecionamento.md) | Redirecionamento | Política base do MVP-0: impressora, token USB, área de transferência e saída de áudio **permitidos**; unidades locais, COM/LPT, entrada de áudio e demais USB **negados** | RNF-014 |
| [ADR-0009](adr/ADR-0009-assinatura-do-rdp-e-hospedagem-do-control-plane.md) | Assinatura do `.rdp` | Assinatura via `rdpsign.exe` atrás da interface `IRdpFileSigner`. **Consequência assumida: o Control Plane é componente Windows** — contêiner Linux está fora enquanto esta decisão valer. Caminho de saída registrado para o Caminho A | — (detalha RF-019, RNF-002) |
| [ADR-0010](adr/ADR-0010-autenticacao-na-sessao-e-ingresso-das-estacoes.md) | Autenticação na sessão | Estações **ingressadas no domínio**, com delegação de credenciais por GPO restrita aos session hosts nominados. A senha de domínio nunca passa pelo Control Plane. Caminho degradado documentado para máquina fora do domínio | — (detalha RNF-042) |
| [ADR-0011](adr/ADR-0011-convencoes-do-modelo-de-dados.md) | Convenções do modelo de dados | UUID v7 como chave, `timestamptz` em UTC, exclusão lógica para dado de tenant e proibida para trilha, e **chave estrangeira composta com `tenant_id`** — segunda linha de defesa que impede no motor uma linha do tenant A apontar para o tenant B | — (detalha RNF-019, RNF-020, RNF-036) |
| [ADR-0016](adr/ADR-0016-backlog-unico-e-reconciliacao-das-duas-linhas.md) | **Backlog único** | `ROADMAP.md` é a fonte; `BACKLOG_MVP0A_PRIORIZADO.md` vira anexo histórico e `ANALISE_BUGS_E_MELHORIAS.md` recebe errata. Os dois gaps da linha B viram **T-207** (coluna `purpose`) e **T-506** (cancelamento), e PS-04 é antecipada como **T-1106** | ROADMAP, MODELO-DE-DADOS §7.1, ARQUITETURA §4.2 |
| [ADR-0015](adr/ADR-0015-checagem-de-consistencia-no-fechamento-de-sessao.md) | **Processo — RA-02** | Fechamento de sessão passa a exigir **checagem de consistência**, em duas metades: mecânica (`./scripts/check-docs.sh`, 7 verificações) e humana (o conteúdo ainda reflete as decisões vigentes?). Primeira alteração do prompt mestre | altera `CLAUDE.md` |
| [ADR-0014](adr/ADR-0014-licenciamento-dos-aplicativos-e-do-cliente.md) | **Licenciamento dos aplicativos** | O cliente adquire, instala e usa suas próprias licenças. O AppBridge **não consulta fornecedor nem intermedia licença**. G-01 deixa de ser confirmação escrita do fornecedor e passa a ser **declaração de titularidade e conformidade assinada pelo cliente**. Restringe NO-04 | nenhuma — não altera RF/RNF |
| [ADR-0013](adr/ADR-0013-replanejamento-do-mvp-0-e-piloto-no-segundo-trimestre.md) | **Replanejamento** | MVP-0 dividido em **MVP-0a** (esqueleto ambulante, out/2026) e **MVP-0b** (dogfood real, dez/2026–jan/2027); piloto do Caminho B em **abr–jun/2027** com 3–5 escritórios. Portões G-01..G-05 mantidos intransponíveis | marcos, não requisitos |
| [ADR-0012](adr/ADR-0012-convencoes-da-api.md) | Convenções da API | `/v1` no caminho; erro em Problem Details com código estável; `Idempotency-Key` obrigatório no lançamento; **o `tenant_id` nunca vem do cliente** — não existe parâmetro a verificar; recurso de outro tenant responde `404`; paginação por cursor | — (detalha RF-021, RF-025, RNF-036, RNF-043) |
| [ADR-0017](adr/ADR-0017-token-de-sessao-e-persistencia-do-refresh-token.md) | **Token de sessão e refresh token** | `accessToken` em JWT HS256 (chave por variável de ambiente); `refreshToken` opaco, guardado só como hash SHA-256, em tabela própria (`refresh_token`) — nunca um JWT autocontido, para que logout consiga revogar de fato. Nenhuma implementação real de `IIdentityProvider` nesta tarefa: infraestrutura de E-01 (AD DS/Entra) ainda não existe | MODELO-DE-DADOS §4.3 |

## 7. Premissas abertas (RP-05)

| ID | Premissa | Origem | Confirmar com |
|----|----------|--------|---------------|
| PRE-01 | Dogfood: ~10 usuários simultâneos, 6–8 apps | P1 | Frederico (inventário real — T-001) |
| PRE-02 | Piloto 60–100 simultâneos; 12 meses ~250 usuários e ~30 apps; RNFs dimensionados para 500 | P1 | meta de projeto |
| PRE-03 | Escritório do dogfood hoje em workgroup, sem domínio; criar AD DS é tarefa do MVP-0 | P2 | Frederico |
| PRE-04 | Não há hoje Windows Server 2025 nem RDS CALs; serão adquiridos | P4 | Frederico / cotação |
| PRE-05 | Custo total ≤ R$ 50/usuário/mês contra venda de R$ 70–150 | P8 | T-003 |
| PRE-06 | Nuvens próprias de Domínio e Alterdata existem e avançam, estágio comercial não verificado | RM-04 | T-004 |
| PRE-07 | Validade do `.rdp` temporário: 60 s | RF-020 | Frederico / medição no dogfood |
| ~~PRE-08~~ | ~~Retenção de logs~~ | RNF-018 | **Resolvida por ADR-0007** |
| ~~PRE-09~~ | ~~Auditoria não bloqueante~~ | RNF-022 | **Resolvida por ADR-0007 — decidida ao contrário: é bloqueante** |
| PRE-10 | 100 aplicativos publicados no dimensionamento para 500 usuários | RNF-026 | Frederico |
| PRE-11 | Abertura ≤ 5 s com prelaunch, ≤ 20 s sem, em rede local | RNF-027 | medição no dogfood |
| PRE-12 | Catálogo p95 ≤ 300 ms; geração+assinatura do `.rdp` p95 ≤ 1 s | RNF-028, RNF-029 | medição na implementação |
| PRE-13 | Propagação da revogação em até 60 s | RNF-030 | Frederico |
| PRE-14 | Telemetria do Agent ≤ 2% de CPU | RNF-031 | medição na V2 |
| PRE-15 | Disponibilidade-alvo 99,5% mensal no piloto | RNF-034 | Frederico (vira cláusula contratual) |
| PRE-16 | Estações Windows 10 22H2 e Windows 11, 64 bits | RNF-045 | Frederico (B-005) |
| PRE-17 | Instalação do launcher sem privilégio de administrador | RNF-044 | verificação técnica (protocolo + atalhos) — ligada ao gatilho do ADR-0005 |
| PRE-18 | O host físico suporta Hyper-V com virtualização assistida por hardware | ADR-0002 | Frederico / aquisição |
| PRE-19 | Prazos de 24 e 60 meses de retenção são escolha de engenharia, não parecer jurídico | ADR-0007 | advogado (T-002) |
| PRE-20 | Tokens A3 do escritório funcionam redirecionados para a sessão | ADR-0008 | teste prático no dogfood (T-005) |
| PRE-21 | Todas as estações rodam edição do Windows que permite ingresso em domínio (Home não permite) | ADR-0010 | levantamento do parque |
| PRE-22 | SessionPrimer + GPO de tempo de logoff sustentam o prelaunch por uma jornada de trabalho | ARQUITETURA §5.3 | medição no dogfood (T-005) |
| PRE-23 | O Connection Broker permite consultar e encerrar sessões com a confiabilidade exigida por RF-038 e RF-008 | ARQUITETURA §4.2 | validação técnica (T-005) |
| PRE-24 | Retenção de `host_telemetry`: 90 dias (não é trilha de auditoria) | MODELO-DE-DADOS §6.3 | quando o Agent existir (V2) |
| PRE-27 | 2–3 GB de RAM por sessão com Domínio + Alterdata + Excel simultâneos | E-01 / T-101 | medição no dogfood |
| PRE-28 | 20–30 GB de container FSLogix por usuário | E-01 / T-101 | medição no dogfood |
| PRE-25 | 1 ponto de estimativa ≈ meio dia de trabalho focado | ROADMAP §1.1 | primeira semana de implementação |
| PRE-26 | Dedicação de 40% a 60% do tempo útil ao projeto | ROADMAP §4 | Frederico |
| PRE-29 | TTL do `accessToken`: 15 minutos | ADR-0017 | medição no dogfood, mesma natureza de PRE-07 |
| PRE-30 | TTL do `refreshToken`: 30 dias | ADR-0017 | medição no dogfood |

## 8. Riscos registrados

| ID | Risco | Severidade | Estado |
|----|-------|-----------|--------|
| R-001 | **Reescrito por ADR-0014.** A responsabilidade de licenciamento passa a ser do cliente, o que **aloca** a exposição mas não a elimina: se um fornecedor vedar execução em servidor de sessão, a vedação continua existindo. Resíduo específico do Caminho B: termos que restringem execução em **infraestrutura operada por terceiro**, independentemente de quem detém a licença — nesse caso a declaração do cliente não protege o provedor | **Alta** (era Crítica) | Aberto — resíduo para o advogado de T-002; G-01 vira declaração assinada |
| R-002 | Custódia centralizada de A1 tem exposição jurídica antes da técnica | Alta | Aberto — mitigação em T-002 |
| R-003 | SPLA/RDS SAL como custo fixo pode inviabilizar PRE-05 | Alta | Aberto — mitigação em T-003 |
| R-004 | Fosso só chega em V2/V3; MVP-0 e MVP-1 não se distinguem de um RDS bem configurado | Alta | **Mitigado parcialmente por ADR-0006** — metering mínimo antecipado para MVP-1. MVP-0 segue sem diferencial, por decisão |
| R-005 | DC + RD Session Host na mesma máquina obriga logon local de usuários finais no controlador de domínio | Alta | **Fechado por ADR-0002** — VMs separadas; virou RNF-007 |
| R-009 | Contagem de licenças incorreta é pior que contagem nenhuma: sem detecção confiável de fim de sessão, o contador infla e o AppBridge passa a impedir trabalho legítimo | Alta | Aberto — mitigações obrigatórias definidas em ADR-0006 |
| R-010 | A rede privada em malha é confortável demais e pode adiar o RD Gateway indefinidamente, levando o piloto comercial a chegar sem caminho de acesso vendável | Média-alta | Aberto — revisão do ADR-0003 é pré-requisito do piloto |
| R-011 | Área de transferência liberada (ADR-0008) é caminho de exfiltração sem rastro. Aceito no dogfood, onde o dado é do próprio escritório; **muda de natureza no Caminho B**, com dado de terceiros | Média-alta | Aberto — revisão obrigatória antes do piloto; deve constar em `SEGURANCA.md` |
| R-012 | Auditoria bloqueante (ADR-0007) transforma disco cheio em indisponibilidade de novos lançamentos | Média | Aberto — exige alerta de espaço em disco e expurgo funcionando desde o MVP-0 |
| R-013 | `rdpsign` como processo por lançamento pode virar gargalo sob carga, e prende o Control Plane ao Windows | Média | Aberto — medir cedo (PRE-12); caminho de saída em ADR-0009 |
| R-014 | **Sem RF-008 no MVP-0, revogar acesso não encerra sessão aberta.** Demissão exige desabilitar a conta no AD e encerrar a sessão manualmente no host | **Alta** | Aberto — precisa constar no roteiro operacional do MVP-0 |
| R-015 | O prelaunch depende de comportamento de tempo de logoff do RDS ainda não medido; dele depende RNF-027 e a evidência de VP-02 | Alta | Aberto — T-005 |
| R-016 | O Control Plane não bloqueia trabalho em andamento, mas bloqueia começar a trabalhar — e o pico de início é às 8h | Média-alta | Aberto — reforça RNF-033 e RNF-040 |
| R-017 | `SessionReconciler` é a única defesa contra contagem inflada de licença antes do Agent | Média | Aberto — ligado a R-009 |
| R-018 | A portabilidade prometida por RNF-035 é hipótese até existir uma segunda implementação de `ISessionBackend` que a prove | Média | Aberto — aceito conscientemente |
| R-019 | O cabeçalho `X-AppBridge-Acting-Tenant` é o ponto mais sensível da API: falha na verificação do papel transforma o mecanismo de suporte multiempresa em porta de travessia de tenant | **Alta** | Aberto — exige teste dedicado de negativa e revisão de código específica (ADR-0012, V-03) |
| R-020 | **Nada impede tecnicamente o provedor de assinar com o certificado A1 de um cliente** (AM-33). A proteção é contratual e de detecção, não de prevenção — e o DIF-01 é vendido como diferencial | **Crítica** | Aberto — B-006 / PS-07, antes de o cofre ir a produção |
| R-021 | A trilha é mantida pelo próprio provedor. Sem encadeamento criptográfico ou carimbo de tempo independente, num litígio ela é a palavra dele (AM-12) | Alta | Aberto — B-007 / PS-03, antes do piloto |
| R-022 | Sem política de dependências, uma biblioteca comprometida entra no launcher ou no Control Plane sem barreira (AM-32) | Média | Aberto — PS-06 |
| R-023 | **A infraestrutura (E-01, 34 pts) é o caminho crítico do MVP-0 e não é código** — depende de compra, de terceiros e da agenda das pessoas | **Alta** | Aberto — iniciar E-01 antes de qualquer linha de código |
| R-024 | O MVP-0 completo não cabe na janela original de P8 | Crítica | **Fechado por ADR-0013** — replanejado em duas etapas, opção A |
| R-026 | Duas linhas divergentes e dois conjuntos de identificadores | Alta | **Fechado por ADR-0016** |
| R-027 | O backlog paralelo começava pelo código, sem issue para o épico E-01 | Alta | **Mitigado em 2026-08-10** — issues #35 a #39 criados; #8 marcado como bloqueado por #37 |
| R-028 | Issue #5 reintroduziria material de chave em arquivo | Alta | **Fechado em 2026-08-10** — issue corrigido |
| R-030 | **O MVP-0a real pode ser maior que qualquer das duas estimativas.** A linha B estimou 96 pts **sem** infraestrutura; a linha A, ~95 pts **com** ela. Somado o que cada uma cobre, aproxima-se de **130 pts** — contra a data de out/2026 do ADR-0013 | **Alta** | Aberto — reavaliar M2a |
| R-031 | O caminho de falha do prelaunch é o menos exercitado do sistema e o que mais deixa estado inconsistente — foi onde o Gap 2 se escondeu | Média | Aberto — T-506 exige teste que **force** a falha |
| R-029 | **Dois gaps confirmados na documentação aprovada:** `purpose` existe em `API.md` e não no modelo de dados (metering contaria prelaunch como uso real); `ISessionBackend` sem operação de cancelamento (prelaunch falho deixa sessão zumbi). | **Média-alta** | **Corrigidos nas fontes por ADR-0016** — `purpose` em `MODELO-DE-DADOS.md` §7.1 e `CancelSessionAsync` em `ARQUITETURA.md` §4.2; implementação em T-207 e T-506 |
| R-032 | **`DevIdentityProvider` (ADR-0017) autentica sem verificação real.** Existe só para viabilizar `dotnet run` local nesta fase — se vazar para fora de `Development`, autentica qualquer requisição | **Alta, contida** | Aberto — registrado só sob `IHostEnvironment.IsDevelopment()`; revisão de código obrigatória antes de qualquer deploy real, mesma classe de cuidado de um `IgnoreQueryFilters()` mal colocado (ADR-0004 item 7) |
| R-025 | **O MVP-1 é o novo gargalo:** ~3 meses entre o fim do dogfood (jan/2027) e o piloto (abr/2027) para os épicos E-13 a E-18, que provavelmente não cabem | **Alta** | Aberto — B-009 |
| R-006 | Execução solo de quatro componentes com MVP-0 previsto em ~2 meses | Alta | Aberto |
| R-007 | O MVP-0 acumula 34 RFs "Must" (RF-001..RF-040 sem os Should/Could) para ~2 meses de execução solo. Se algo tiver de sair, os candidatos naturais são RF-016, RF-026, RF-032, RF-033, RF-034 e RF-040 — todos Should/Could, nenhum Must. Corte de Must exige ADR | Alta | Aberto — decisão de escopo de Frederico |
| R-008 | Windows 10 saiu do suporte padrão em out/2025 (PRE-16). Estação sem atualização de segurança é risco do lado do cliente que o AppBridge não elimina — apenas reduz, por manter dado e aplicativo no servidor | Média | Aberto — depende de B-005 |
| RM-01..RM-10 | Riscos de mercado — ver `VISAO.md` §6 | vários | Aberto |

## 9. Pendências de projeto abertas

| ID | Pendência | Origem | Resolver antes de |
|----|-----------|--------|-------------------|
| PD-01 | Política de expurgo de linhas com exclusão lógica (`deleted_at` antigo) — distinta da retenção de trilha | ADR-0011, MODELO-DE-DADOS §14 | Implementação |
| PD-02 | Row-Level Security do PostgreSQL como terceira linha de defesa de isolamento | ADR-0011 | Piloto |
| ~~PD-03~~ | ~~Onde fica o binário do ícone~~ | — | **Resolvida em `API.md` §3** — arquivo referenciado, servido por endpoint com `ETag` |
| PD-04 | Onde ficam as respostas de idempotência durante os 60 s de validade | ADR-0012, API §12 | Implementação |
| PD-05 | Limites concretos de taxa por endpoint (RNF-010) | API §12 | Depende de medição (T-005) — também PS-09 |

### 9.1 Pendências de segurança (PS-01..PS-10)

Consolidadas em `SEGURANCA.md` §5. As de maior peso, repetidas aqui por serem decisão de produto e
não de implementação:

| ID | Pendência | Prazo |
|----|-----------|-------|
| **PS-07** | Reduzir o poder unilateral do provedor sobre o cofre (R-020) | Antes do cofre ir a produção (V2) |
| **PS-03** | Encadeamento criptográfico da trilha (R-021) | Antes do piloto |
| **PS-02** | Restringir e registrar acesso direto ao banco | Antes do piloto |
| **PS-08** | Revisar a liberação de área de transferência com dado de terceiros (R-011) | Antes do piloto |
| **PS-05**, **PS-09**, **PS-10** | Permissões mínimas da conta de serviço, limites de taxa, procedimento de comprometimento do certificado de assinatura | MVP-0 |
| **PS-01**, **PS-04**, **PS-06** | Detecção de uso indevido da chave, varredura de segredos, política de dependências | MVP-1 |

## 10. Violações de processo detectadas (RA-06)

**Auditoria de consistência em 2026-08-08 (S006) — 6 achados de deriva documental, todos corrigidos.**

Nenhuma mudança silenciosa de escopo, stack, modelo de dados ou API. O que se encontrou foi
documentação que **deixou de refletir decisões já registradas** — deriva, não violação:

| # | Achado | Corrigido |
|---|--------|-----------|
| 1 | Os 7 entregáveis mantinham "aguardando aprovação" no cabeçalho, contradizendo este `STATUS.md` | ✅ |
| 2 | `VISAO.md` descrevia o modelo antigo de licenciamento em NO-04, RM-05 e R-001 | ✅ (nova §11 de emendas) |
| 3 | Contagem de ADRs desatualizada (13 citados, 14 existentes) | ✅ |
| 4 | `ARQUITETURA.md` com dependências desatualizadas | ✅ |
| 5 | `MODELO-DE-DADOS.md` descrevendo `license_notes` pelo conceito extinto | ✅ |
| 6 | `SEGURANCA.md` sem nota do ADR-0014 | ✅ |

**Recomendação aprovada e aplicada (ADR-0015):** a RA-02 do `CLAUDE.md` passou a exigir a checagem de
consistência no fechamento de sessão. A parte mecanizável está em `./scripts/check-docs.sh` — 7
verificações, código de saída 1 em caso de achado. A parte humana permanece humana: nenhuma expressão
regular teria detectado o achado nº 2.
