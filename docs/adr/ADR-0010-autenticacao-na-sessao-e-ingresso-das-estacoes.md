# ADR-0010 — Autenticação na sessão RDS e exigência de ingresso das estações no domínio
Data: 2026-08-08 · Status: **aceito** · Autor: Arquiteto de Software Principal (decisão delegada por Frederico em 2026-08-08)

## Contexto

RNF-042 é o requisito que sustenta toda a proposta de valor VP-02: *o usuário não precisa saber que o
aplicativo é remoto*. Ele clica no atalho e o aplicativo abre.

Há um detalhe entre "clicar no atalho" e "o aplicativo abrir" que decide se essa promessa se cumpre:
**o `mstsc` pede senha**. O AppBridge autentica o usuário no Control Plane (ADR-0001) e entrega um
`.rdp` assinado, mas essa autenticação **não é a mesma** que abre a sessão no session host. Quem abre
a sessão é o Windows, contra o AD DS, e por padrão ele pede credencial.

Se cada abertura de aplicativo pedir senha, o produto deixa de "parecer local" e vira um RDP com
catálogo bonito — perde-se VP-02, CS-01 fica comprometido e a adoção sem treinamento (que é o
argumento de venda para PA-01) desaparece.

O caminho suportado para eliminar o pedido de senha é a **delegação de credenciais padrão** (CredSSP),
habilitada por GPO na estação, apontando para os session hosts. Ela funciona quando a estação está
ingressada no domínio e o usuário fez logon com a conta de domínio: o Windows reaproveita a credencial
do logon local.

`PREMISSA:` (PRE-03) o escritório está hoje em workgroup. Criar o domínio já é tarefa do MVP-0
(ADR-0001) — este ADR decide se as **estações** também entram nele.

## Decisão

**As estações do MVP-0 são ingressadas no domínio AD DS, e o logon sem senha na sessão é obtido por
delegação de credenciais padrão configurada por GPO.**

1. Estações ingressadas no domínio, usuários fazendo logon com conta de domínio.
2. GPO habilita a delegação de credenciais padrão **restrita aos session hosts nominados** — não a
   curinga amplo. Delegação para `TERMSRV/*` é recusada por RP-06 (menor privilégio): ela autorizaria
   a estação a entregar a credencial do usuário a qualquer servidor alcançável.
3. O `.rdp` gerado traz o nome de usuário preenchido, para que a sessão abra na identidade correta
   sem digitação.
4. **Caminho degradado, explícito:** para estação não ingressada (máquina pessoal, notebook de
   terceiro), o `mstsc` pede a credencial e o usuário pode salvá-la no Windows Credential Manager. O
   produto continua funcionando, com uma etapa a mais. Esse caminho é **suportado, não recomendado**,
   e não atende RNF-042 plenamente — o que precisa estar dito no material do produto, não descoberto
   pelo cliente.
5. O ingresso das estações no domínio entra no **roteiro de implantação do MVP-0** como pré-requisito,
   não como tarefa opcional de infraestrutura.
6. A senha de domínio **nunca transita pelo Control Plane e nunca é armazenada por ele** (RNF-004). O
   Control Plane emite autorização e descritor de conexão; a credencial é assunto entre a estação e o
   Windows.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| **O Control Plane guarda a senha do usuário e a injeta no `.rdp`** | Tecnicamente possível e absolutamente proibido: transformaria o Control Plane em repositório de senhas de domínio em claro ou reversível. Viola RP-06 de forma que nenhum ganho de usabilidade compensa. Recusada sem hesitação. |
| **Deixar o `mstsc` pedir senha sempre** | Preserva a simplicidade da infraestrutura e sacrifica VP-02, que é a razão de o produto existir. Se o usuário digita senha a cada aplicativo, não há diferença percebida frente a um RDP comum. |
| **Delegação de credenciais com curinga `TERMSRV/*`** | Resolve com uma linha de GPO e amplia a superfície de ataque para qualquer servidor que a estação alcance. Menor privilégio (RNF-005) proíbe. |
| **Estações fora do domínio + Credential Manager** | É o caminho degradado do item 4. Como decisão padrão, deixaria a credencial salva em cada estação, com sincronização manual a cada troca de senha. |
| **Adiar o ingresso das estações para o MVP-1** | Adiaria justamente a validação de CS-01 (um dia de trabalho real sem atrito), que é o critério de sucesso do MVP-0. |

## Consequências

**Positivas**
- Clicar no atalho abre o aplicativo. VP-02 e RNF-042 passam a ser atingíveis de fato.
- Ingressar as estações habilita, de quebra, GPO, distribuição da impressão digital do certificado de
  assinatura (ADR-0009) e a política de redirecionamento (ADR-0008) — três coisas que o produto já
  precisa e que, sem domínio, teriam de ser configuradas máquina a máquina.

**Negativas**
- Aumenta o escopo de infraestrutura do MVP-0: além de criar o domínio, é preciso ingressar cada
  estação, o que envolve tocar em todas as máquinas do escritório uma vez. Some com R-006.
- Cria dependência do domínio para o trabalho diário: DC indisponível passa a afetar também o logon
  das estações, não só o AppBridge. É o preço de ter um domínio — e é o desenho normal de qualquer
  escritório com AD.

**Riscos**
- **O ingresso das estações é a tarefa que mais facilmente estoura o prazo do MVP-0**, porque depende
  de disponibilidade das máquinas e das pessoas, não de código. Deve ser sequenciado cedo no roteiro,
  não deixado para a véspera do teste de CS-01.
- Estações antigas em Windows 10 fora de suporte (R-008) entram no domínio e passam a ter credencial
  de domínio, o que eleva o impacto de um comprometimento dessas máquinas. Reforça a necessidade de
  tratar R-008.
- `PREMISSA:` (PRE-21) todas as estações do escritório rodam edição do Windows que permite ingresso em
  domínio — **Home não permite**. Se houver máquinas Home, elas seguem pelo caminho degradado do item
  4 ou precisam de atualização de edição. A confirmar no levantamento do parque.

## Requisitos relacionados

RF-005, RF-018, RF-022, RF-029, RF-030 · RNF-004, RNF-005, RNF-042, RNF-045 · ADR-0001, ADR-0008,
ADR-0009 · Origem: VP-02, CS-01, PR-04
