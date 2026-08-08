# Matriz de licenciamento — entregável de T-001
> Portão **G-01** · Risco **R-001** · Preenchida à medida que as respostas chegam.
> Formato exigido em P3: **aplicativo × versão × tipo de licença × multiusuário S/N**.

---

## 1. Legenda de resposta

| Símbolo | Significado |
|---------|-------------|
| ✅ **Sim** | Permitido, com evidência escrita e cláusula referenciada |
| ⚠️ **Condicionado** | Permitido mediante modalidade específica, custo adicional ou requisito técnico |
| ❌ **Não** | Vedado |
| 🕓 **Aguardando** | Consulta enviada, sem resposta |
| ❔ **Sem resposta** | Fornecedor se recusou a responder ou não respondeu após cobrança |

> **`❔` não é neutro.** Um fornecedor que não se compromete por escrito é um risco em aberto para o
> Caminho B, e deve ser tratado como tal na decisão — não como pendência administrativa.

## 2. Matriz

**C-1** = uso pelos próprios colaboradores do licenciado (MVP-0, dogfood).
**C-2** = hospedagem por prestador para usuários de outra pessoa jurídica (piloto, Caminho B).

| # | Aplicativo | Versão | Tipo de licença | **C-1** | **C-2** | Suporte mantido | Modalidade / custo | Evidência | Data |
|---|-----------|--------|-----------------|---------|---------|-----------------|--------------------|-----------|------|
| 1 | Domínio — Contábil | | | 🕓 | 🕓 | | | | |
| 2 | Domínio — Folha | | | 🕓 | 🕓 | | | | |
| 3 | Domínio — Escrita Fiscal | | | 🕓 | 🕓 | | | | |
| 4 | Alterdata — [módulo] | | | 🕓 | 🕓 | | | | |
| 5 | Microsoft Office / 365 Apps | | | | | n/a | **Exige licença por volume (LTSC) ou M365 Apps com ativação em computador compartilhado. OEM/varejo não atende** | revendedor | |
| 6 | Windows Server 2025 + RDS CAL | | por usuário | | | n/a | RDS CAL por usuário; no Caminho B, avaliar **SPLA** (T-003) | revendedor | |
| 7 | ERP de cliente — [nome] | | | 🕓 | 🕓 | | | | |
| 8 | [legado interno] | | | | | | | | |

**Coluna "Evidência":** referência ao arquivo do e-mail de resposta, guardado fora do webmail pessoal.

## 3. Controle das consultas

| Fornecedor | Canal | Enviada em | Cobrança em | Respondida em | Quem respondeu |
|-----------|-------|------------|-------------|---------------|----------------|
| Thomson Reuters / Domínio | | | | | |
| Alterdata | | | | | |
| [ERP de cliente] | | | | | |
| Revendedor Microsoft | | | | | |

> Cobrar em **10 dias úteis** sem resposta. Silêncio não é permissão.

## 4. Leitura do resultado — o que cada combinação significa

| C-1 | C-2 | Consequência para o projeto |
|-----|-----|------------------------------|
| ✅ | ✅ | Cenário ideal. Caminho A e Caminho B viáveis para esse aplicativo |
| ✅ | ⚠️ | Caminho B viável **com custo a apurar**. Alimenta T-003 e pode pressionar PRE-05 (≤ R$ 50/usuário/mês) |
| ✅ | ❌ | **Caminho B inviável para esse aplicativo.** Se atingir Domínio ou Alterdata, atinge o núcleo da proposta ao público-alvo PA-01 |
| ❌ | ❌ | **Interrompe também o MVP-0** para esse aplicativo. O dogfood precisaria ser redesenhado |
| ❔ | ❔ | Risco em aberto. Não confundir com autorização |

## 5. Decisão a tomar quando a matriz estiver preenchida

Consolidada a matriz, `STATUS.md` deve registrar uma destas conclusões:

1. **G-01 aprovado** — Caminho B segue como planejado.
2. **G-01 aprovado com ressalva** — segue com custo ou restrição adicional; reavaliar PRE-05 e a
   precificação do piloto.
3. **G-01 reprovado para aplicativo específico** — o aplicativo sai do escopo hospedado, e é preciso
   avaliar se o restante ainda sustenta a proposta de valor.
4. **G-01 reprovado no núcleo** (Domínio e/ou Alterdata em C-2) — **o Caminho B é encerrado**. O
   Caminho A permanece viável, e `VISAO.md` §2.5 precisa de reavaliação por ADR.

> Qualquer uma das quatro é um resultado legítimo desta tarefa. A única saída ruim é começar a
> construir sem saber qual delas é a verdadeira.
