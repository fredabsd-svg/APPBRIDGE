# Changelog

Todas as mudanças relevantes do projeto AppBridge são registradas aqui.
Formato: [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) · Versionamento: SemVer
independente por componente (RP-03).

## [Não publicado]

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
