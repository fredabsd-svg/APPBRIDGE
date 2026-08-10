> ⚠️ **ERRATA — leia antes de usar como referência.**
> Esta análise contribuiu com **dois defeitos reais** confirmados na documentação aprovada (coluna
> `purpose` ausente no modelo de dados e falta de operação de cancelamento em `ISessionBackend`), hoje
> incorporados ao `ROADMAP.md` como T-207 e T-506.
>
> Contém, porém, **14 referências de requisito incorretas** — entre elas, atribuir ao ADR-0009 um
> reconhecimento que está em `SEGURANCA.md` (AM-33), tratar AM-02 como abuso de cofre quando é
> comprometimento da chave de assinatura, e inverter RNF-034 com RNF-040. A lista completa está em
> `operacao/revisao-issues-e-backlog-paralelo.md` §6.
>
> O corpo **não foi reescrito**: é registro do que foi analisado nesta data (ADR-0016 §3).

# 🔍 ANÁLISE TÉCNICA — AppBridge
## Bugs Potenciais, Riscos Não Mitigados e Oportunidades de Melhoria

**Data:** 2026-08-09  
**Escopo:** Design do MVP-0a, Arquitetura, Segurança e Roadmap  
**Fase:** Implementação iniciando (design concluído em 2026-08-08)

---

## 📋 RESUMO EXECUTIVO

O AppBridge tem **design sólido e rigoroso**, mas carrega **5 riscos críticos ainda não mitigados** que devem ser endereçados **antes ou durante MVP-0a**, não depois. Adicionalmente, há **3 problemas em ADRs** que criam suposições frágeis e **2 gaps arquiteturais** que podem virar bugs em runtime.

**Recomendação:** Priorize as 3 tarefas T-005 (medições no dogfood), PS-05 e PS-07 antes de começar a escrever código do Control Plane.

---

## 🚨 RISCO 1: COFRE VULNERÁVEL A ABUSO OPERACIONAL (R-020 · Crítica)

### O Problema
- **ADR-0009** reconhece: "nada impede tecnicamente o operador de assinar com o certificado A1 de um cliente"
- DIF-01 (cofre de certificados) é vendido como **diferencial de segurança**, mas a proteção é contratual, não técnica
- Cliente no piloto perguntará: "O que impede vocês de assinar um contrato em nome da minha empresa?"
- Resposta hoje: **"Contrato"** — não é satisfatória

### Por Que é Crítico
- Toca o diferencial de produto (DIF-01)
- Bate direto na jurídica (dupla obrigação: técnica + contratual)
- Piloto com 3–5 clientes × 50 empresas cada = 150–250 certificados sob custódia — risco massivo

### Pendências Ligadas
- **PS-07:** Reduzir poder unilateral do provedor (prazo: antes do cofre em produção, V2)
- **B-006:** Decisão de Frederico sobre segunda aprovação vs. senha sob custódia vs. módulo de hardware

### Ação Imediata (MVP-0a)
- [ ] Não implementar cifragem de chave privada A1 sem arquitetura de protocolos de dupla aprovação ou escrow
- [ ] Registrar em `SEGURANCA.md` que AM-02 (abuso operacional) fica como 🔴 Pendente até segunda aprovação existir
- [ ] Documentar no `STATUS.md` que MVP-0 não vai ter cofre funcional no dogfood — apenas stub que recusa qualquer chave A1

---

## 🚨 RISCO 2: REVOGAÇÃO NÃO ENCERRA SESSÃO (R-014 · Alta)

### O Problema
- **RF-008** (terminar sessão remota) é MVP-1, não MVP-0
- No MVP-0, revogar acesso a um usuário **não encerra a sessão aberta**
- **Caso real:** demissão de funcionário às 15h → admin revoga acesso → usuário continua trabalhando até 18h

### Por Que é Crítico
- Será descoberto no **pior momento** (demissão real com sensibilidade)
- Violaria RNF-030 (propagação de revogação em até 60 s)
- Afeta todos os 4 RNF de segurança do MVP-0 (RNF-014, RNF-020, RNF-023, RNF-036)

