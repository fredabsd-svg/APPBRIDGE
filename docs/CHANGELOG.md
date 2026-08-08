# Changelog

Todas as mudanças relevantes do projeto AppBridge são registradas aqui.
Formato: [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) · Versionamento: SemVer
independente por componente (RP-03).

## [Não publicado]

### Adicionado
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
