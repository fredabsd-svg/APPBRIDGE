# Changelog

Todas as mudanças relevantes do projeto AppBridge são registradas aqui.
Formato: [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) · Versionamento: SemVer
independente por componente (RP-03).

## [Não publicado]

### Segurança — troca por access token recente e de uso único (ADR-0023, S020)
- `POST /v1/auth/session` passa a exigir o access token do Entra para a API do AppBridge:
  - `aud` igual à API;
  - `scp` com `access_as_user`;
  - `azp`/`appid` igual ao launcher;
  - `iat` de até 10 min.

  O ID token deixa de ser aceito (RC-05).
- O identificador do token (`uti`/`jti`) é gravado como SHA-256 em `identity_token_redemption`, a tabela
  da migração `IdentityTokenRedemption`. Uma segunda troca responde `401` e grava
  `IDENTITY_TOKEN_REPLAYED` na trilha. A rotina diária remove os registros vencidos.
- O metadado OpenID do Entra passa a ser cacheado por processo (RC-02).
- O launcher envia o access token e serializa a renovação entre instâncias com uma trava de arquivo.
  A instância que chega depois adota o par já renovado em vez de reapresentar o refresh consumido (RC-03).
- ADR-0021 e ADR-0022 foram aceitos por decisão delegada. O ADR-0017 §1 foi substituído pelo ADR-0023.
  Suíte com 67 testes e 88,78% de cobertura.
- **Implantação:** `IdentityProvider__Audience` muda para o identificador da API, e passam a ser exigidos
  `IdentityProvider__ClientApplicationId` e o escopo exposto no Entra. Veja o roteiro operacional §3.

### Adicionado — reconciliação de sessões (T-602, S019)
- `ISessionBackend.ListActiveSessionsAsync(hosts)`. O `RdsSessionBackend` consulta `Get-RDUserSession`
  no broker de `RdsSession:ConnectionBroker`, traduz a conta em SID no próprio Windows e devolve JSON
  compatível com o PowerShell 5.1. O `RdsSessionListParser` aceita lista, objeto único e resposta vazia.
- `SessionReconciler`, serviço de fundo com ciclo de 60 s (PRE-30), roda para cada tenant ativo:
  - vincula a sessão pendente pelo host e pelo SID do usuário e grava `session_started`;
  - atualiza `last_seen_at` das sessões presentes;
  - fecha com `reconciled_missing` as sessões ausentes, e as pendentes vencidas com o motivo `never_connected`;
  - grava `session_ended` com duração e host (RF-038).
  O backend é consultado fora da transação, e as travas por usuário são as mesmas do `SessionRegistry`.
- Se o backend falha, nada é fechado por ausência. Só o que está sem sinal há mais de 30 min (PRE-31)
  é fechado como `stale_expired`.
- Registrado o ADR-0022 (**proposto**). ARQUITETURA §4.2, MODELO-DE-DADOS §6.2, ROADMAP, STATUS e o
  roteiro operacional foram atualizados. Suíte com 58 testes e 88,63% de cobertura. O PRE-23 continua
  dependendo de Connection Broker real.

### Adicionado — registro de sessão no lançamento (T-601, S018)
- `SessionRegistry` reutiliza a sessão aberta do usuário em host `online` ou `draining` do pool do
  aplicativo (RF-024). Sem sessão, pede ao `ISessionBackend` só a escolha do host. O posicionamento é
  serializado por usuário com `pg_advisory_xact_lock`, e prelaunch e clique simultâneos passam a gerar
  uma sessão só.
- O lançamento concedido registra a sessão depois da assinatura, na mesma transação do `launch`, e
  preenche `launch.session_id`. A sessão nasce com `backend_session_id` nulo (vínculo pendente) e ocupa
  vaga por `SessionRegistry__PendingBindingMinutes` minutos (padrão 10, PRE-29) até a T-602 vinculá-la.
- Migração `SessionPendingBinding`: `session.backend_session_id` passa a aceitar nulo. O rollback grava
  texto vazio nas linhas pendentes.
- `RdsSessionBackend` fica só com a seleção de host e conta como carga as sessões pendentes dentro da
  janela. `CancelSessionAsync` fecha a sessão sem vínculo sem chamar o host.
- Registrado o ADR-0021 (**proposto**). ARQUITETURA §4.2, MODELO-DE-DADOS §6.2, API (lançamento),
  ROADMAP e roteiro operacional foram atualizados.

### Corrigido — revisão de código (S018)
- **Crítico:** `TenantContextMiddleware` recebia `CancellationToken` como parâmetro de `InvokeAsync`.
  O `UseMiddleware` tenta resolvê-lo pelo contêiner, então toda requisição HTTP respondia 500, inclusive
  `/health`. Agora o middleware usa `HttpContext.RequestAborted`, e um teste passa pelo pipeline real do
  `UseMiddleware`. O teste falha no código anterior e passa no novo. Um smoke HTTP local confirmou
  `/health` com 200 e as rotas protegidas com 401.
