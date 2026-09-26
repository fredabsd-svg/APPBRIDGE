# ADR-0023 — Troca de sessão por access token do Entra, recente e de uso único
Data: 2026-09-26 · Status: **aceito** (decisão delegada por Frederico em 2026-09-26, S020) · Substitui: **ADR-0017 §1** · Autor: Arquiteto de Software Principal

## Contexto

O ADR-0017 §1 manda o `POST /v1/auth/session` validar "o token OIDC", e o launcher envia o **ID token**
do Entra. A revisão de código da S018 (RC-05, R-035) apontou três problemas:

- **O ID token não foi feito para chamar API.** A audiência dele é o próprio launcher, não o AppBridge.
  Ele também não diz a que API o usuário consentiu acesso.
- **Nada prende o token ao cliente que o pediu.** Qualquer aplicativo do mesmo tenant Entra que obtenha
  um token com a mesma audiência pode trocá-lo.
- **O token vira sessão longa.** Um token capturado (em log, proxy ou dump de memória) vale cerca de 1 h
  no Entra. Trocado, vira sessão renovável de até 30 dias (ADR-0020), e pode ser trocado quantas vezes
  se queira dentro dessa hora.

O launcher já pede ao Entra o escopo delegado da API (`APPBRIDGE_ENTRA_SCOPE`), então o access token
certo já existe e só não era usado.

## Decisão

1. **A troca aceita somente o access token do Entra emitido para a API do AppBridge.** Além de
   assinatura, emissor e validade (ADR-0017), o Control Plane exige:
   - `aud` igual a `IdentityProvider:Audience`, que passa a ser o identificador da **API** e não o do
     launcher;
   - `scp` contendo `IdentityProvider:RequiredScope` (padrão `access_as_user`);
   - `azp` igual a `IdentityProvider:ClientApplicationId`, o launcher registrado.

   O ID token deixa de ser aceito, porque tem outra audiência e não tem `scp`.
2. **O token tem de ser recente.** `iat` pode ter no máximo `IdentityProvider:MaxTokenAgeMinutes`
   (padrão 10 min, aceito de 1 a 60). Isso encurta a janela útil de um token capturado de 1 h para
   10 min.
3. **Cada token só é trocado uma vez.** O Control Plane grava o SHA-256 do identificador do token
   (`uti`, ou `jti` quando não houver `uti`) em `identity_token_redemption`, por tenant, até o
   vencimento do token. A segunda apresentação responde `401 INVALID_IDENTITY_TOKEN` e grava
   `access_event` de autenticação com falha `IDENTITY_TOKEN_REPLAYED`. Token sem identificador é recusado.
4. O contrato do corpo não muda de nome: o campo `identityToken` de `POST /v1/auth/session` passa a
   carregar o access token. A mudança de nome do campo fica para a próxima versão da API, porque não
   compensa quebrar o contrato `/v1` só pela palavra.
5. O metadado OpenID do Entra passa a ser cacheado por processo (RC-02), e não por requisição, o que
   evita um download a cada login anônimo.

## Alternativas consideradas

- **Manter o ID token e só acrescentar o uso único.** Isso resolve a repetição, mas mantém um token sem
  consentimento de API e aceito de qualquer cliente com a mesma audiência. Rejeitada.
- **Prova de posse (DPoP ou token binding).** Resolveria a captura, mas o Entra não emite access token
  DPoP para public client desktop de forma geral, e o MSAL .NET não oferece isso no fluxo interativo.
  Adiada para a V2, junto com RD Gateway e MFA.
- **Reduzir a sessão do ADR-0020.** Atacaria o sintoma e pioraria o uso diário. Rejeitada.

## Consequências

**Positivas:** o token trocado passa a ser do AppBridge, pedido pelo launcher e com consentimento do
usuário. Um token capturado vale no máximo 10 minutos e uma única vez. A tentativa de repetição fica na
trilha.

**Negativas:** o registro no Entra precisa expor a API com o escopo `access_as_user` e autorizar o
launcher como cliente. `IdentityProvider:Audience` muda de valor na implantação: quem usava o client ID
do launcher passa a usar o da API. É uma tabela nova, podada diariamente.

**Riscos:** relógio do Control Plane muito adiantado recusa tokens recém-emitidos; a tolerância de
1 min do ADR-0017 continua valendo. Se o launcher repetir o `POST /v1/auth/session` depois de perder a
resposta, o segundo envio é recusado e o usuário precisa entrar de novo. Isso é raro e visível, e vale
mais que aceitar repetição. A captura antes do primeiro uso continua possível. Esse é o resíduo do R-035,
agora limitado a 10 minutos.

## Requisitos relacionados (RF/RNF)

RF-001, RF-002, RF-004, RNF-004, RNF-036 · ADR-0001, ADR-0017, ADR-0020 · R-035 · RC-02, RC-05
