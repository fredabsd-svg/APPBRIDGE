# STATUS — AppBridge
> Estado vivo do projeto. Atualizado ao fim de toda sessão (RA-02).

**Última atualização:** 2026-08-08 · **Sessão atual:** S001 · **Fase:** Design e Arquitetura (MVP-0)

---

## 1. Onde estamos

Perguntas de descoberta P1–P8 respondidas por Frederico. Bloqueio B-001 encerrado.
`VISAO.md` (entregável 1) produzido e **submetido, aguardando aprovação explícita** (RP-04).

Quatro decisões de arquitetura ficaram definidas em direção pelas respostas P2, P4, P5 e P7, mas
**nenhuma foi ratificada por ADR** — ver §6. Por RP-07, os ADR-0001..0004 devem ser escritos antes de
`ARQUITETURA.md` (entregável 3).

**Nada de código de produção existe ou deve existir nesta fase.**

## 2. Entregáveis da fase de design

| # | Entregável | Estado |
|---|-----------|--------|
| 1 | `docs/VISAO.md` | 🔵 submetido — aguardando aprovação |
| 2 | `docs/REQUISITOS.md` | ⬜ não iniciado — depende da aprovação do nº 1 |
| 3 | `docs/ARQUITETURA.md` | ⬜ não iniciado — depende de ADR-0001..0004 |
| 4 | `docs/MODELO-DE-DADOS.md` | ⬜ não iniciado |
| 5 | `docs/API.md` | ⬜ não iniciado |
| 6 | `docs/SEGURANCA.md` | ⬜ não iniciado |
| 7 | `docs/ROADMAP.md` + backlog MVP-0 | ⬜ não iniciado |

Legenda: ⬜ não iniciado · 🟡 em produção · 🔵 submetido, aguardando aprovação · ✅ aprovado

## 3. Próximos passos

1. **Frederico aprova ou devolve `VISAO.md` com correções.**
2. Decidir se os ADR-0001..0004 (§6) vêm antes ou depois de `REQUISITOS.md`. Recomendação: `REQUISITOS.md` primeiro (não depende deles), ADRs em seguida, antes de `ARQUITETURA.md`.
3. Decidir sobre a recomendação de antecipar o DIF-02 (metering mínimo) para o piloto — exige ADR (R-004).
4. Iniciar T-001 (tabela de licenciamento dos apps), que é pré-condição de viabilidade do Caminho B.

## 4. Bloqueios ativos

| ID | Bloqueio | Impede | Responsável |
|----|----------|--------|-------------|
| ~~B-001~~ | ~~Perguntas P1–P8 sem resposta~~ | — | **Encerrado em 2026-08-08** |
| B-002 | Aprovação de `VISAO.md` | Entregável 2 (`REQUISITOS.md`) | Frederico |
| B-003 | Em P4, a frase "serve para provar o conceito com 2–3 sessões" ficou sem sujeito — qual máquina/ambiente? | Detalhamento da topologia em ADR-0002 | Frederico |

## 5. Tarefas abertas

| ID | Tarefa | Origem | Responsável | Criticidade |
|----|--------|--------|-------------|-------------|
| T-001 | Tabela **app × versão × tipo de licença × multiusuário S/N**, com confirmação **por escrito** de Domínio/Thomson Reuters e Alterdata sobre execução em servidor de terminal; Office só via licença por volume (LTSC) ou M365 Apps com ativação em computador compartilhado — OEM/varejo não serve; ERPs de clientes: cláusula de instalação em servidor | P3 | Frederico + AppBridge (levantamento) | **Crítica — pré-condição do Caminho B (R-001)** |
| T-002 | Minuta do termo de autorização de custódia e uso de certificado digital, revisada por advogado | P6 | Frederico / jurídico | Alta — bloqueia DIF-01 em produção |
| T-003 | Cotação SPLA atual em revendedor, para validar PRE-05 (custo ≤ R$ 50/usuário/mês) | P8 | Frederico | Alta |
| T-004 | Verificar estágio comercial das ofertas de nuvem de Domínio/Thomson Reuters e Alterdata (PRE-06) | RM-04 | Frederico | Média |

