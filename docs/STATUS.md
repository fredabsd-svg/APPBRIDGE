# STATUS — AppBridge
> Estado vivo do projeto. Atualizado ao fim de toda sessão (RA-02).

**Última atualização:** 2026-08-08 · **Sessão atual:** S001 · **Fase:** Design e Arquitetura (MVP-0)

---

## 1. Onde estamos

Repositório recém-inicializado. Nenhum entregável da fase de design (Seção 7 do prompt mestre)
foi produzido ainda. Sessão S001 aberta para: confirmar entendimento do produto, levantar as
perguntas de descoberta que destravam `VISAO.md` e `REQUISITOS.md`, e propor o esqueleto do
repositório.

**Nada de código de produção existe ou deve existir nesta fase.**

## 2. Entregáveis da fase de design

| # | Entregável | Estado |
|---|-----------|--------|
| 1 | `docs/VISAO.md` | ⬜ não iniciado — bloqueado por P1–P4 |
| 2 | `docs/REQUISITOS.md` | ⬜ não iniciado — bloqueado por P1–P8 |
| 3 | `docs/ARQUITETURA.md` | ⬜ não iniciado |
| 4 | `docs/MODELO-DE-DADOS.md` | ⬜ não iniciado |
| 5 | `docs/API.md` | ⬜ não iniciado |
| 6 | `docs/SEGURANCA.md` | ⬜ não iniciado |
| 7 | `docs/ROADMAP.md` + backlog MVP-0 | ⬜ não iniciado |

Legenda: ⬜ não iniciado · 🟡 em produção · 🔵 submetido, aguardando aprovação · ✅ aprovado

## 3. Próximos passos

1. Frederico responde às perguntas P1–P8 (Seção 5 deste documento).
2. Produzir `VISAO.md` (entregável 1) — submeter, aguardar aprovação (RP-04).
3. Somente após aprovação: `REQUISITOS.md`.

## 4. Bloqueios ativos

| ID | Bloqueio | Impede | Responsável |
|----|----------|--------|-------------|
| B-001 | Perguntas de descoberta P1–P8 sem resposta | Entregáveis 1 e 2 | Frederico |

## 5. Perguntas de descoberta abertas (S001)

| ID | Pergunta | Destrava |
|----|----------|----------|
| P1 | Escala-alvo do MVP-0 e do piloto: quantos usuários simultâneos, quantos escritórios, quantos apps publicados? | VISAO, REQUISITOS (RNF de capacidade) |
| P2 | Identidade no MVP-0: AD on-premises, Entra ID, ou ambos? Há domínio/floresta já existente no escritório para o dogfood? | VISAO, REQUISITOS, ARQUITETURA |
| P3 | Inventário concreto dos apps do dogfood (Domínio, Alterdata, Office, legados): quais exatamente, e há licença que autorize execução multiusuário em servidor de sessão? | VISAO (não-objetivos), REQUISITOS |
| P4 | Infraestrutura disponível hoje: servidor físico/VM, versão do Windows Server, RDS CALs adquiridas, link de internet e IP fixo? | VISAO, ARQUITETURA |
| P5 | Acesso externo já no MVP-0 (RD Gateway + MFA) ou MVP-0 restrito à rede interna/VPN? | REQUISITOS, SEGURANCA |
| P6 | Cofre de certificados A1: existe autorização formal do titular do certificado para custódia centralizada? Qual o modelo jurídico pretendido (procuração, contrato de custódia)? | VISAO (diferencial 1), SEGURANCA |
| P7 | Multi-tenant: o piloto do Caminho B terá tenants isolados por servidor, por OU/domínio, ou apenas lógicos no banco? | MODELO-DE-DADOS, ARQUITETURA |
| P8 | Restrições de prazo e orçamento da fase de design e do piloto (datas-alvo, custo mensal aceitável por usuário)? | ROADMAP |

## 6. Premissas abertas (RP-05)

Nenhuma. Nenhuma suposição foi assumida — as lacunas viraram perguntas P1–P8.

## 7. Riscos registrados

| ID | Risco | Impacto | Estado |
|----|-------|---------|--------|
| R-001 | Licenciamento dos sistemas contábeis (Domínio, Alterdata) pode vedar ou encarecer execução em servidor de sessão multiusuário | Alto — pode invalidar o Caminho B | Aberto (depende de P3) |
| R-002 | Custódia centralizada de certificado A1 tem exposição jurídica (ICP-Brasil, responsabilidade por uso indevido) além da técnica | Alto | Aberto (depende de P6) |
| R-003 | SPLA/RDS SAL no Caminho B impõe custo fixo por usuário/mês que pode inviabilizar o preço-alvo | Médio-alto | Aberto (depende de P8) |
| R-004 | RDS é produto maduro e commoditizado; o valor do AppBridge está no fosso (cofre, metering, orquestrador), não no RemoteApp em si | Médio | Aberto |

## 8. Violações de processo detectadas (RA-06)

Nenhuma.
