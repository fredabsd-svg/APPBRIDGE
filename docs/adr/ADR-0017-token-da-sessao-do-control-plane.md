# ADR-0017 — Token de sessão do Control Plane
Data: 2026-09-24 · Status: **aceito** · Autor: Arquiteto de Software Principal

## Contexto

O ADR-0001 e o ADR-0012 definem a troca do token do provedor de identidade por um token próprio do
AppBridge. A implementação precisa validar integralmente o token externo, vincular a identidade a
uma conta AD já provisionada e assinar o token usado nas chamadas seguintes. Nenhum segredo pode ser
versionado ou aparecer em logs (RP-06).

## Decisão

1. O `POST /v1/auth/session` valida o token OIDC usando metadados e chaves publicados pela autoridade
   configurada, conferindo emissor, audiência, assinatura, validade e expiração.
2. A associação entre o `tid` validado do Entra e o `tenant.id` interno é configuração do servidor.
   O cliente não escolhe nem envia o tenant AppBridge.
3. O Control Plane emite JWT com `HS256`, usando chave aleatória de pelo menos 256 bits fornecida por
   variável de ambiente ou cofre. Em produção, o serviço não inicia sem essa chave. Em ambiente de
   desenvolvimento, uma chave aleatória em memória permite execução local e invalida tokens após
   reinício.
4. O JWT contém o identificador da conta em `sub`, `tenant_id`, nome, papel `user`, `jti`, `iat` e
   `exp`. O padrão de validade é 30 minutos, configurável até 24 horas. Cada requisição valida
   assinatura, emissor, audiência e expiração antes de vincular o `TenantContext`.
5. O MVP-0a não emite refresh token. Renovação, invalidação remota e guarda no Credential Manager
   continuam na T-303 (MVP-0b).

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Reutilizar o token de identidade | Mistura a audiência do Entra com a API interna e não permite reduzir os privilégios do token da sessão AppBridge. |
| JWT com chave assimétrica | Facilita validação por terceiros, mas o MVP-0a tem apenas o Control Plane como emissor e consumidor; adiciona uma infraestrutura de chave sem benefício atual. |
| Segredo fixo no código ou em arquivo versionado | Viola RP-06 e expõe a emissão de sessões a qualquer pessoa com acesso ao repositório. |

## Consequências

**Positivas:** a API valida token próprio com escopo e duração controlados; a chave e o vínculo de
tenant ficam fora do cliente e do repositório.

**Negativas:** todas as instâncias do Control Plane precisam compartilhar a mesma chave ativa.
Rotacioná-la invalida imediatamente os tokens emitidos com a chave anterior. Refresh token e logout
ainda não fazem parte do MVP-0a.

**Riscos:** a configuração inicial do Entra e da chave é pré-requisito externo. A implementação
precisa ser exercitada em Windows e com um tenant Entra antes do aceite ponta a ponta.

## Requisitos relacionados

RF-001..RF-004, RF-010, RNF-004, RNF-036, RNF-043 · ADR-0001, ADR-0012
