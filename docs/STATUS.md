# STATUS — AppBridge
> Estado vivo do projeto. Atualizado ao fim de toda sessão (RA-02).

**Última atualização:** 2026-08-08 · **Sessão atual:** S001 · **Fase:** Design e Arquitetura (MVP-0)

---

## 1. Onde estamos

Perguntas de descoberta P1–P8 respondidas. `VISAO.md` **aprovado por Frederico em 2026-08-08**.
`REQUISITOS.md` (entregável 2) produzido e **submetido, aguardando aprovação** — 86 RFs (dos quais 10
declarados fora de escopo) e 53 RNFs, todos com origem rastreável e classificação MoSCoW por fase.

**Frederico delegou as decisões pendentes** ("você decide", 2026-08-08). Em consequência, foram
escritos e aceitos os **ADR-0001 a ADR-0008**, e `REQUISITOS.md` foi emendado (ver §8 daquele
documento). Não há mais decisão de arquitetura pendente para iniciar `ARQUITETURA.md`.

**Nada de código de produção existe ou deve existir nesta fase.**

## 2. Entregáveis da fase de design

| # | Entregável | Estado |
|---|-----------|--------|
| 1 | `docs/VISAO.md` | ✅ **aprovado** (2026-08-08) |
| 2 | `docs/REQUISITOS.md` | 🔵 submetido — aguardando aprovação (emendado por ADR-0001..0008) |
| 3 | `docs/ARQUITETURA.md` | ⬜ não iniciado — desbloqueado, depende só da aprovação do nº 2 |
| 4 | `docs/MODELO-DE-DADOS.md` | ⬜ não iniciado |
| 5 | `docs/API.md` | ⬜ não iniciado |
| 6 | `docs/SEGURANCA.md` | ⬜ não iniciado |
| 7 | `docs/ROADMAP.md` + backlog MVP-0 | ⬜ não iniciado |

Legenda: ⬜ não iniciado · 🟡 em produção · 🔵 submetido, aguardando aprovação · ✅ aprovado

## 3. Próximos passos

1. **Frederico aprova ou devolve `REQUISITOS.md` (emendado) e os ADR-0001..0008.** ADR aceito é imutável (RA-05): discordância vira ADR novo que substitui, não edição.
2. Produzir `ARQUITETURA.md` (entregável 3) — desbloqueado.
3. Iniciar T-001 (tabela de licenciamento dos apps), pré-condição de viabilidade do Caminho B.
4. Antes do piloto: revisar ADR-0003 (o acesso por malha privada não é vendável a cliente) e a condição 4 do ADR-0008 (área de transferência liberada, com dado de terceiros em jogo).

## 4. Bloqueios ativos

| ID | Bloqueio | Impede | Responsável |
|----|----------|--------|-------------|
| ~~B-001~~ | ~~Perguntas P1–P8 sem resposta~~ | — | **Encerrado em 2026-08-08** |
| ~~B-002~~ | ~~Aprovação de `VISAO.md`~~ | — | **Encerrado em 2026-08-08 — aprovado** |
| B-003 | Em P4, a frase "serve para provar o conceito com 2–3 sessões" ficou sem sujeito — qual máquina/ambiente? | Detalhamento da topologia em ADR-0002 | Frederico |
| B-004 | Aprovação de `REQUISITOS.md` emendado e dos ADR-0001..0008 | Entregável 3 (`ARQUITETURA.md`) | Frederico |
| ~~B-005~~ | ~~Questões abertas de `REQUISITOS.md` §7~~ | — | **Encerrado em 2026-08-08** — 3 de 5 decididas por ADR-0007/0008; as outras 2 dependem de levantamento (T-001, parque de estações), não de decisão |

## 5. Tarefas abertas

