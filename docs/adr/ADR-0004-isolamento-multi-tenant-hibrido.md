# ADR-0004 — Isolamento multi-tenant híbrido: físico no RDS, lógico no Control Plane
Data: 2026-08-08 · Status: **aceito** · Autor: Arquiteto de Software Principal (decisão delegada por Frederico em 2026-08-08)

## Contexto

A restrição de arquitetura §2.5 do prompt mestre exige multi-tenant desde o modelo de dados, para que
o Caminho A (software no cliente) e o Caminho B (serviço hospedado) permaneçam viáveis. A resposta P7
definiu a direção: isolamento por session host, OU e GPO na camada RDS, e `tenant_id` em toda tabela
no Control Plane.

O ponto que exige decisão explícita não é *se* haverá isolamento, mas **onde ele é aplicado**. Filtro
por `tenant_id` escrito à mão em cada consulta é a forma mais comum de vazamento entre tenants em
sistemas SaaS: basta um `Where` esquecido em uma consulta de relatório para expor dados de outro
escritório contábil — que, neste domínio, significa expor a contabilidade de terceiros.

## Decisão

**Isolamento híbrido, com o filtro de tenant aplicado por padrão na camada de acesso a dados — nunca
consulta a consulta.**

### Camada RDS (isolamento físico e de sistema operacional)

1. Cada escritório-cliente tem **session host(s) dedicados**. Usuários de tenants diferentes não
   compartilham instância de sistema operacional.
2. Cada escritório tem **OU própria** no AD DS do provedor, com **GPOs próprias**.
3. **Domínio único do provedor com OU por cliente** atende o piloto. Floresta separada só se um
   cliente exigir contratualmente — e nesse caso, ADR próprio.
4. O roteamento de lançamento respeita o tenant (RF-074): não existe caminho de código que aponte um
   usuário para host de outro tenant.

### Camada Control Plane (isolamento lógico)

5. **`tenant_id` obrigatório em toda tabela de dados de tenant**, desde a primeira migração do MVP-0
   (RNF-036). Tabelas de catálogo global do produto, se existirem, são explicitamente marcadas como
   tal e não contêm dado de cliente.
6. **O filtro é aplicado no `DbContext`, por filtro global de consulta**, alimentado por um contexto
   de tenant resolvido a partir do token — não por parâmetro passado pelo chamador. Consulta que
   esquecer o filtro **continua isolada**, porque o filtro não depende de quem escreveu a consulta.
7. Toda travessia deliberada de tenant (papel de operador do provedor, RF-075) é **explícita no
   código**, nominal, e registrada na trilha administrativa (RNF-017).
8. Chaves primárias não são sequenciais previsíveis expostas em API; o identificador público de
   recurso não permite enumerar recursos de outro tenant.
9. **Teste de isolamento é obrigatório**: a suíte do Control Plane contém casos que tentam ler e
   escrever dado de outro tenant e exigem falha. Isolamento sem teste é intenção, não controle.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| **Banco de dados por tenant** | Isolamento mais forte, mas multiplica migrações, backup e operação por cliente, e complica relatórios do provedor. Desproporcional para 3–5 escritórios no piloto. Pode ser reavaliado se um cliente exigir — via novo ADR. |
| **Schema por tenant no PostgreSQL** | Meio-termo interessante, mas o EF Core lida mal com multiplicidade de schema em tempo de execução, e o ganho sobre o filtro global bem-feito é pequeno diante do custo. |
| **Apenas isolamento lógico, session host compartilhado entre tenants** | Mais barato, mas coloca processos de escritórios contábeis concorrentes no mesmo sistema operacional, com FSLogix como única fronteira. Inaceitável no domínio contábil, e mataria o argumento comercial do Caminho B. |
| **Filtro por `tenant_id` em cada consulta** | É a alternativa que a maioria adota e é a origem da maioria dos vazamentos. Recusada por depender de disciplina humana em vez de mecanismo. |
| **Floresta AD separada por cliente** | Isolamento máximo, custo operacional máximo. Reservado para exigência contratual específica. |

## Consequências

**Positivas**
- Vazamento entre tenants exige burlar o mecanismo, não apenas esquecer uma cláusula.
- Session host dedicado simplifica três coisas de uma vez: desempenho previsível por cliente,
  licenciamento por cliente e a drenagem de sessões do orquestrador (DIF-03).
- Serve aos dois caminhos de negócio sem bifurcar o código.

**Negativas**
- Session host dedicado tem **custo mínimo por cliente**, mesmo que o cliente tenha 5 usuários. Isso
  pressiona a meta de custo PRE-05 (≤ R$ 50/usuário/mês) justamente nos clientes menores — que são
  o perfil típico de PA-01.
- Filtro global de consulta tem armadilhas conhecidas (agregações, `IgnoreQueryFilters`, migrações,
  consultas SQL cruas) que precisam de revisão de código específica.

**Riscos**
- **Economia de escala invertida:** um escritório de 5 usuários pode custar mais por usuário do que
  um de 20. A precificação do piloto precisa refletir isso, ou haverá cliente deficitário. Ligado a
  R-003 e T-003.
- Um único domínio do provedor concentra risco: comprometimento do DC afeta todos os tenants.
  Mitigação parcial em ADR-0002 (DC isolado do session host) e em RNF-005.

## Requisitos relacionados

RF-073, RF-074, RF-075, RF-076 · RNF-036, RNF-038, RNF-005, RNF-017, RNF-048 · Origem: P7, §2.5 prompt, CS-05
