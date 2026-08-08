# ADR-0006 — Antecipação do metering mínimo (DIF-02) de V2 para MVP-1
Data: 2026-08-08 · Status: **aceito** · Autor: Arquiteto de Software Principal (decisão delegada por Frederico em 2026-08-08)

## Contexto

`VISAO.md` §4.1 registrou o risco **R-004**: os três diferenciais estratégicos estão em V2 e V3, de
modo que MVP-0 e MVP-1 não se distinguem de um RDS bem configurado. O piloto do Caminho B está
previsto para o 1º trimestre de 2027 (P8) e concorre com hospedadores nacionais já estabelecidos e
com preço formado (RM-09). Entrar nessa disputa sem nenhum diferencial visível é entrar por preço —
exatamente o terreno onde um projeto novo perde.

Entre os três diferenciais, o metering (DIF-02) é o único que:

- depende apenas de dados que o Control Plane **já registra** no MVP-0 (RF-037, RF-038: lançamento e
  início/fim de sessão);
- **não** depende do Agent (V2), do cofre (V2) nem de snapshot/rollback (V3);
- resolve uma dor imediata e verificável do próprio dogfood (PR-03) — quantas licenças de Domínio ou
  Alterdata estão em uso agora;
- é **demonstrável em 30 segundos** numa reunião comercial, que é o que o piloto precisa.

## Decisão

**RF-062 (contagem em tempo real), RF-063 (teto por aplicativo e por tenant) e RF-064 (bloqueio de
lançamento ao atingir o teto) passam de V2 para MVP-1, com grau Must.**

Permanecem em V2: **RF-065** (fila de espera) e **RF-066** (relatório histórico de pico). A fila é a
parte cara — envolve notificação, expiração de reserva, ordem justa e disputa entre usuários — e não é
necessária para demonstrar valor: bloquear com mensagem clara já resolve o problema de conformidade
de licença.

Condição técnica que faz parte da decisão: **a contagem só é confiável se o encerramento de sessão for
detectado com confiabilidade.** Sem o Agent (V2), o Control Plane precisa obter esse dado consultando
periodicamente o Connection Broker do RDS. Essa consulta passa a ser parte do escopo do MVP-1 e deve
ficar **atrás da interface de backend de sessão** definida em RNF-035, para não amarrar a lógica de
metering ao RDS.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| **Manter tudo em V2** (situação original) | Deixa R-004 sem tratamento e leva o piloto ao mercado sem diferencial. Foi a alternativa recusada. |
| **Antecipar para o MVP-0** | O MVP-0 é dogfood interno com 10 usuários, onde estouro de licença não é problema real, e já concentra 34 requisitos Must em ~2 meses (R-007). Antecipar para lá aumentaria o risco de calendário sem ganho. |
| **Antecipar o DIF-01 (cofre de certificados)** | É o diferencial mais valioso e o mais desejado pelo público-alvo, mas depende de parecer jurídico (T-002, R-002) e de custódia de material sensível de terceiros. Antecipá-lo sem o termo revisado seria assumir risco jurídico para ganhar prazo — troca ruim. |
| **Antecipar o DIF-03 (orquestrador)** | Depende do Agent, de snapshot e de rollback. É o mais caro dos três e o menos demonstrável em reunião. |
| **Antecipar o metering completo, com fila** | A fila multiplica o esforço e introduz decisões de produto (quem tem prioridade? quanto tempo a vaga fica reservada?) que ninguém pediu ainda. |

## Consequências

**Positivas**
- O piloto passa a ter um diferencial verificável e específico, respondendo parcialmente a R-004 e a RM-09.
- Aproveita dado que já será coletado, com custo incremental modesto.
- Protege o provedor do Caminho B de inadimplência contratual com fornecedores de software — o que se
  conecta diretamente a T-001 e R-001.

**Negativas**
- Aumenta o escopo do MVP-1, que já contém painel administrativo, grupos, favoritos, atualização
  automática e reconexão robusta.
- Cria uma dependência de consulta ao Connection Broker que só será substituída pelo Agent na V2 —
  ou seja, um caminho de código que nasce sabendo que será trocado.

**Riscos**
- **Contagem incorreta é pior que contagem nenhuma.** Se o encerramento de sessão não for detectado
  com confiabilidade, o contador infla, o teto é atingido indevidamente e o AppBridge passa a impedir
  o trabalho do usuário. Mitigação obrigatória: expiração por inatividade no registro de sessão,
  reconciliação periódica com o Connection Broker e capacidade de o administrador zerar o contador.
- Risco de calendário do MVP-1 (R-007 se estende ao MVP-1). Se o prazo apertar, o corte deve sair de
  RF-017 (favoritos) e RF-045 (encerrar sessão pelo painel), não do metering.

## Requisitos relacionados

**Alterados por este ADR:** RF-062, RF-063, RF-064 (de V2 para MVP-1).
**Mantidos:** RF-065, RF-066 (V2).
**Dependências:** RF-037, RF-038, RNF-035, RNF-039 · Origem: R-004, DIF-02, PR-03, RM-09, CS-05