### Pendências Ligadas
- Nenhuma — o risco está declarado, falta **documentar a resposta operacional**

### Ação Imediata (MVP-0a)
- [ ] Criar **roteiro operacional obrigatório** em `docs/operacao/T-106-revogacao-manual.md`:
  1. Admin revoga no AppBridge
  2. AppBridge nega novo lançamento em 60 s
  3. Admin **encerra sessão manualmente** via RDS Console no host
  4. Verificar saída de usuário no `eventvwr` (evento 4779 de RDS)
- [ ] Adicionar ao backlog do MVP-1 (E-13): "RF-008 — Endpoint para encerrar sessão remota"
- [ ] Testar o procedimento no dogfood com uma revogação real

---

## 🚨 RISCO 3: TRILHA VERIFICÁVEL APENAS PELO PROVEDOR (R-021 · Alta)

### O Problema
- Sem encadeamento criptográfico ou carimbo de tempo independente, "a trilha é a palavra do provedor"
- **PS-03** é pré-requisito do piloto, não tarefa mínima do MVP-0
- Cliente num litígio: "Como sei que vocês não reconstruíram a trilha?"
- Resposta hoje: "Nossas políticas internas" — não resiste a auditoria forense

### Por Que é Crítico
- Afeta **RNF-022** (trilha de auditoria verificável) no MVP-0
- Enquanto estiver aberto, o diferencial **DIFI-02** (segurança de certificado) fica comprometido
- Encadeamento criptográfico exige **código novo** (não é operação)

### Pendências Ligadas
- **PS-03:** Encadeamento criptográfico da trilha (prazo: antes do piloto)
- **B-007:** Decisão de Frederico sobre que tipo de encadeamento

### Ação Imediata (MVP-0a)
- [ ] Implementar **Merkle tree incremental** ou **HCPA** para as tabelas append-only na V2 (RNF-022)
- [ ] **No MVP-0**, registrar em `SEGURANCA.md` que AM-12 fica 🔴 Pendente e será revisado em ADR novo
- [ ] Documentar no contrato do piloto (T-002) que a trilha do Caminho B não é independentemente verificável até V2
- [ ] Adicionar tarefa ao E-13 (piloto): "PS-03 — Implementar Merkle tree da trilha"

---

## 🚨 RISCO 4: CONTAGEM DE LICENÇA INFLÁVEL (R-009 · Alta)

### O Problema
- **PRE-23** (Connection Broker confiável em detectar fim de sessão) **nunca foi medida**
- Se o Connection Broker relatar fim com latência > 60 s, o teto de licenças é atingido indevidamente
- Mitigação existe no papel: `SessionReconciler` + expiração por inatividade, mas **não está na suíte de testes** do MVP-0a

### Por Que é Crítico
- RF-062 (metering mínimo) antecipado de V2 para MVP-1 — virou dado crítico, não nice-to-have
- Se contagem infla, o AppBridge **nega trabalho legítimo** (AM-24 · DoS)
- Afeta RNF-010 (limitação de taxa) e RNF-026 (dimensionamento para 100 apps)

### Pendências Ligadas
- **T-005:** Medições obrigatórias no dogfood → **Medir confiabilidade do Connection Broker**
- **R-017:** `SessionReconciler` é a única defesa — exige testes de regrediência

### Ação Imediata (MVP-0a)
- [ ] Adicionar ao plano de testes do MVP-0a:
  - Teste 1: Abrir app, reconectar 5 vezes, fechar → verificar que contagem não infla
  - Teste 2: Forçar queda de conexão da estação → verificar que `SessionReconciler` limpa em < 60 s
  - Teste 3: Modo de alta inatividade (12h de logoff) → verificar que sessão expirada libera licença
- [ ] Registrar resultado das medições em `docs/auditoria/AAAA-MM-DD-SNNN.md`
- [ ] Se confiabilidade < 99%, adicionar circuito interruptor que libera licença forçadamente após 90 s

---

## 🚨 RISCO 5: GARGALO DE ASSINATURA (R-013 · Média-Alta)

