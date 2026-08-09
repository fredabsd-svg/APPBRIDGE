# ADR-0003 — Acesso externo no MVP-0: rede privada em malha, sem RD Gateway
Data: 2026-08-08 · Status: **aceito** · Autor: Arquiteto de Software Principal (decisão delegada por Frederico em 2026-08-08)

## Contexto

O MVP-0 é dogfood interno (PRE-01, ~10 usuários), mas parte da equipe precisa trabalhar fora do
escritório. A regra RP-06 e o requisito RNF-001 são absolutos: **a porta 3389 nunca é exposta à
internet**. A resposta P5 definiu que RD Gateway + MFA ficam para a V2 e que, no MVP-0, o acesso
remoto se apoia na rede privada **Tailscale** já em uso pelo escritório.

Há uma sutileza que precisa ficar escrita, porque é onde esse tipo de arranjo costuma falhar: colocar
o session host numa rede em malha **não elimina o 3389** — apenas o move para uma interface privada.
Qualquer dispositivo autorizado na malha passa a alcançar o 3389 do session host. Sem regra explícita,
"ninguém da internet alcança" vira "todo notebook pessoal cadastrado alcança".

## Decisão

**No MVP-0 não há RD Gateway nem qualquer publicação na internet. O acesso de fora do escritório se dá
exclusivamente por rede privada em malha (Tailscale), com alcance ao 3389 restrito por lista de
controle de acesso.**

Condições que fazem parte da decisão, não são recomendações:

1. **Nenhum encaminhamento de porta** no roteador do escritório para 3389, 443 ou qualquer porta do
   session host ou do Control Plane.
2. A malha privada tem **ACL explícita**: apenas dispositivos de um grupo nomeado alcançam a porta
   3389 do session host e a porta do Control Plane. O padrão é negar.
3. **Dispositivo é aprovado nominalmente.** Aprovação automática de novo dispositivo fica desligada.
4. O Control Plane também só é alcançável pela rede interna ou pela malha — ele não fica público.
5. Verificação de CS-04 é feita por **varredura externa** contra o IP público do escritório, com o
   resultado registrado. Sem varredura, o requisito não está atendido — está presumido.
6. A malha é solução do **MVP-0/MVP-1**. Ela **não é** a resposta comercial: um cliente do piloto não
   vai instalar cliente de VPN em máquina de terceiro. RD Gateway + MFA (RF-009) continua sendo o
   caminho da V2, e o relay de saída (túnel reverso, sem abrir portas) segue como candidato à
   evolução comercial descrita em §2.4 do prompt mestre.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| **RD Gateway + MFA já no MVP-0** | É a solução correta a médio prazo, mas custa certificado público, publicação, endurecimento e configuração de MFA — trabalho de infraestrutura que compete com o prazo do MVP-0 (R-006) para atender ~10 pessoas que já têm malha privada funcionando. Antecipar traria pouco valor e muito risco de calendário. |
| **VPN IPsec/SSL tradicional no roteador** | Mais frágil, mais trabalhosa e sem as ACLs por dispositivo que a malha oferece. Também coloca o usuário remoto dentro da rede inteira, não apenas diante do serviço. |
| **Expor 3389 com "porta alternativa" ou lista de IPs** | Violação direta de RP-06 e RNF-001. Segurança por obscuridade. Não é uma alternativa — está registrada aqui apenas para constar como recusada. |
| **Publicar o RD Web Access** | Ainda é publicação na internet, com superfície própria, e sem MFA no MVP-0 seria pior que a malha privada. |

## Consequências

**Positivas**
- Zero superfície pública no MVP-0. CS-04 é atingível e verificável já na primeira semana.
- Custo zero de infraestrutura adicional e nenhum certificado público a gerenciar.
- Compatível com o teletrabalho da equipe do dogfood a partir do dia um.

**Negativas**
- Exige cliente de malha instalado em cada máquina que acessa de fora — aceitável para equipe própria,
  **inviável para cliente do piloto**.
- Cria um caminho de acesso que precisa ser **desativado**, e não apenas complementado, quando o RD
  Gateway entrar na V2. Se os dois coexistirem sem revisão, a superfície some do radar.

**Riscos**
- **O risco central é organizacional:** a malha privada é tão confortável que pode adiar o RD Gateway
  indefinidamente, e o piloto comercial chegar sem caminho de acesso vendável. Mitigação: RF-009
  é **Must da V2**, e a revisão desta decisão é pré-requisito do piloto.
- Comprometimento de um dispositivo pessoal autorizado na malha é comprometimento de um caminho até o
  3389. Mitigação: ACL por grupo, aprovação nominal e AppLocker no host (RNF-006).

## Requisitos relacionados

RNF-001, RNF-009, RNF-003, RNF-006 · RF-009 (V2) · CS-04 · Origem: P5, RP-06, PR-04
