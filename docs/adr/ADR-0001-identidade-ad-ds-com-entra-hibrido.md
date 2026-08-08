# ADR-0001 — Identidade: AD DS como diretório-base, com Entra ID em modelo híbrido
Data: 2026-08-08 · Status: **aceito** · Autor: Arquiteto de Software Principal (decisão delegada por Frederico em 2026-08-08)

## Contexto

O AppBridge precisa autenticar o usuário e, em seguida, abrir uma sessão RDS em nome dele. São dois
problemas distintos que costumam ser confundidos:

1. **Quem é o usuário** perante o Control Plane (autenticação da API e do launcher);
2. **Qual conta abre a sessão** no session host.

A pilha RDS clássica resolve (2) exclusivamente com **AD DS**: RD Connection Broker e RD Session Host
precisam estar ingressados em domínio, e a sessão é aberta por uma conta de domínio. Session host
ingressado apenas no Entra ID é caminho de **AVD**, não de RDS clássico. Portanto, "usar Entra ID e
pronto" não é uma opção disponível nesta arquitetura.

Por outro lado, o público-alvo (PA-01) trabalha cada vez mais fora do escritório, e o Caminho B
exigirá MFA na V2 — terreno onde o Entra ID é muito superior a qualquer coisa que construamos.

`PREMISSA:` (PRE-03) o escritório do dogfood está hoje em workgroup; criar o domínio é tarefa do MVP-0.

## Decisão

**AD DS é o diretório-base e a autoridade da conta que abre a sessão RDS. O Entra ID é o provedor de
autenticação do Control Plane, em modelo híbrido sincronizado.**

Concretamente:

1. Um domínio AD DS é criado no MVP-0. Session hosts e Connection Broker são ingressados nele.
2. **Microsoft Entra Connect Sync** replica as contas do AD DS para o Entra ID (identidade híbrida).
3. O launcher autentica o usuário **contra o Entra ID** (fluxo OIDC) e apresenta o token ao Control
   Plane, que emite seu próprio token de sessão (RF-001, RF-003, RF-004).
4. O Control Plane mantém, para cada identidade autenticada, o **vínculo com a conta AD DS**
   correspondente (RF-002), e é essa conta que aparece no arquivo `.rdp` gerado.
5. **A autorização é do AppBridge, não do diretório.** O diretório responde "quem é" e "de quais
   grupos participa"; quem decide "pode abrir este aplicativo" é o Control Plane (RF-021).
6. No MVP-0, para o dogfood, é aceitável autenticar diretamente contra o AD DS enquanto o tenant do
   Entra não estiver pronto — mas o vínculo do item 4 já existe desde o primeiro dia, para que a
   troca não seja refatoração.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| **Entra ID puro, sem AD DS** | Não atende RDS clássico: Connection Broker e session hosts exigem domínio. Levaria a AVD, mudando motor, custo (Azure em dólar) e fase. Contraria RM-07 como estratégia de curto prazo. |
| **AD DS puro, sem Entra** | Simples e barato no MVP-0, mas empurra para o AppBridge a construção de MFA, autenticação de fora da rede e federação — trabalho grande, mal feito, e reinvenção de roda (RP-09). Bloquearia RF-009 na V2. |
| **Microsoft Entra Domain Services** como AD gerenciado | Elimina o DC próprio, mas amarra o produto ao Azure já no MVP-0, contrariando RNF-037 (rodar on-premises) e o Caminho A (infraestrutura do cliente). |
| **Autenticação própria do AppBridge** (usuário e senha no banco) | Cria mais um repositório de credencial para vazar, sem ganho. Viola o espírito de RP-06. |

## Consequências

**Positivas**
- Compatível com RDS clássico hoje e com AVD amanhã: em ambos os casos há um diretório e um token.
- MFA, acesso condicional e federação chegam na V2 sem código nosso (RF-009).
- O escritório do dogfood ganha domínio — pré-requisito de FSLogix, GPO e AppLocker (RNF-006, RNF-011).

**Negativas**
- Introduz duas peças de infraestrutura no MVP-0 que hoje não existem: o DC e o Entra Connect. É
  trabalho de infraestrutura, não de produto, competindo pelo mesmo prazo (R-006).
- Identidade híbrida tem modos de falha próprios (sincronização, conflito de UPN, senha divergente)
  que o suporte do escritório não conhece.
- Cria dependência de um tenant Entra ID, mesmo que gratuito.

**Riscos**
- **Ponto único de falha:** DC indisponível derruba autenticação *e* abertura de sessão. Mitigação
  no piloto: segundo DC. No MVP-0, aceita-se o risco de um DC só, com backup diário (RNF-033).
- O UPN do usuário no Entra precisa bater com o do AD DS; domínio interno `.local` obriga a
  configurar sufixo de UPN roteável. É a armadilha clássica de Entra Connect e precisa entrar no
  roteiro de implantação.

## Requisitos relacionados

RF-001, RF-002, RF-003, RF-004, RF-005, RF-006, RF-007, RF-009, RF-010, RF-021 ·
RNF-005, RNF-011, RNF-035, RNF-037 · Origem: P2, VP-03, PR-04
