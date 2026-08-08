# ADR-0007 — Auditoria bloqueante no caminho crítico de lançamento e política de retenção
Data: 2026-08-08 · Status: **aceito** · Autor: Arquiteto de Software Principal (decisão delegada por Frederico em 2026-08-08)

## Contexto

`REQUISITOS.md` §7 deixou duas questões abertas, ambas com efeito sobre requisitos já escritos:

1. **RNF-022 / PRE-09** — se o Control Plane não conseguir gravar o registro de auditoria de um
   lançamento, ele impede o lançamento ou deixa passar com alerta?
2. **RNF-018 / PRE-08** — qual o prazo de retenção da trilha?

A primeira costuma ser apresentada como um dilema entre segurança e disponibilidade. Neste sistema,
**esse dilema é em grande parte falso**, e é isso que decide a questão: a autorização do lançamento
(RF-021) já exige leitura do mesmo PostgreSQL onde a trilha é gravada. Se o banco está indisponível, o
lançamento **já falha** por não haver como decidir a permissão. Tornar a gravação da trilha bloqueante
não introduz um novo modo de falha relevante — apenas fecha a janela estreita em que o banco lê mas
não escreve (disco cheio, tabela em somente-leitura, permissão errada). Nessa janela específica, um
sistema que "deixa passar" produz exatamente o cenário que a auditoria existe para impedir: acesso
concedido sem registro.

## Decisão

### Parte 1 — Auditoria de eventos de segurança é bloqueante

**A gravação do registro de auditoria faz parte da mesma transação que concede o acesso.** Se o
registro não puder ser gravado, o acesso não é concedido.

Aplica-se a: autenticação (RF-036), lançamento de aplicativo (RF-037), negativa de autorização
(RF-039), ação administrativa (RF-041) e uso de certificado do cofre (RF-042).

**Não** se aplica a: telemetria, métricas operacionais, indicador de latência, log de diagnóstico do
launcher e fim de sessão (RF-038) — este último é observado após o fato e não pode bloquear nada.

Condições que fazem parte da decisão:

1. A falha apresenta ao usuário mensagem acionável, em português, que **não** expõe detalhe interno
   (RNF-043), e gera alerta operacional imediato.
2. A falha é registrada onde for possível — no mínimo no log estruturado da aplicação (RNF-039) —
   ainda que a trilha em banco esteja indisponível.
3. **Sessões já abertas não são afetadas** (RNF-032). O bloqueio atinge novos lançamentos, nunca o
   trabalho em andamento. Esta condição é o que torna a decisão suportável na operação diária de um
   escritório contábil.

### Parte 2 — Retenção

| Categoria de trilha | Padrão | Mínimo configurável | Máximo configurável |
|---|---|---|---|
| Log de acesso e de lançamento (RF-036..RF-040) | **12 meses** | 6 meses | 60 meses |
| Trilha administrativa (RF-041) | **24 meses** | 12 meses | 60 meses |
| Trilha de uso de certificado digital (RF-042) | **60 meses** | 60 meses | sem limite |

4. A retenção é **configurável por tenant**, dentro dos limites acima. O tenant não pode configurar
   abaixo do mínimo — isso protege o provedor no Caminho B de instrução de cliente que o deixaria sem
   evidência.
5. O expurgo é automatizado, executado por rotina, e **o próprio expurgo é registrado** (o quê, quanto,
   quando, por qual política) — caso contrário, some justamente o rastro de que algo sumiu.
6. `PREMISSA:` (PRE-19) os prazos de 24 e 60 meses são escolha de engenharia com folga, **não parecer
   jurídico**. A trilha de certificado, em particular, é evidência potencial em disputa sobre
   assinatura e deve ser confirmada com o advogado que revisar o termo de custódia (T-002).

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| **Auditoria não bloqueante, apenas com alerta** (PRE-09 original) | Preserva disponibilidade num cenário que quase não existe — banco que lê mas não escreve — ao custo de permitir acesso sem registro exatamente quando algo já está errado no sistema. Má troca. |
| **Gravação assíncrona em fila, com garantia de entrega** | Boa engenharia, mas introduz fila, persistência intermediária e reconciliação no MVP-0, para um sistema de 10 usuários. Complexidade desproporcional (R-006, R-007). Pode ser reavaliada quando a escala justificar — via novo ADR. |
| **Trilha em arquivo local do servidor além do banco** | Duplica a fonte da verdade e cria divergência entre as duas. O log estruturado (item 2) já cobre a necessidade de não perder o registro da própria falha. |
| **Retenção única de 12 meses para tudo** | Simples, mas trataria a trilha de uso de certificado — a evidência mais sensível do produto — com o mesmo prazo de um log de login. |

## Consequências

**Positivas**
- "Todo acesso concedido tem registro" passa a ser propriedade estrutural do sistema, não promessa
  operacional. É afirmação sustentável diante de auditoria (VP-04, CS-03).
- Custo de implementação praticamente nulo: mesma transação, mesmo banco.
- Retenção diferenciada reconhece que as três trilhas têm valor probatório diferente.

**Negativas**
- Um erro de configuração no banco (disco cheio, permissão) passa a parar novos lançamentos. Isso é
  intencional, mas exige monitoramento de espaço em disco desde o MVP-0 (RNF-040).
- Retenção de 60 meses da trilha de certificado implica planejamento de armazenamento e de backup a
  longo prazo.

**Riscos**
- **Disco cheio vira incidente de indisponibilidade.** Mitigação: alerta de espaço em disco e política
  de expurgo funcionando desde o MVP-0, não deixada para depois.
- Cliente do piloto pode exigir retenção diferente da tabela. A configurabilidade por tenant cobre o
  caso, respeitados os mínimos.

## Requisitos relacionados

**Alterados por este ADR:** RNF-022 (passa a bloqueante, Must), RNF-018 (ganha a tabela de prazos por
categoria). **Resolve:** PRE-08, PRE-09.
**Relacionados:** RF-036, RF-037, RF-038, RF-039, RF-041, RF-042, RNF-015, RNF-019, RNF-032, RNF-039,
RNF-040, RNF-043 · Origem: RA-07, VP-04, CS-03, T-002