### O Problema
- **PRE-12** (p95 ≤ 1 s de assinatura de `.rdp`) é **suposição**, não medição
- `rdpsign.exe` é processo externo — invocá-lo por lançamento pode virar gargalo
- Se assinatura levar 2–3 s × 100 apps simultâneos = 200–300 s de latência acumulada
- Viola RNF-027 (abertura ≤ 5 s com prelaunch, ≤ 20 s sem)

### Por Que é Crítico
- Toca na UX promitida (abertura rápida = diferencial vs. RDS vanilla)
- Sem medição, o MVP-0a pode entregar uma janela de 10–15 s, não 5 s
- Feedback negativo do dogfood → replanejar antes do piloto

### Pendências Ligadas
- **T-005:** Medições obrigatórias → **Incluir teste de carga em assinatura**
- **R-013:** Caminho de saída em ADR-0009 (pool de processos `rdpsign` ou biblioteca nativa)

### Ação Imediata (MVP-0a)
- [ ] Implementar com **pool de worker threads** que chamam `rdpsign.exe` async
- [ ] Medir p50/p95 de latência com 10, 50 e 100 apps simultâneos no dogfood
- [ ] Se p95 > 1.5 s, abrir ADR novo: trocar por `Windows.Win32.Security.Cryptography`
- [ ] Documentar resultado em `ARQUITETURA.md` §5.2

---

## 🔧 PROBLEMA 1 EM ADR: AUDITORIA BLOQUEANTE (ADR-0007)

### O Problema
- ADR assume: "custo quase nulo de bloquear cada lançamento na transação"
- **Mas R-016 registra:** "bloqueia começar a trabalhar — pico de início é às 8h"
- Se disco ficar cheio às 8h da manhã, **ninguém entra até expurgo rodar**
- Trade-off entre segurança (auditoria inviolável) e disponibilidade (RNF-032) **não foi explicitado**

### Por Que é Problemático
- Violação de RNF-032 (sessões abertas continuam) se o disco encher
- Choque com RNF-040 (availability 99,5%) durante horário comercial
- Mitigação (alerta de espaço + expurgo automático) é assumida, não garantida

### Ação Imediata (MVP-0a)
- [ ] Implementar **monitoramento proativo** de espaço em disco:
  - Alerta em 85%, bloqueio em 95%
  - Expurgo incremental automático (não aguarda agendamento)
  - Fallback: negar novos lançamentos com HTTP `507 Insufficient Storage` detalhado
- [ ] Testar simulando disco cheio no dogfood
- [ ] Adicionar cláusula no contrato do piloto: "Disponibilidade durante horário comercial exige monitoramento de disco contínuo"

---

## 🔧 PROBLEMA 2 EM ADR: RDPSIGN COMO PROCESSO (ADR-0009)

### O Problema
- ADR diz: "será medido cedo"
- Nenhuma tarefa do MVP-0a/0b mede performance bajo carga realista
- Assume que invocar processo externo por lançamento é viável, sem ter provado

### Por Que é Problemático
- Se for lento, está colado na arquitetura (muda ADR, não é refactoring)
- Feedback do dogfood chega em out/2026; replanejar para nov/2026 é apertado para piloto em abr/2027

### Ação Imediata (MVP-0a)
- [ ] **Medir T-005:** rodar `rdpsign.exe` 100 vezes em sequência, registrar p50/p95/p99
- [ ] Se p99 > 100 ms, preocupar-se com pool de workers
- [ ] Incluir teste de carga em "assinatura sob 100 concorrentes" na suite do MVP-0

---

## 🕳️ GAP ARQUITETURAL 1: PRELAUNCH NÃO RASTREÁVEL EM MODELO

### O Problema
- **ARQUITETURA.md** §5.3 descreve SessionPrimer (prelaunch) como "invisível"
- **Mas API.md** §4 usa `"purpose": "prelaunch"` para distinguir trilha
- **Modelo de dados** não tem campo para registrar isso: fica apenas em log estruturado JSON
- **Consequência:** RF-062 (contagem de uso) pode contar prelaunchs como lançamentos reais

