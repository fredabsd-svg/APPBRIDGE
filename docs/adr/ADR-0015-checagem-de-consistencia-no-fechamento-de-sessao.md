# ADR-0015 — Checagem de consistência obrigatória no fechamento de sessão
Data: 2026-08-08 · Status: **aceito** · Autor: Arquiteto de Software Principal · Aprovado por: Frederico (2026-08-08)

## Contexto

A auditoria da sessão S006 encontrou **seis casos de deriva documental** acumulados em poucas sessões
de trabalho. Nenhum era violação de processo — não houve mudança silenciosa de escopo, stack, modelo
de dados ou API. Todos eram documentos que **deixaram de refletir decisões já registradas**.

Dois deles tinham efeito prático:

- Os sete entregáveis mantinham "aguardando aprovação" no cabeçalho depois de aprovados, de modo que
  qualquer pessoa que abrisse um deles concluiria o oposto do que dizia o `STATUS.md`.
- `VISAO.md` ainda descrevia o modelo de licenciamento anterior ao ADR-0014 em três pontos, um deles
  classificando como **crítico** um risco que já havia sido reescrito.

O padrão é revelador: **os documentos mais estáveis são os menos revisitados, e por isso os que mais
mentem.** `VISAO.md` é o mais lido do conjunto.

A regra **RA-02** exige, no fechamento de sessão, gerar o log, atualizar o `STATUS.md` e o
`CHANGELOG.md`. Ela garante que o que **mudou** seja registrado — não que o que **já existia**
continue verdadeiro depois da mudança. É essa lacuna que produz a deriva.

## Decisão

**RA-02 passa a exigir uma quarta etapa: a checagem de consistência da documentação.**

Texto acrescentado à regra em `CLAUDE.md`:

> (4) a **checagem de consistência** — executar `./scripts/check-docs.sh` e resolver os achados, e
> confirmar que os documentos afetados pelas decisões da sessão foram atualizados. Deriva documental
> é violação de RA-06 tanto quanto mudança silenciosa.

A checagem tem duas metades, e a distinção é deliberada:

**Mecânica**, no script `scripts/check-docs.sh` — sete verificações: cabeçalhos de estado
contraditórios com o `STATUS.md`, contagem de ADRs, ADR referenciado mas inexistente, requisito usado
sem definição em `REQUISITOS.md`, link interno morto, uso dos prefixos reservados `RP-`/`RA-` como
identificador próprio, e ausência do log da sessão corrente. Sai com código 1 se houver achado.

**Humana**, que o script não faz e não deve fingir fazer: *o conteúdo ainda reflete as decisões
vigentes?* Nenhuma expressão regular teria detectado o achado nº 2 de S006 — só a leitura detecta.
O script termina imprimindo exatamente essa pergunta.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| **Manter RA-02 como está e confiar na atenção** | É o que produziu os seis achados. Disciplina humana não escala com 14 ADRs e 9 documentos que se referenciam. |
| **Só a checagem mecânica, sem a etapa de leitura** | Daria falsa segurança: o achado mais grave de S006 (`VISAO.md` descrevendo o modelo antigo) é invisível a qualquer verificação automática. |
| **Só a checagem humana, sem script** | Verificações repetitivas — contagem, link morto, ID órfão — são exatamente o que a pessoa deixa de fazer quando está cansada no fim da sessão. |
| **Auditoria periódica em vez de por sessão** | A deriva de S006 levou poucas sessões para aparecer. Auditoria mensal deixaria a documentação errada por semanas, e é justamente nesse intervalo que decisões se apoiam nela. |
| **Verificação em CI** | Melhor a médio prazo e compatível com esta decisão — o script já sai com código 1. Não se adota agora porque não existe pipeline; quando existir, é só chamá-lo. |

## Consequências

**Positivas**
- Deriva documental deixa de ser encontrada por acaso e passa a ser encontrada por procedimento.
- O custo é de minutos por sessão, contra o custo de decidir com base em documento errado.
- O script é o começo do ferramental de qualidade do projeto, que hoje não tem nenhum.

**Negativas**
- Mais uma etapa no fechamento de sessão, com risco de virar carimbo se executada sem atenção.
- O script precisa de manutenção: cada convenção nova de identificador ou de estrutura pode exigir
  uma verificação nova, ou produzir falso positivo.

**Riscos**
- **Falso positivo corrói a confiança na ferramenta.** Dois apareceram na primeira execução — a
  legenda de estados do `STATUS.md` e o `ADR-0000` do template — e foram corrigidos de imediato. Se
  voltarem a aparecer com frequência, a tendência natural é ignorar a saída, e aí a checagem passa a
  custar sem entregar.
- **Falso negativo é o risco maior:** passar no script pode ser lido como "documentação consistente",
  quando ele só cobre o mecanizável. Mitigação escolhida: a saída de sucesso **não diz que está tudo
  certo** — diz que a parte mecânica passou e pergunta pela humana.

## Requisitos relacionados

Não altera RF nem RNF. Altera a regra de processo **RA-02** e reforça **RA-06** · Relacionado a
RNF-050 (documentação atualizada como definição de pronto) · Origem: auditoria da sessão S006
