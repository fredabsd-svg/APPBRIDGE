# ADR-0013 — Replanejamento: MVP-0 em duas etapas e piloto no 2º trimestre de 2027
Data: 2026-08-08 · Status: **aceito** · Autor: Arquiteto de Software Principal · Aprovado por: Frederico (2026-08-08)

## Contexto

`ROADMAP.md` §4 quantificou o backlog do MVP-0 em **241 pontos**. Com a âncora de PRE-25 (1 ponto ≈
meio dia de trabalho focado), são ≈ 120 dias de trabalho, contra ≈ 32 dias úteis na janela definida
em P8 — e esses dias não são integrais, porque Frederico dirige um escritório de contabilidade
(PRE-26). A conclusão sobrevive a erro de 2× na estimativa: **o MVP-0 completo não caberia até
meados de outubro de 2026** (R-024).

Foram apresentadas três opções para o piloto. Frederico escolheu a **opção A**.

Este ADR existe porque alterar marcos e recortes de fase é mudança de escopo, e mudança de escopo sem
ADR é violação de processo (RP-07, RA-06). A regra de ouro do escopo continua valendo: nada muda de
fase sem registro.

## Decisão

### 1. O MVP-0 passa a ser entregue em duas etapas

| Etapa | Conteúdo | Pontos | Alvo |
|-------|----------|--------|------|
| **MVP-0a · esqueleto ambulante** | E-01 e E-02 completos; T-301, T-304; T-401, T-402; T-501 a T-504; launcher no mínimo necessário para disparar o lançamento — **sem MSIX, sem atalhos, sem prelaunch** | ≈ 95 | **meados de out/2026** |
| **MVP-0b · dogfood real** | Todo o restante: launcher empacotado, atalhos, prelaunch, reconciliação, retenção, segurança e a semana de dogfood dirigido | ≈ 146 | **dez/2026 a jan/2027** |

**Critério de aceite do MVP-0a:** Frederico abre o Domínio Contábil pelo AppBridge, na própria
estação, sem digitar senha, com `.rdp` assinado, registro em trilha e a porta 3389 comprovadamente
fechada (V-01, V-05, V-06).

**Critério de aceite do MVP-0b:** CS-01 a CS-04 integralmente.

O recorte do MVP-0a não é o "primeiro terço do backlog": ele é escolhido para **validar cedo as três
premissas que ainda podem derrubar o desenho** — PRE-22 (o prelaunch sustenta a jornada?), PRE-23 (o
Connection Broker responde com confiabilidade?) e PRE-11 (tempo de abertura). Um resultado ruim em
qualquer uma delas em outubro é recuperável; em janeiro, às vésperas do piloto, não é.

### 2. O piloto do Caminho B vai para o 2º trimestre de 2027

O marco M3 deixa de ser 1º tri/2027 e passa a **abr–jun/2027**, com 3–5 escritórios, como previsto
originalmente em P8 — o que muda é a data, não o tamanho do piloto.

### 3. Os portões G-01 a G-05 permanecem intransponíveis

Adiar o piloto **não afrouxa** nenhum portão. G-01 (confirmação por escrito de que as licenças de
Domínio e Alterdata permitem execução multiusuário) segue podendo encerrar o Caminho B, e o prazo
maior deve ser usado para concluí-lo com folga, não para postergá-lo.

### 4. Ordem de início: infraestrutura e licenciamento antes de código

T-101 e T-102 (aquisição e VMs) e G-01/T-001 (licenciamento) começam **antes da primeira linha de
código**. São caminho crítico, não dependem de aprovação de documento, e G-01 é a tarefa de maior
retorno sobre esforço do projeto inteiro (R-023).

## Alternativas consideradas

| Opção | Descrição | Por que não |
|-------|-----------|-------------|
| **B** | Piloto reduzido no 1º tri/2027, com 1 escritório e sem cofre | Valida o modelo comercial mais cedo, mas com dogfood terminando em janeiro sobraria menos de um mês entre o fim da validação interna e a entrada de dado de terceiros. Também comprimiria G-03 e G-04. |
| **C** | Manter 3–5 escritórios no 1º tri/2027 | Levaria a produção um sistema sem dogfood completo, sem PS-02 e sem V-09. Foi desaconselhada na apresentação. |
| **D** | Manter o MVP-0 inteiro em outubro, cortando escopo no meio do caminho | É o cenário que o replanejamento existe para evitar: sob pressão, o que cai primeiro é teste, documentação e verificação de segurança — exatamente o que sustenta as afirmações de `SEGURANCA.md` §10. |
| **E** | Contratar ajuda para caber em outubro | Não foi colocada como opção por Frederico e mudaria a estrutura de custo do projeto. Permanece disponível como resposta futura a R-006, via novo ADR. |

## Consequências

**Positivas**
- O cronograma passa a ser defensável em vez de otimista, e o replanejamento acontece **antes** da
  implementação, não no meio dela.
- O MVP-0a força a validação precoce de PRE-11, PRE-22 e PRE-23, que são as premissas capazes de
  invalidar decisões já tomadas.
- O trimestre adicional dá folga real para T-001 (G-01), T-002 (parecer jurídico), T-003 (cotação
  SPLA), PS-02 e PS-03 — todos itens que estavam apertados no plano original.

**Negativas**
- A validação comercial do Caminho B atrasa um trimestre. Em um mercado com concorrente estabelecido
  e preço formado (RM-09), tempo é desvantagem.
- Um trimestre a mais de custo sem receita.
- O MVP-0a, por não ter atalhos nem prelaunch, **não é demonstrável como produto**. É marco de
  engenharia, e precisa ser comunicado como tal — inclusive para não gerar expectativa no escritório.

**Riscos**
- **O MVP-1 passa a ser o novo gargalo.** Com o dogfood terminando em janeiro e o piloto começando
  em abril, sobram ~3 meses para os épicos E-13 a E-18 — que incluem o painel, a trilha
  administrativa, o encerramento de sessão (RF-008, que fecha R-014) e o metering mínimo. Pela mesma
  aritmética da §4 do roadmap, **é improvável que o MVP-1 completo caiba nessa janela**. Registrado
  como **R-025**, com decisão pendente em **B-009**: definir um subconjunto mínimo do MVP-1 exigido
  pelo piloto. A recomendação preliminar é priorizar RF-008, o metering mínimo e a gestão de
  permissões pelo painel, adiando favoritos (RF-017), atualização automática (RF-035) e exportação
  (RNF-021).
- Datas mais folgadas tendem a ser consumidas. Mitigação: o MVP-0a tem critério de aceite objetivo e
  data fixa, e as medições de T-1003 e T-1002 são entregáveis dele, não formalidades.

## Requisitos relacionados

Não altera nenhum RF/RNF — altera **fase de entrega e marcos**. Afeta: CS-01 a CS-05 (datas),
E-01 a E-12 (agrupamento), G-01 a G-05 (prazo, não conteúdo) · Origem: R-024, R-006, R-007, P8 ·
Substitui a linha do tempo proposta em P8 e refletida em `ROADMAP.md` §2.
