# T-001 — Consulta de licenciamento aos fornecedores
> Tarefa **T-001** · Portão **G-01** do piloto · Risco coberto: **R-001 (crítico)**
> Origem: P3 · Sessão S003 · 2026-08-08

---

## 1. Por que esta tarefa existe e por que ela vem primeiro

O AppBridge executa aplicativos de terceiros em **servidor de sessão multiusuário**. Se o contrato de
licença de Domínio ou de Alterdata vedar esse modo de uso — ou cobrar por ele de forma que inviabilize
o preço-alvo —, o **Caminho B (serviço hospedado) deixa de existir** e o Caminho A perde boa parte do
público-alvo.

Isso é **R-001**, o risco mais grave do projeto inteiro. E tem uma característica rara: **não depende
de código**. A resposta pode chegar antes da primeira linha escrita.

> **A pergunta que essa consulta responde na prática:** "vale a pena construir isto?"
> Uma negativa aqui economiza meses. Uma confirmação escrita vale mais que qualquer sprint.

Por isso T-001 é a tarefa de maior retorno sobre esforço do projeto, e por isso ela e a aquisição de
hardware são as duas primeiras coisas a começar (`ROADMAP.md` §8, R-023).

## 2. Os dois cenários — e por que eles precisam ser perguntados separadamente

Esta é a decisão mais importante da redação das cartas. Muitos fornecedores tratam os dois de forma
completamente diferente, e uma resposta genérica ("sim, pode usar em servidor") **não distingue**:

| Cenário | Descrição | Fase | Se for vedado |
|---------|-----------|------|---------------|
| **C-1 · Uso próprio** | O escritório instala em servidor próprio e seus **próprios colaboradores** acessam remotamente | MVP-0, dogfood | O dogfood não acontece. O projeto para |
| **C-2 · Hospedagem para terceiros** | Um provedor hospeda o software e **escritórios clientes** acessam | Piloto, Caminho B | O Caminho B morre. O Caminho A sobrevive |

**C-1 costuma ser permitido; C-2 é onde mora o risco.** Uma carta que pergunte apenas "posso usar em
servidor?" recebe um "sim" que não cobre C-2 — e o problema só aparece quando já houver clientes.

## 3. O que caracteriza uma resposta útil

Uma resposta serve para G-01 quando tem **as quatro** características:

1. **Por escrito** — e-mail com identificação de quem responde. Conversa de chat ou telefone não serve.
2. **De canal com autoridade** — gerente de conta, canal comercial formal ou jurídico. **Resposta de
   atendente de suporte de primeiro nível não vale**, porque não vincula o fornecedor.
3. **Com referência à cláusula** do contrato ou do EULA, e não apenas a opinião de quem respondeu.
4. **Distinguindo C-1 de C-2** explicitamente.

> **A armadilha mais comum:** o fornecedor não veda, mas declara que **não presta suporte** nesse
> cenário. Na prática, isso é um veto disfarçado — um escritório contábil não opera um sistema
> fiscal sem suporte do fabricante. Por isso a pergunta sobre suporte é item próprio nas cartas, e
> não uma nota de rodapé.

## 4. Arquivos desta pasta

| Arquivo | Uso |
|---------|-----|
| `carta-modelo.md` | Modelo parametrizado. Serve para qualquer fornecedor, inclusive ERPs de clientes |
| `carta-dominio.md` | Versão pronta para Domínio / Thomson Reuters |
| `carta-alterdata.md` | Versão pronta para Alterdata |
| `matriz-licenciamento.md` | **O entregável de T-001** — a tabela que consolida as respostas |

## 5. Como conduzir

1. **Levantar o inventário real** antes de enviar: aplicativo, versão, módulos e número de usuários.
   As cartas têm um quadro para isso; enviar com o quadro vazio enfraquece a consulta.
2. **Enviar pelo canal comercial formal**, com cópia para o gerente de conta.
3. **Registrar a data de envio** na `matriz-licenciamento.md`.
4. **Cobrar em 10 dias úteis** se não houver resposta. Silêncio não é permissão.
5. **Arquivar a resposta** — o e-mail é evidência contratual e precisa sobreviver a troca de caixa
   postal. Guardar em local que não seja apenas o webmail de uma pessoa.
6. **Consolidar na matriz** e reavaliar o Caminho B à luz do conjunto.

## 6. Office e Microsoft — não precisa de carta

O caso do Office é diferente: as regras são públicas e a questão não é "posso?", e sim **"qual SKU
comprar"**. Conforme P3, o que precisa ser verificado com um revendedor Microsoft:

- Office em servidor de sessão exige **licença por volume (LTSC)** ou **Microsoft 365 Apps com
  ativação em computador compartilhado**. Licença **OEM ou varejo não atende** a esse cenário.
- **RDS CAL por usuário** para cada pessoa que acessar (já previsto em T-101).
- No Caminho B, a hospedagem para terceiros normalmente passa por **SPLA**, o que muda a estrutura de
  custo — é o que T-003 (cotação SPLA) precisa apurar, e o que valida ou derruba PRE-05.

Estes três itens entram na `matriz-licenciamento.md` como linhas próprias, com origem "revendedor",
e não como resposta de carta.

## 7. Critério de conclusão de T-001

T-001 está concluída quando a `matriz-licenciamento.md` tiver, **para cada aplicativo do inventário**,
a resposta de C-1 e de C-2 com evidência arquivada — ou a constatação registrada de que o fornecedor
se recusou a responder, o que é, por si só, um resultado a considerar na decisão sobre o Caminho B.
