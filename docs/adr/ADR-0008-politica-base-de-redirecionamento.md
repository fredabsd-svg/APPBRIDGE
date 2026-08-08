# ADR-0008 — Política base de redirecionamento de periféricos e área de transferência
Data: 2026-08-08 · Status: **aceito** · Autor: Arquiteto de Software Principal (decisão delegada por Frederico em 2026-08-08)

## Contexto

RNF-014 estabeleceu "negar por padrão" para redirecionamento de recursos entre a estação e a sessão,
e colocou a gestão dessas políticas em V2 (RF-048, pelo painel). Ficou uma lacuna prática: **o MVP-0
precisa de uma política concreta desde o primeiro lançamento**, e "negar tudo" impediria o trabalho —
contador imprime, e o certificado A3 vive num token USB na mesa do usuário.

Sem decisão explícita, o resultado previsível é a configuração ir sendo afrouxada no improviso, item a
item, sem registro — que é como se chega a um session host com unidades locais e área de transferência
liberadas para todos e ninguém sabendo por quê.

Questão 5 de `REQUISITOS.md` §7.

## Decisão

**Fica definida uma política base, aplicada por GPO nos session hosts desde o MVP-0.** Negar por padrão
continua sendo a regra; o que este ADR faz é declarar as exceções conscientes e o motivo de cada uma.

| Recurso | MVP-0 | Justificativa |
|---|---|---|
| **Impressora local** (Easy Print) | **Permitido** | Contador imprime guia, relatório e balancete. Negar inviabiliza o trabalho e derruba a adoção (VP-02, CS-01). Risco baixo: fluxo de saída, sem execução. |
| **Token/smart card USB (A3)** | **Permitido** | É o meio pelo qual o usuário assina hoje. Tratar A3 como cidadão de primeira classe é diferencial declarado (DIF-01). Restringir por aplicativo quando o cofre chegar (V2). |
| **Área de transferência** | **Permitido, bidirecional** | Copiar dado entre planilha local e sistema contábil é rotina diária. Negar geraria contorno pior (usuário mandando arquivo por e-mail ou WhatsApp). Risco de exfiltração reconhecido e aceito no MVP-0. |
| **Unidades locais (drive redirection)** | **Negado** | É o principal caminho de exfiltração em massa e de entrada de arquivo malicioso no session host. A troca de arquivos deve ocorrer por área de rede controlada, não por mapeamento de disco da estação. |
| **Portas COM/LPT** | **Negado** | Sem caso de uso conhecido no inventário. Liberar por exceção nominal, se aparecer. |
| **Áudio (entrada e saída)** | **Saída permitida, entrada negada** | Saída resolve alerta sonoro de sistema; entrada de microfone não tem caso de uso e é sensível. |
| **Plug and play genérico (outros USB)** | **Negado** | Scanner e leitor de código de barras, se necessários, entram por exceção nominal por aplicativo, com registro. |

Condições que fazem parte da decisão:

1. A política base vale **por tenant** e é aplicada por GPO na OU do tenant (ADR-0004).
2. **Toda exceção é nominal, documentada e registrada** — aplicativo, recurso, motivo, quem autorizou.
   A partir do MVP-1, a autorização de exceção entra na trilha administrativa (RNF-017).
3. Quando RF-048 (gestão de políticas pelo painel) chegar na V2, ele **administra esta mesma política
   base**; não a substitui por outra concepção.
4. Área de transferência e impressora ficam **sob revisão obrigatória antes do piloto**: no dogfood, o
   dado é do próprio escritório; no Caminho B, é dado de terceiros sob responsabilidade do provedor,
   e a análise de risco muda.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| **Negar tudo no MVP-0 e liberar sob demanda** | Puro no papel, inviável na prática: o primeiro dia de dogfood terminaria com o usuário sem imprimir e sem assinar. Contraria CS-01. |
| **Liberar tudo e restringir depois** | Restrição posterior é politicamente muito mais difícil que liberação posterior. Uma vez que o usuário mapeia o disco local, tirar isso vira conflito. |
| **Negar área de transferência** | Defensável em ambiente de alta segurança, mas aqui geraria contorno pior por fora do sistema, com menos controle e nenhum rastro. |
| **Permitir unidades locais só de leitura** | O RDS não oferece esse controle com a granularidade necessária, e o caminho de entrada de arquivo no host continuaria aberto. |

## Consequências

**Positivas**
- O MVP-0 nasce com política escrita, aplicável por GPO e auditável, em vez de configuração ad hoc.
- As duas liberações de maior risco (área de transferência e impressora) estão declaradas como
  **decisão consciente com data de revisão**, não como descuido.

**Negativas**
- Negar unidades locais vai gerar atrito real no dogfood — haverá pedido para reverter. A resposta
  precisa ser um caminho alternativo de troca de arquivos, definido na implantação, não a reversão.
- Exceções nominais só entram na trilha administrativa no MVP-1; entre o MVP-0 e lá, o controle é
  documental.

**Riscos**
- **Área de transferência liberada é exfiltração possível**, sem rastro. É o ponto mais frágil desta
  decisão e está registrado como tal. Revisão obrigatória antes do piloto (condição 4). Deve constar
  como pendência conhecida em `SEGURANCA.md` (entregável 6).
- Redirecionamento de token A3 tem histórico de instabilidade em RDS, dependente de driver do
  fabricante. `PREMISSA:` (PRE-20) os tokens usados pelo escritório funcionam redirecionados —
  **precisa de teste prático no dogfood antes de virar promessa comercial**.

## Requisitos relacionados

**Alterados por este ADR:** RNF-014 (passa de V2 para MVP-0, com a política base definida aqui).
**Relacionados:** RF-048, RF-060, RNF-006, RNF-017, RNF-023 · Origem: `REQUISITOS.md` §7 questão 5,
RP-06, DIF-01, CS-01
