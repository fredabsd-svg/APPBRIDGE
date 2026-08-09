# ADR-0011 — Convenções do modelo de dados: chaves, tempo, exclusão e integridade de tenant
Data: 2026-08-08 · Status: **aceito** · Autor: Arquiteto de Software Principal (decisão delegada por Frederico em 2026-08-08)

## Contexto

`MODELO-DE-DADOS.md` vai definir dezenas de tabelas. Quatro escolhas se repetem em todas elas e, se
forem feitas tabela a tabela, produzem um esquema inconsistente que ninguém consegue auditar:

1. **Tipo de chave primária** — sequencial ou opaca;
2. **Representação de tempo** — com ou sem fuso;
3. **Exclusão** — física ou lógica;
4. **Como o `tenant_id` participa da integridade referencial** — porque ADR-0004 exige isolamento por
   mecanismo, e uma chave estrangeira comum **não impede** que uma linha do tenant A aponte para uma
   linha do tenant B.

O item 4 é o que motiva este ADR. O filtro global no `DbContext` (ADR-0004) protege a **leitura**. Ele
não protege contra uma **escrita** que grave um `application_id` de outro tenant num registro de
permissão — e essa linha, uma vez gravada, é lida normalmente, porque o filtro só confere o
`tenant_id` da própria linha.

## Decisão

### 1. Chave primária: UUID v7, gerado pela aplicação

Todas as tabelas usam `uuid` como chave primária, na variante **v7** (ordenada no tempo).

- **Não enumerável**: satisfaz ADR-0004 item 8 — um identificador exposto em API não permite
  descobrir recursos de outro tenant por incremento.
- **Ordenada no tempo**: ao contrário do UUID v4, preserva localidade de inserção no índice, evitando
  a fragmentação que torna UUID aleatório caro em tabelas grandes como `launch`.
- **Gerada pela aplicação**: o identificador existe antes do `INSERT`, o que simplifica correlação
  entre trilha e registro operacional na mesma transação (ADR-0007).

### 2. Tempo: `timestamptz`, sempre em UTC

Toda coluna temporal é `timestamptz` e é gravada em UTC (RNF-020). Conversão para o fuso do usuário é
responsabilidade exclusiva da apresentação. **Nenhuma coluna de data usa `timestamp` sem fuso** — é a
origem clássica de trilha de auditoria com hora errada, que é trilha sem valor.

### 3. Exclusão: lógica para dado de tenant, proibida para trilha

| Categoria | Regra |
|-----------|-------|
| Dado operacional de tenant (aplicativo, grupo, usuário, host) | **Exclusão lógica** via `deleted_at`/`deleted_by`. Nunca `DELETE`. Preserva a integridade histórica da trilha, que referencia essas linhas. |
| Registro de permissão | Nem exclusão física nem lógica: **vigência temporal** (`effective_from`/`effective_to`). Revogar é fechar a vigência, não apagar — RF-007 precisa saber que a permissão existiu. |
| Trilha de auditoria | **Nem `UPDATE` nem `DELETE`** pela aplicação (RNF-019). A única remoção é o expurgo por política de retenção (ADR-0007), executado por rotina própria e ele mesmo registrado. |

### 4. Integridade de tenant: chave estrangeira composta

Toda chave estrangeira entre tabelas de tenant **inclui o `tenant_id`**:

```sql
-- a chave candidata que torna isso possível
ALTER TABLE application ADD CONSTRAINT uq_application_tenant UNIQUE (tenant_id, id);

-- a FK que impede o cruzamento
ALTER TABLE application_permission
  ADD CONSTRAINT fk_permission_application
  FOREIGN KEY (tenant_id, application_id) REFERENCES application (tenant_id, id);
```

Com isso, **o banco recusa** uma permissão do tenant A apontando para um aplicativo do tenant B — mesmo
que um erro de código tente gravá-la. O isolamento deixa de depender apenas do filtro de leitura e
passa a ter uma segunda linha de defesa, no motor.

### 5. Campos de auditoria em toda tabela

| Categoria | Campos obrigatórios |
|-----------|--------------------|
| Tabelas mutáveis | `created_at`, `created_by`, `updated_at`, `updated_by`, `row_version` (concorrência otimista), `deleted_at`, `deleted_by` |
| Tabelas append-only (trilha) | `created_at` e `created_by` **apenas** |

**Desvio declarado:** a Seção 7 do prompt mestre pede campos de auditoria em toda tabela. Nas tabelas
de trilha, os campos `updated_*` são deliberadamente omitidos — uma linha de auditoria que registra
"quem me alterou" é uma linha que admite alteração, o que contradiz RNF-019. A ausência é o controle.

### 6. Nomenclatura

`snake_case` no banco, identificadores em inglês (RP-01). Tabelas no singular. Chaves estrangeiras
`<entidade>_id`. Índices `ix_<tabela>_<colunas>`, restrições `uq_`/`fk_`/`ck_`.

## Alternativas consideradas

| Item | Alternativa | Por que não |
|------|-------------|-------------|
| Chave | `bigint` sequencial | Enumerável quando exposto (contraria ADR-0004) e obriga a manter um segundo identificador público, dobrando o trabalho. |
| Chave | UUID v4 | Não enumerável, mas aleatório: fragmenta o índice e degrada tabelas de alto volume como `launch`. |
| Chave | Chave composta natural (`tenant_id` + código) | Propaga colunas por todas as FKs e engessa renomeação. |
| Tempo | `timestamp` local + coluna de fuso | Mais complexo e mais fácil de errar que gravar tudo em UTC. |
| Exclusão | `DELETE` físico com trilha registrando a exclusão | A trilha sobreviveria, mas referências históricas quebrariam e relatórios passariam a mentir sobre o passado. |
| Isolamento | Apenas o filtro global do `DbContext` | Protege leitura, não escrita. É exatamente a lacuna que a FK composta fecha. |
| Isolamento | Row-Level Security do PostgreSQL | Defesa forte e no lugar certo, mas exige gerenciar conexão por tenant ou variável de sessão a cada requisição, com risco de vazamento em pool de conexões. **Fica registrada como evolução desejável**, a reavaliar quando o piloto começar. |

## Consequências

**Positivas**
- Isolamento com duas linhas de defesa independentes: filtro na leitura, restrição no motor.
- Trilha imutável por construção, não por disciplina.
- Esquema previsível: quem abre qualquer tabela sabe o que vai encontrar.

**Negativas**
- UUID ocupa 16 bytes contra 8 de `bigint`, e a FK composta acrescenta o `tenant_id` a cada índice —
  custo de armazenamento real, embora modesto na escala de RNF-026.
- Exclusão lógica exige que **toda** consulta considere `deleted_at`, o que se resolve por filtro
  global no `DbContext`, junto com o filtro de tenant.
- A chave candidata `(tenant_id, id)` é redundante com a chave primária. É o preço da FK composta.

**Riscos**
- Índices maiores em `launch` e nas tabelas de trilha, que são as de maior volume. Mitigação: medir
  em T-005 e particionar por período se necessário — o particionamento também facilita o expurgo.
- Exclusão lógica acumula linhas mortas indefinidamente. Precisa de política própria, distinta da
  retenção de trilha, ainda não definida. **Registrado como pendência** para `MODELO-DE-DADOS.md`.

## Requisitos relacionados

RNF-019, RNF-020, RNF-036, RNF-052 · RF-007, RF-073 · ADR-0004, ADR-0007 · Origem: §7 prompt (campos
de auditoria em toda tabela), RA-07
