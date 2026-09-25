# ADR-0020 — Sessão renovável do launcher
Data: 2026-09-25 · Status: **aceito** · Autor: Arquiteto de Software Principal (decisão delegada na continuidade autorizada por Frederico)

## Contexto

O MVP-0a guarda o JWT do Control Plane somente na memória do processo. A T-303 precisa renovar a
sessão sem novo login interativo, guardar os tokens com proteção do Windows e encerrar a sessão pelo
launcher (RF-004..RF-006). O launcher é um cliente nativo público: não pode guardar segredo de cliente.
O contrato anterior não definiu formato, rotação ou validade do refresh token.

## Decisão

1. O access token continua sendo JWT HS256, com validade curta (30 minutos por padrão), e passa a
   conter `sid`, o ID da sessão persistida no Control Plane. Cada requisição autenticada confere que
   essa sessão existe, pertence ao tenant e usuário do token, não foi revogada e não expirou.
2. O Control Plane emite refresh token opaco com identificadores de tenant e sessão como localizador
   e um segredo aleatório de 256 bits. Esses IDs não autorizam a operação: somente o valor completo,
   cuja impressão SHA-256 coincide com um registro não consumido, pode renovar a sessão. O valor bruto
   nunca é persistido no servidor nem escrito em log.
3. Cada refresh consome o token apresentado e emite outro. Os hashes consumidos permanecem ligados à
   sessão até a limpeza da sessão, permitindo detectar reapresentação. Replay revoga a sessão inteira,
   incluindo o token atual, e é auditado; o serviço não tenta adivinhar qual cópia era legítima.
4. O refresh expira depois de 7 dias sem uso e toda sessão tem limite absoluto de 30 dias. Ambos os
   prazos são configuráveis no Control Plane, limitados a 1–90 dias, e o prazo ocioso não ultrapassa
   o limite absoluto. Após expiração, o usuário autentica novamente no provedor de identidade.
5. `POST /v1/auth/refresh` recebe o refresh token no corpo JSON, adequado ao cliente desktop que
   guarda seus tokens no Windows Credential Manager; não usa cookie de navegador. Transporte exige
   TLS conforme RNF-003.
6. `POST /v1/auth/logout` exige access token vigente, revoga a sessão identificada por `sid` e grava
   `access_event` na mesma transação. O launcher apaga a credencial local mesmo se o Control Plane
   estiver indisponível, mas informa que a revogação remota não foi confirmada. Logout confirmado
   invalida o AppBridge, mas não encerra a sessão RDS já aberta (RF-008).
7. O launcher persiste access token, refresh token e expiração juntos em uma credencial genérica do
   Windows Credential Manager com persistência local ao usuário. Em plataforma diferente de Windows,
   a implementação recusa persistir a credencial.
8. Sessões encerradas/expiradas e seus hashes são removidos após 30 dias, por serviço de manutenção;
   a trilha de auditoria continua sua retenção independente segundo ADR-0007.
9. Respostas de login e refresh incluem `Cache-Control: no-store` e `Pragma: no-cache`, para que
   intermediários não armazenem tokens ou credenciais (RFC 6749 §5.1).

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Refresh token reutilizável sem rotação | Não detecta cópia ou replay do token roubado de um cliente público. |
| Refresh token em cookie HTTP-only | Adequado a cliente web; o launcher nativo guarda tokens no Credential Manager e não depende de cookie de navegador. |
| Guardar token bruto no PostgreSQL | Uma cópia ou backup do banco passaria a permitir renovação de sessão. |
| JWT de acesso sem consulta ao estado de sessão | Não permite que logout invalide imediatamente um JWT ainda válido. |
| Revogar somente o refresh no logout | O access JWT continuaria aceito até expirar, contrariando o efeito esperado do logout. |

## Consequências

**Positivas:** tokens em repouso ficam protegidos pelo repositório do Windows; um refresh roubado tem
uso único; replay revoga o conjunto da sessão; logout invalida access tokens sem esperar o `exp`.

**Negativas:** cada chamada autenticada consulta a sessão no PostgreSQL. Duas renovações concorrentes
com o mesmo token são indistinguíveis de replay e encerram a sessão; o cliente deve serializar a
renovação. Se a resposta de uma renovação se perder depois do commit, a cópia antiga não pode ser
reutilizada e o usuário precisará autenticar novamente. Se logout ocorrer sem conexão com o Control
Plane, só a credencial local é apagada; o refresh grant remoto continua válido até expirar.

**Riscos:** a proteção do Credential Manager depende da conta Windows e da segurança da estação; um
processo executado como o próprio usuário ainda pode pedir acesso à credencial. Rotação, expiração e
revogação não encerram sessões RDS existentes. Estações que ainda tenham JWTs anteriores sem `sid`
precisarão obter uma nova sessão após a implantação.

## Requisitos relacionados

RF-001, RF-004..RF-006, RF-036, RNF-003, RNF-004, RNF-015, RNF-019, RNF-036, RNF-043 · ADR-0001,
ADR-0007, ADR-0010, ADR-0011, ADR-0012, ADR-0017 · [RFC 6749 §5.1](https://www.rfc-editor.org/rfc/rfc6749.html#section-5.1), [RFC 9700 §4.14.2](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14.2)