### Por Que é Bug em Potencial
- Relatório de licenças fica **sujo**
- Cliente vê "100 usos de Domínio" quando foi na verdade 20 usos + 80 prelaunchs
- Bilhetagem errada no Caminho B

### Ação Imediata (MVP-0a)
- [ ] Adicionar coluna `launch_purpose` (enum `real | prelaunch`) em `session_launch`
- [ ] Filtrar prelaunchs em consultas de RF-062 (deixar como "preparação", não uso)
- [ ] Validar no teste que o endpoint `GET /v1/applications/{id}/usage` **não conta prelaunchs**

---

## 🕳️ GAP ARQUITETURAL 2: FALTA OPERAÇÃO DE CANCELAMENTO DE PRELAUNCH

### O Problema
- **ARQUITETURA.md** §4.2 define interface `ISessionBackend` com operações CRUD
- Não há `CancelPrelaunchAsync()` ou similar
- Se prelaunch falhar **após criar sessão no RDS**, fica "aberta e invisível" contando contra licença

### Por Que é Bug em Potencial
- Prelaunch falha por qualquer razão (timeout, app indisponível, crash do launcher)
- Sessão vira "zumbi" contando no teto até expiração por inatividade (RNF-033)
- Violaria RNF-010 (limitação de taxa) e AM-24 (contagem inflada)

### Ação Imediata (MVP-0a)
- [ ] Adicionar `CancelSessionAsync(sessionId, reason)` com logging em trilha
- [ ] Chamar ao detectar falha de prelaunch em `StartSessionAsync`
- [ ] Testar: prelaunch falha → verifica que `SessionReconciler` limpa em < 60 s

---

## ✨ OPORTUNIDADES RÁPIDAS DE MELHORIA

### 1️⃣ Heartbeat Proativo do Launcher (RF-015 · 1 ponto)

**O que:** Adicionar `GET /v1/me` à sincronização de catálogo  
**Por quê:** Detectar token expirado antes do lançamento, evitar surpresa  
**Custo:** ~1 dia (ou 1 ponto)  
**Ganho:** Melhora RNF-042 ("parece local") com mínimo esforço  
**Quando:** MVP-0a, antes de fechar o endpoint de sincronização

---

### 2️⃣ Reconciliação Manual de Licenças no Painel (RF-062 · 2 pontos)

**O que:** Admin > Aplicativos > "Resetar contador" com confirmação  
**Por quê:** Lidar com bugs de contagem (R-009) sem parar o sistema  
**Custo:** Endpoint + UI + auditoria  
**Ganho:** Operabilidade no dogfood, salva o MVP-1 em caso de problema  
**Quando:** MVP-0b (já está em `API.md` §6)

---

### 3️⃣ Teste Automatizado de Violação Multi-Tenant (AM-07 · 3 pontos)

**O que:** Suite de testes que **tenta deliberadamente** ler/gravar tenant alheio  
**Por quê:** R-019 é altíssimo risco; falha silenciosa é pior que falha ruidosa  
**Custo:** ~2–3 dias escrevendo cenários ofensivos  
**Ganho:** Detecta regressão cedo se alguém mexer em autorização  
**Quando:** MVP-0a, junto com testes de unidade do Control Plane

---

### 4️⃣ Varredura de Segredos Automática (PS-04 · 2 pontos)

**O que:** GitHub Action que roda `detect-secrets` em cada PR  
**Por quê:** Previne vazamento de certificado ou credencial por acidente  
**Custo:** Configuração do Action + arquivo `.secrets.baseline`  
**Ganho:** Rede de segurança gratuita, paga dividendos em segurança  
**Quando:** MVP-0a, antes de primeiro commit do Control Plane

---

### 5️⃣ Health Check com SLA Definido (RNF-034 · 1 ponto)

**O que:** Endpoint `GET /v1/health` que reporta dependências críticas (DB, AD, cert store)  
**Por quê:** Piloto exige 99,5% de disponibilidade; sem health check, é cego  
**Custo:** ~4–6 horas  
**Ganho:** Base para monitoramento, alertas e SLA reporting  
**Quando:** MVP-0a, sprint 1

