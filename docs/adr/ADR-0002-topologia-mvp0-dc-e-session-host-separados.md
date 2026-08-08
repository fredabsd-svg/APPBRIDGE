# ADR-0002 — Topologia do MVP-0: controlador de domínio e session host em VMs separadas
Data: 2026-08-08 · Status: **aceito** · Autor: Arquiteto de Software Principal (decisão delegada por Frederico em 2026-08-08)

## Contexto

A resposta P4 propôs um host Windows Server 2025 com 8 vCPU e 32–64 GB **acumulando session host e
controlador de domínio** no MVP-0, para ~10 usuários simultâneos e 6–8 aplicativos (PRE-01).

O acúmulo tem um efeito de segurança que não é evidente à primeira vista: para que usuários finais
abram sessão RemoteApp numa máquina, eles precisam do direito **"Permitir logon local"** nela. Se essa
máquina é o controlador de domínio, esse direito é concedido no DC — onde, por padrão, apenas
administradores o têm. A consequência é direta: qualquer escape de aplicativo, qualquer exploração de
um sistema contábil legado rodando ali, deixa de ser "comprometimento de uma máquina" e passa a ser
**comprometimento do domínio inteiro**, incluindo a base de contas e o material do futuro cofre de
certificados (DIF-01).

Registrado como risco **R-005** em `VISAO.md`.

## Decisão

**Controlador de domínio e RD Session Host não coabitam a mesma instância de sistema operacional, em
nenhuma fase, inclusive no MVP-0.**

Topologia do MVP-0:

| VM | Papel | Dimensionamento inicial |
|----|-------|------------------------|
| **AB-DC01** | AD DS (controlador de domínio), DNS | 2 vCPU · 4 GB · disco pequeno |
| **AB-RDS01** | RD Session Host + Connection Broker + RD Licensing + aplicativos publicados | 6 vCPU · restante da RAM |
| **AB-CP01** | Control Plane + PostgreSQL | Pode coabitar AB-RDS01 no MVP-0; separar antes do piloto |

Ambas as VMs rodam sobre **Hyper-V no mesmo host físico**, que continua sendo uma única máquina
comprada — o custo adicional é de configuração, não de hardware. Uma licença **Windows Server 2025
Standard** cobre **duas VMs** desde que todos os núcleos físicos estejam licenciados e o host seja
usado apenas para virtualização, o que torna o custo de licença adicional igual a zero.

O Control Plane pode dividir VM com o session host no MVP-0 (é dogfood interno), mas **precisa sair
de lá antes do piloto**, porque a partir daí ele passa a guardar dado de outros tenants.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| **Tudo numa VM só** (proposta original de P4) | Obriga logon local de usuário final no DC. Ganha-se a economia de uma VM e perde-se o isolamento do domínio inteiro. Desproporcional. |
| **Três hosts físicos separados** | Custo e complexidade injustificados para 10 usuários no MVP-0. |
| **Manter tudo junto no MVP-0 "porque é só dogfood", separar no piloto** | O dogfood é o ambiente onde os certificados A1 reais do escritório vão circular e onde as bases contábeis reais ficam. Não é laboratório descartável. Além disso, "arrumamos depois" costuma virar dívida permanente. |
| **Container em vez de VM para o Control Plane** | Não resolve o problema (o conflito é DC × session host) e adiciona uma tecnologia nova ao MVP-0 sem necessidade. |

## Consequências

**Positivas**
- Escape de aplicativo no session host não é comprometimento de domínio.
- O DC fica pequeno, estável e raramente tocado — que é exatamente como um DC deve ser.
- A separação já espelha a topologia do piloto, então o roteiro de implantação é escrito uma vez só.

**Negativas**
- Uma camada de virtualização a mais para operar (Hyper-V), com backup e atualização próprios.
- ~4 GB de RAM e 2 vCPU consumidos pelo DC, indisponíveis para sessões de usuário.
- Um sistema operacional a mais para manter atualizado.

**Riscos**
- Com **um único host físico**, ele é ponto único de falha para tudo. Aceito no MVP-0 (dogfood, R-006),
  **inaceitável no piloto** — a topologia do piloto exigirá ADR próprio contemplando segundo host e
  segundo DC.
- 8 vCPU para ~10 usuários simultâneos em Domínio, Alterdata e Excel é apertado se os aplicativos
  forem pesados. O dimensionamento precisa ser medido no dogfood (CS-01) e não deve ser tratado como
  confirmado.
- `PREMISSA:` (PRE-18) o host físico suporta Hyper-V com virtualização assistida por hardware. A
  confirmar na aquisição.

## Requisitos relacionados

RNF-007 (usuários finais sem logon local em DC) · RNF-005, RNF-006, RNF-011, RNF-026, RNF-033, RNF-037 ·
Origem: P4, R-005, RP-06