- A chave de idempotência do lançamento passa a ser do usuário: outro usuário do tenant que a
  reapresente recebe `IDEMPOTENCY_CONFLICT` em vez do `.rdp` assinado.
- As regex de host, UPN e alias do `.rdp` terminam em `\z`. Com `$`, um `\n` final era aceito.
- O launcher chama `mstsc.exe` pelo caminho absoluto do diretório do sistema.
- Suíte com 52 testes e cobertura de linhas de 88,18%. Os builds Release do Control Plane e do Launcher
  terminam sem aviso. Os achados abertos estão em `STATUS.md` §5.1.

### Alterado — página do repositório e marca (S017, S018)
- S017: o README virou página de produto e a marca foi publicada em `docs/brand/mark.svg`. O log da
  S017 citava arquivos que não entraram no commit. Isso foi registrado em `STATUS.md` §10.
- S018: a marca foi redesenhada como uma ponte de arco com a janela de aplicativo sobre o tabuleiro,
  porque a anterior lia como mesa. Ganhou variante para fundo escuro (`mark-dark.svg`), PNG de 512 px e
  prévia social de 1280 × 640. O README ganhou diagrama do fluxo em tema claro e escuro, selos, seção de
  segurança e LGPD, marcos e mapa da documentação. Nenhum requisito muda.

### Adicionado — sessão renovável do launcher (T-303, T-803, S016)
- Criadas as tabelas `auth_session` e `auth_refresh_token`, com isolamento por tenant e FK composta.
  Refresh tokens têm 256 bits aleatórios, rotacionam a cada uso e ficam no banco somente como SHA-256;
  reapresentar um token consumido revoga a sessão.
- Access JWT agora inclui `sid`; cada chamada autenticada confere sessão, tenant, usuário e expiração.
  Logout revoga a sessão e grava o evento na mesma transação, invalidando access e refresh tokens sem
  esperar o `exp`. Uma rotina limpa sessões e hashes 30 dias depois da validade/revogação.
- `POST /v1/auth/session` passou a devolver o par de tokens; implementados `POST /v1/auth/refresh` e
  `POST /v1/auth/logout`. O refresh expira com 7 dias de inatividade e limite absoluto de 30 dias,
  ambos configuráveis entre 1 e 90 dias.
- O launcher persiste o par no Windows Credential Manager, restaura e renova sessões e oferece logout
  interativo ou `AppBridge.Launcher.exe --logout`. Se logout remoto falhar, apaga a credencial local,
  avisa que o servidor não confirmou a revogação e retorna erro; o grant remoto expira pelos seus limites.
- Adicionada a migração `AuthenticationSessions`; contrato da API, modelo, arquitetura, segurança,
  roteiro operacional, status, roadmap e auditoria atualizados; registrados os detalhes no ADR-0020.
- Build Release do Control Plane, suíte (42 testes; cobertura 87,44%), build do Launcher e
  `./scripts/check-docs.sh` aprovados. Serviços e middleware foram exercitados com PostgreSQL local;
  rota HTTP completa, Credential Manager e MSAL ainda precisam de validação em Windows/Entra real.

### Adicionado — entrega autorizada de ícones PNG (T-404, S015)
- Implementado `GET /v1/applications/{id}/icon`, que só serve ícones de aplicativos publicados para
  usuários com permissão vigente; aplicativos inexistentes, não publicados ou não autorizados não
  são enumeráveis pela rota.
- `icon_ref` agora é um caminho relativo a `CatalogAssets:RootPath` (padrão `data/icons`). O serviço
  bloqueia caminhos absolutos, travessia, links simbólicos, arquivos sem assinatura PNG e arquivos
  maiores que 1 MiB.
- Respostas PNG recebem `ETag` SHA-256 forte e `Cache-Control: private, max-age=86400`; tags
  correspondentes retornam `304`. Erros distintos indicam app não disponível ou ícone ausente/inválido.
- Atualizados contrato, roteiro operacional, backlog, status e auditoria da sessão.
- Build Release sem warnings; 38 testes passaram com 85,54% de cobertura de linhas. A checagem
  documental desta sessão passou.

### Adicionado — revalidação do catálogo autorizado (T-403, S014)
- `GET /v1/applications` calcula um `ETag` forte determinístico sobre o JSON de aplicativos que o
  usuário pode ver. A ordenação por nome e UUID mantém a representação estável.
- `If-None-Match` agora aceita lista de tags, comparação fraca e `*`; conteúdo inalterado responde
  `304 Not Modified` sem corpo. As respostas definem `Cache-Control: private, no-cache` para evitar
  cache compartilhado do catálogo por usuário.
