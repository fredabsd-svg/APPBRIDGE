# ADR-0009 — Estratégia de assinatura do `.rdp` e sua consequência sobre a hospedagem do Control Plane
Data: 2026-08-08 · Status: **aceito** · Autor: Arquiteto de Software Principal (decisão delegada por Frederico em 2026-08-08)

## Contexto

RNF-002 e RF-019 exigem que **todo arquivo `.rdp` entregue ao cliente seja assinado**. A assinatura é
o que impede que um `.rdp` adulterado — apontando para outro host, ou pedindo redirecionamento que a
política nega — seja aceito pela estação do usuário.

O mecanismo suportado pela Microsoft é o **`rdpsign.exe`**, utilitário do Windows que assina o arquivo
com um certificado de assinatura de código e grava a assinatura no próprio `.rdp`. Do lado do cliente,
a confiança é estabelecida por GPO, informando a impressão digital dos publicadores confiáveis.

O ponto que precisa de decisão explícita: **o formato de assinatura do `.rdp` não é uma especificação
pública implementável com segurança em código próprio**. Assinar significa, na prática, depender de um
executável do Windows. Isso tem uma consequência que não é óbvia e que atinge o modelo de negócio: o
componente que assina **precisa rodar em Windows**. Se o Control Plane for para um contêiner Linux
numa nuvem — que é o destino natural de um SaaS no Caminho A —, a assinatura deixa de funcionar.

Descobrir isso na hora de empacotar o SaaS seria caro. É decisão de arquitetura, não de implementação.

## Decisão

**A assinatura é feita invocando `rdpsign.exe`, isolada atrás da interface `IRdpFileSigner`, e o
Control Plane é oficialmente um componente Windows nesta fase.**

1. `IRdpFileSigner` tem uma única responsabilidade: receber o conteúdo do `.rdp` e devolver o conteúdo
   assinado. Nenhuma outra parte do Control Plane sabe como a assinatura acontece.
2. A implementação do MVP-0 (`RdpSignExeSigner`) invoca `rdpsign.exe` em um processo separado, com
   tempo limite, e trata falha como **erro de lançamento** — nunca entrega `.rdp` sem assinatura
   (RNF-002). Não existe caminho de degradação para "entregar sem assinar".
3. O **certificado de assinatura** fica no repositório de certificados da máquina, com chave privada
   **não exportável**, e a conta de serviço do Control Plane tem permissão apenas de uso (RNF-008).
   A impressão digital é distribuída às estações por GPO.
4. **Consequência assumida:** o Control Plane roda em Windows enquanto esta decisão valer. Contêiner
   Linux está fora.
5. **Caminho de saída registrado desde já:** se o Caminho A exigir Control Plane em Linux, a saída não
   é reimplementar a assinatura, e sim **isolar a assinatura em um serviço Windows mínimo** —
   possivelmente o próprio Agent, que já roda no host RDS — chamado pelo Control Plane. Isso exigirá
   ADR novo, mas a interface do item 1 é o que torna essa mudança barata.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| **Implementar a assinatura em C# puro** | O formato não é publicado como especificação estável. Errar a implementação produz assinatura que o cliente rejeita — ou, pior, que ele aceita hoje e rejeita depois de uma atualização do Windows. Risco desproporcional. |
| **Não assinar e confiar no TLS do canal de entrega** | Violação direta de RP-06 e RNF-002. TLS protege o transporte; a assinatura protege o arquivo depois que ele chega ao disco do usuário — que é exatamente onde ele fica exposto. |
| **Assinar apenas o "essencial" e deixar parte do `.rdp` fora da assinatura** | Assinatura parcial é pior que assinatura nenhuma, porque cria falsa confiança. |
| **Entregar `.rdp` estáticos pré-assinados por aplicativo** | Elimina a dependência do `rdpsign` em tempo de execução, mas contraria RF-018 (arquivo específico por usuário e lançamento) e destrói a autorização por lançamento. |
| **Assinar sob demanda em serviço Windows separado já no MVP-0** | É o desenho correto para o Caminho A, mas adiciona um processo e um canal a mais ao MVP-0 sem benefício imediato. Fica registrado como caminho de saída, não como escolha inicial. |

## Consequências

**Positivas**
- Usa o mecanismo suportado pela Microsoft, com comportamento previsível nas estações.
- A interface isola a dependência: trocar a estratégia depois é trocar uma classe.
- Falha de assinatura vira falha de lançamento, o que mantém RNF-002 como propriedade estrutural.

**Negativas**
- Invocar processo externo por lançamento tem custo e é ponto de falha (tempo limite, permissão,
  perfil da conta de serviço). Precisa de tratamento e de métrica própria (RNF-029 mede exatamente
  esse caminho).
- Prende o Control Plane ao Windows nesta fase, restringindo opções de hospedagem e de custo.

**Riscos**
- **Desempenho:** `rdpsign` é um processo por lançamento. Sob 500 usuários (RNF-026), criação de
  processo pode virar gargalo. Mitigação: medir cedo (PRE-12); se necessário, serializar em pool ou
  migrar para o serviço separado do item 5.
- **Comprometimento do certificado de assinatura** permitiria forjar `.rdp` confiável para todas as
  estações. É o material mais sensível do MVP-0 depois das credenciais. Mitigação: chave não
  exportável, uso restrito à conta de serviço, e procedimento de rotação documentado em
  `SEGURANCA.md` (entregável 6).

## Requisitos relacionados

RF-018, RF-019 · RNF-002, RNF-004, RNF-008, RNF-026, RNF-029, RNF-035, RNF-037 · Origem: RP-06,
§2.2 prompt, Caminho A (§2.5)