---

## 📊 TABELA DE PRIORIZAÇÃO

| ID | Categoria | Título | Severidade | Prazo | Deps | Esforço |
|---|-----------|--------|-----------|-------|------|---------|
| R-020 | Risco | Cofre vulnerável a abuso | 🔴 Crítica | MVP-0a | B-006 | Decisão |
| R-014 | Risco | Revogação sem término | 🟠 Alta | MVP-0a | Roteiro operacional | 4 h |
| R-021 | Risco | Trilha não verificável | 🟠 Alta | MVP-0a | PS-03, ADR novo | Arquitetura |
| R-009 | Risco | Contagem de licença inflável | 🟠 Alta | MVP-0a | T-005 | Teste |
| R-013 | Risco | Gargalo de assinatura | 🟠 Média-Alta | MVP-0a | T-005 | Medição |
| ADR-0007 | Problema | Auditoria bloqueante + RNF-032 | 🟡 Média | MVP-0a | Nenhuma | Implementação |
| Gap 1 | Arquitetura | Prelaunch sem rastreamento | 🟡 Média | MVP-0a | Nenhuma | 4 h |
| Gap 2 | Arquitetura | Falta cancelamento de prelaunch | 🟡 Média | MVP-0a | Nenhuma | 8 h |
| Oportunidade 1 | Melhoria | Heartbeat proativo | 🟢 Baixa | MVP-0a | Nenhuma | 1 dia |
| Oportunidade 2 | Melhoria | Reconciliação manual de licenças | 🟢 Baixa | MVP-0b | Nenhuma | 2 dias |
| Oportunidade 3 | Melhoria | Teste de violação multi-tenant | 🟢 Baixa | MVP-0a | Nenhuma | 3 dias |
| Oportunidade 4 | Melhoria | Varredura de segredos automática | 🟢 Baixa | MVP-0a | Nenhuma | 2 dias |
| Oportunidade 5 | Melhoria | Health check com SLA | 🟢 Baixa | MVP-0a | Nenhuma | 1 dia |

---

## 🎯 RECOMENDAÇÕES IMEDIATAS

### Sprint de Arquitetura (Antes do primeiro commit)
1. [ ] Reunião com Frederico sobre R-020, B-006, B-007 (30 min)
2. [ ] Criar ADR-0015 sobre encadeamento criptográfico da trilha
3. [ ] Atualizar `SEGURANCA.md` com estado real de AM-02, AM-12, AM-20

### Sprint 1 do MVP-0a (Primeiras 2 semanas)
1. [ ] Implementar logs estruturados com `purpose: prelaunch | real` (Gap 1)
2. [ ] Adicionar `CancelSessionAsync()` em `ISessionBackend` (Gap 2)
3. [ ] Implementar health check com dependências (Oportunidade 5)
4. [ ] Configurar varredura de segredos no GitHub (Oportunidade 4)

### Sprint T-005 (Dogfood, out/2026)
1. [ ] Medir confiabilidade do Connection Broker (R-009)
2. [ ] Medir latência de `rdpsign` sob carga (R-013)
3. [ ] Testar revogação manual (R-014)
4. [ ] Teste de violação de multi-tenant (Oportunidade 3)

---

## 📝 CONCLUSÃO

O AppBridge tem **design robusto**, mas há **5 riscos que não podem ser ignorados**. Adicionalmente, **2 gaps arquiteturais simples** virão bugs no runtime se não forem endereçados agora.

**O caminho crítico não é código — é medição e decisão:**

1. **Confiabilidade do Connection Broker** (R-009) — se falhar, RF-062 é inviável
2. **Latência de assinatura** (R-013) — se for lenta, RNF-027 não passa
3. **Decisão sobre cofre** (R-020, B-006) — sem dupla aprovação, DIF-01 não é vendável

Recomendo que **T-005 saia do backlog para tarefa semanal do MVP-0a**, não apareça só no dogfood (out/2026). Feedback precoce economiza semanas de replanejos.