- Adicionados testes unitários da representação, da mudança do ETag quando o corpo muda e das formas
  aceitas de `If-None-Match`.
- Build Release sem warnings; 28 testes passaram com 84,98% de cobertura de linhas. `./scripts/check-docs.sh`
  também passou.

### Adicionado — fatia de software do MVP-0a e launcher mínimo (S013)
- Criado `AppBridge.Launcher`, um executável de console .NET 10 que autentica interativamente com
  MSAL com um escopo delegado configurado, troca o ID token pela sessão AppBridge, lista somente
  apps autorizados e chama `mstsc.exe` com `.rdp` assinado temporário. O access token do escopo não
  é enviado à API; tokens e segredos não são gravados em disco (ADR-0019).
- Implementados OIDC tenant-mapping, JWT de sessão, autorização no momento do lançamento, seed de
  catálogo, políticas de redirecionamento e backend RDS por interface; respostas de lançamento ficam
  em `launch_idempotency` por 60 s e corpos vencidos são limpos pelo pruner (ADR-0017/0018).
- Adicionadas migrações para FKs compostas por tenant e idempotência persistida. As migrações foram
  aplicadas e revertidas no PostgreSQL local; os 26 testes de integração passam com 84,56% de
  cobertura de linhas (fora `Program.cs` e migrações geradas).
- `GET /health` respondeu `200 Healthy` em smoke test HTTP local com `X-Correlation-Id`. O descritor
  RDP rejeita host/UPN que possam alterar sua sintaxe; indisponibilidade de metadados OIDC retorna
  `503 IDENTITY_PROVIDER_UNAVAILABLE`.
- Adicionado workflow GitHub Actions para restaurar, compilar e testar o Control Plane e compilar o
  launcher; `docs/operacao/desenvolvimento-control-plane.md` registra as configurações e pré-requisitos.
- O aceite real ainda depende de Entra, estação Windows, host RDS/Connection Broker, certificado e
  execução de V-01/V-05/V-06; `STATUS.md` mantém essas pendências explícitas.

### Adicionado — persistência inicial do Control Plane (T-202, S012)
- Configurados EF Core 10.0.12, o provedor PostgreSQL Npgsql 10.0.3 e a ferramenta local `dotnet-ef`
  10.0.12.
- Criados o `AppDbContext`, a fábrica de design-time e o registro de conexão do PostgreSQL no Control
  Plane; conexão local pode ser sobrescrita por `ConnectionStrings__AppBridge`.
- `SaveChanges` avança `row_version` e atualiza `updated_at` em linhas mutáveis, mantendo a
  concorrência otimista prevista no ADR-0011.
- Gerada a migração inicial com 15 tabelas MVP-0; as 14 tabelas vinculadas a tenants já incluem
  `tenant_id`, chave candidata `(tenant_id, id)` e FK para `tenant`.
- A migração contém verificações de enum e retenção, índices únicos/parciais e campos de auditoria.
  A validação de exceção da política de redirecionamento é pendência PD-06 para T-501.
- O build do Control Plane passou sem warnings ou erros. A aplicação e reversão da migração não foram
  executadas porque não há PostgreSQL local ativo; T-202 continua em andamento.
- `MODELO-DE-DADOS.md` foi alinhado aos mínimos de retenção do ADR-0007 e ao escopo por tenant de
  `signing_certificate`. `application_permission` não recebe exclusão lógica, conforme ADR-0011, e a
  regra geral de campos de auditoria passou a explicitar essa exceção.

### Ambiente de desenvolvimento — S011
- Instalado o SDK .NET 10.0.401 em `/home/fred/.dotnet`; `.bashrc` e `.profile` configurados para
  exportar `DOTNET_ROOT` e incluir o SDK no `PATH` do usuário.
- `dotnet build src/AppBridge.ControlPlane/AppBridge.ControlPlane.csproj` passou sem warnings ou
  erros. A chamada `WriteAsJsonAsync` foi ajustada à sobrecarga do .NET 10.
- A verificação de execução de `/health` e a demonstração do rastreamento de um lançamento seguem
  pendentes.

### Adicionado — base do Control Plane (T-201, S010)
- Criado o projeto ASP.NET Core em `src/AppBridge.ControlPlane`, com endpoint `/health`, resposta de
  erro interna em Problem Details e logs JSON estruturados.
- Adicionado middleware para receber ou gerar `X-Correlation-Id`, devolver o mesmo cabeçalho e
  incluir `correlationId` nos logs de requisição.
- O perfil local escuta apenas em `127.0.0.1:5080`; o README descreve como iniciar o projeto.
- Ao fim da S010, o build estava pendente por falta do SDK; foi resolvido na S011. E-01 e G-01
  continuam pendentes; a sequência antecipada está registrada em `STATUS.md` e no R-023.

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