## 6. Decisões definidas em direção, pendentes de ADR (RP-07)

| ADR | Tema | Direção | Status |
|-----|------|---------|--------|
| ADR-0001 | Identidade e autenticação | AD DS como base (RDS clássico exige domain join; Entra ID puro só atende AVD) + Entra Connect; Control Plane autentica via Entra/JWT e mapeia para contas AD | a escrever |
| ADR-0002 | Topologia do MVP-0 | 1 host Windows Server 2025, 8 vCPU, 32–64 GB, **DC e session host em VMs separadas** (ver R-005) | a escrever |
| ADR-0003 | Acesso externo no MVP-0 | Sem RD Gateway; rede interna + Tailscale. RD Gateway + MFA na V2; relay tipo túnel reverso como candidato comercial | a escrever |
| ADR-0004 | Isolamento multi-tenant | Híbrido: session host + OU + GPO por escritório na camada RDS; `tenant_id` em toda tabela desde o MVP-0 | a escrever |
| ADR-0005 | Ratificação da stack de referência (Seção 3 do prompt mestre) | .NET 10 / WinUI 3 / ASP.NET Core / EF Core / PostgreSQL / Blazor | a escrever |

## 7. Premissas abertas (RP-05)

| ID | Premissa | Origem | Confirmar com |
|----|----------|--------|---------------|
| PRE-01 | Dogfood: ~10 usuários simultâneos, 6–8 apps | P1 | Frederico (inventário real — T-001) |
| PRE-02 | Piloto 60–100 simultâneos; 12 meses ~250 usuários e ~30 apps; RNFs dimensionados para 500 | P1 | meta de projeto |
| PRE-03 | Escritório do dogfood hoje em workgroup, sem domínio; criar AD DS é tarefa do MVP-0 | P2 | Frederico |
| PRE-04 | Não há hoje Windows Server 2025 nem RDS CALs; serão adquiridos | P4 | Frederico / cotação |
| PRE-05 | Custo total ≤ R$ 50/usuário/mês contra venda de R$ 70–150 | P8 | T-003 |
| PRE-06 | Nuvens próprias de Domínio e Alterdata existem e avançam, estágio comercial não verificado | RM-04 | T-004 |

## 8. Riscos registrados

| ID | Risco | Severidade | Estado |
|----|-------|-----------|--------|
| R-001 | Licença de Domínio/Alterdata pode vedar execução multiusuário em servidor de terminal — invalida o Caminho B | **Crítica** | Aberto — mitigação em T-001 |
| R-002 | Custódia centralizada de A1 tem exposição jurídica antes da técnica | Alta | Aberto — mitigação em T-002 |
| R-003 | SPLA/RDS SAL como custo fixo pode inviabilizar PRE-05 | Alta | Aberto — mitigação em T-003 |
| R-004 | Fosso (DIF-01..03) só chega em V2/V3; MVP-0 e MVP-1 não se distinguem de um RDS bem configurado. Recomendação: antecipar DIF-02 mínimo para o piloto, via ADR | Alta | Aberto — decisão de Frederico |
| R-005 | DC + RD Session Host na mesma máquina obriga logon local de usuários finais no controlador de domínio. Proposta: separar em duas VMs no mesmo host (Windows Server Standard licencia 2 VMs) | Alta | Aberto — endereçar em ADR-0002 |
| R-006 | Execução solo de quatro componentes com MVP-0 previsto em ~2 meses | Alta | Aberto |
| RM-01..RM-10 | Riscos de mercado — ver `VISAO.md` §6 | vários | Aberto |

## 9. Violações de processo detectadas (RA-06)

Nenhuma.