| ID | Tarefa | Origem | Responsável | Criticidade |
|----|--------|--------|-------------|-------------|
| T-001 | Tabela **app × versão × tipo de licença × multiusuário S/N**, com confirmação **por escrito** de Domínio/Thomson Reuters e Alterdata sobre execução em servidor de terminal; Office só via licença por volume (LTSC) ou M365 Apps com ativação em computador compartilhado — OEM/varejo não serve; ERPs de clientes: cláusula de instalação em servidor | P3 | Frederico + AppBridge (levantamento) | **Crítica — pré-condição do Caminho B (R-001)** |
| T-002 | Minuta do termo de autorização de custódia e uso de certificado digital, revisada por advogado | P6 | Frederico / jurídico | Alta — bloqueia DIF-01 em produção |
| T-003 | Cotação SPLA atual em revendedor, para validar PRE-05 (custo ≤ R$ 50/usuário/mês) | P8 | Frederico | Alta |
| T-004 | Verificar estágio comercial das ofertas de nuvem de Domínio/Thomson Reuters e Alterdata (PRE-06) | RM-04 | Frederico | Média |

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
| PRE-20 | Tokens A3 do escritório funcionam redirecionados para a sessão | ADR-0008 | teste prático no dogfood |

## 8. Riscos registrados

| ID | Risco | Severidade | Estado |
|----|-------|-----------|--------|
| R-001 | Licença de Domínio/Alterdata pode vedar execução multiusuário em servidor de terminal — invalida o Caminho B | **Crítica** | Aberto — mitigação em T-001 |
| R-002 | Custódia centralizada de A1 tem exposição jurídica antes da técnica | Alta | Aberto — mitigação em T-002 |
| R-003 | SPLA/RDS SAL como custo fixo pode inviabilizar PRE-05 | Alta | Aberto — mitigação em T-003 |
| R-004 | Fosso só chega em V2/V3; MVP-0 e MVP-1 não se distinguem de um RDS bem configurado | Alta | **Mitigado parcialmente por ADR-0006** — metering mínimo antecipado para MVP-1. MVP-0 segue sem diferencial, por decisão |
| R-005 | DC + RD Session Host na mesma máquina obriga logon local de usuários finais no controlador de domínio | Alta | **Fechado por ADR-0002** — VMs separadas; virou RNF-007 |
| R-009 | Contagem de licenças incorreta é pior que contagem nenhuma: sem detecção confiável de fim de sessão, o contador infla e o AppBridge passa a impedir trabalho legítimo | Alta | Aberto — mitigações obrigatórias definidas em ADR-0006 |
| R-010 | A rede privada em malha é confortável demais e pode adiar o RD Gateway indefinidamente, levando o piloto comercial a chegar sem caminho de acesso vendável | Média-alta | Aberto — revisão do ADR-0003 é pré-requisito do piloto |
| R-011 | Área de transferência liberada (ADR-0008) é caminho de exfiltração sem rastro. Aceito no dogfood, onde o dado é do próprio escritório; **muda de natureza no Caminho B**, com dado de terceiros | Média-alta | Aberto — revisão obrigatória antes do piloto; deve constar em `SEGURANCA.md` |
| R-012 | Auditoria bloqueante (ADR-0007) transforma disco cheio em indisponibilidade de novos lançamentos | Média | Aberto — exige alerta de espaço em disco e expurgo funcionando desde o MVP-0 |
| R-006 | Execução solo de quatro componentes com MVP-0 previsto em ~2 meses | Alta | Aberto |
| R-007 | O MVP-0 acumula 34 RFs "Must" (RF-001..RF-040 sem os Should/Could) para ~2 meses de execução solo. Se algo tiver de sair, os candidatos naturais são RF-016, RF-026, RF-032, RF-033, RF-034 e RF-040 — todos Should/Could, nenhum Must. Corte de Must exige ADR | Alta | Aberto — decisão de escopo de Frederico |
| R-008 | Windows 10 saiu do suporte padrão em out/2025 (PRE-16). Estação sem atualização de segurança é risco do lado do cliente que o AppBridge não elimina — apenas reduz, por manter dado e aplicativo no servidor | Média | Aberto — depende de B-005 |
| RM-01..RM-10 | Riscos de mercado — ver `VISAO.md` §6 | vários | Aberto |

## 9. Violações de processo detectadas (RA-06)

Nenhuma.
