# ADR-0014 — O licenciamento dos aplicativos hospedados é responsabilidade do cliente
Data: 2026-08-08 · Status: **aceito** · Autor: Arquiteto de Software Principal · Decisão de: Frederico (2026-08-08)

## Contexto

`ROADMAP.md` estabeleceu o portão **G-01**: antes do piloto, seria necessário obter dos fornecedores
dos aplicativos-alvo (Domínio/Thomson Reuters, Alterdata) **confirmação por escrito** de que suas
licenças permitem execução em servidor de sessão multiusuário. O risco coberto era **R-001**,
classificado como crítico, por poder invalidar o Caminho B.

Para executar esse portão, a sessão S003 preparou cartas de consulta formal aos fornecedores.

Frederico determinou outro caminho: **o cliente instala e usa**. A licença é do cliente, adquirida
por ele junto ao fornecedor, e o AppBridge não intermedia, não revende e não consulta fornecedor em
nome de ninguém.

Isso é coerente com um não-objetivo já aprovado — **NO-04**: *"Não substituímos, revendemos nem
emulamos os aplicativos hospedados. Domínio, Alterdata, Office e ERPs continuam sendo licenciados
pelo cliente ou pelo provedor, sob os contratos dos respectivos fornecedores."* Este ADR **restringe**
essa formulação: passa a ser **sempre pelo cliente**, nunca pelo provedor.

## Decisão

**A titularidade e a conformidade das licenças dos aplicativos hospedados são do cliente.**

1. O cliente **adquire, instala e utiliza** suas próprias licenças dos aplicativos que rodarão sobre
   o AppBridge. O AppBridge fornece a plataforma de distribuição — não fornece o software hospedado.
2. **O AppBridge não consulta fornecedores de software em nome do cliente.** As cartas de consulta
   preparadas em S003 são removidas do repositório.
3. **O portão G-01 deixa de ser "confirmação escrita do fornecedor"** e passa a ser **"declaração de
   titularidade e conformidade de licença, assinada pelo cliente"**, anexa ao contrato de prestação
   de serviços, na qual o cliente declara possuir as licenças dos aplicativos que solicitar publicar
   e responder por sua conformidade.
4. A **matriz de licenciamento é mantida**, com finalidade alterada: deixa de ser resultado de
   consulta a fornecedor e passa a ser **registro do que o cliente declarou** — aplicativo, versão,
   tipo de licença, quantidade. Serve à operação (dimensionamento, metering) e como evidência do que
   foi declarado.
5. Isso **não altera nenhum RF ou RNF**. O produto continua o mesmo; muda a alocação de
   responsabilidade contratual e um portão de processo.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| **Consultar os fornecedores** (plano original, G-01 com cartas) | Depende de terceiros sem prazo, expõe a operação a uma negativa que talvez não fosse aplicada na prática, e coloca o AppBridge numa conversa contratual que não é dele — o contrato de licença é entre o cliente e o fornecedor. |
| **O provedor adquirir e revender as licenças** | Contraria NO-04 diretamente, transformaria o AppBridge em revenda de software de terceiros e traria para si a responsabilidade integral de conformidade. |
| **Ignorar o tema** | Deixaria o provedor exposto sem nenhum instrumento. A declaração do item 3 é o custo mínimo para que a alocação de responsabilidade exista de fato. |

## Consequências

**Positivas**
- Remove do caminho crítico uma dependência de terceiros sem prazo de resposta.
- Alinha o produto ao seu não-objetivo: o AppBridge distribui aplicativos, não licencia software.
- Simplifica a entrada de cliente no piloto: sem espera por posicionamento de fornecedor.
- Reduz a superfície contratual do provedor.

**Negativas**
- O provedor perde a informação que a consulta traria. Ele passa a operar sem saber se algum
  fornecedor veda o cenário — e continuará sem saber até que, eventualmente, alguém pergunte.

**Riscos**
- **R-001 muda de natureza, não desaparece.** Quem instala não altera o que a licença permite: se o
  contrato de um fornecedor vedar execução em servidor de sessão, a vedação existe mesmo com o
  cliente instalando. O que a declaração faz é **alocar a responsabilidade**, não eliminar o fato.
- **Residual específico do Caminho B:** alguns termos de licença restringem a execução em
  infraestrutura operada por terceiro **independentemente de quem detém a licença**. Nesse cenário, a
  declaração do cliente não protege o provedor de um questionamento do fornecedor. É o resíduo
  conhecido desta decisão, e está registrado como **R-001 (reescrito)**.
- **Risco de suporte, que recai sobre o cliente:** se o fornecedor recusar suporte por o software
  estar em servidor de sessão, quem fica sem suporte é o cliente — mas a insatisfação chega ao
  provedor, porque foi a plataforma que mudou o modo de uso. Recomenda-se que a declaração do item 3
  mencione esse ponto explicitamente.
- **Efeito colateral positivo sobre o DIF-02:** com a conformidade sendo do cliente, o metering de
  licenças (RF-062..RF-064) ganha valor — passa a ser o instrumento com que o cliente **demonstra**
  que respeita o número de licenças que declarou possuir. Reforça a decisão de ADR-0006 de antecipá-lo.

## Requisitos relacionados

Não altera RF nem RNF. Afeta **NO-04** (restringe a formulação), o portão **G-01** de `ROADMAP.md`,
a tarefa **T-001** e o risco **R-001** · Origem: decisão de Frederico, 2026-08-08
