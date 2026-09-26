# Desenvolvimento e operação local — Control Plane e Launcher

Este roteiro prepara os serviços necessários para exercitar o caminho MVP-0a em ambiente de
desenvolvimento. A execução real de RemoteApp requer Windows Server 2025 com RDS, Connection Broker,
certificado de assinatura e uma estação Windows registrada no Entra; um container local não substitui
esses componentes (ADR-0002, ADR-0009, ADR-0010).

## 1. Ferramentas

- .NET SDK 10.0.401 (fixado em `global.json`; SDKs 10.0 posteriores são aceitos).
- PostgreSQL 18 ou compatível.
- No Control Plane Windows: `rdpsign.exe`, certificado SHA-256 com chave privada no repositório de
  certificados da máquina, e PowerShell com o módulo `RemoteDesktop` quando o Connection Broker for
  consultado.
- Na estação do usuário: Windows, `mstsc.exe`, registro de aplicativo Entra do tipo public client e
  acesso HTTPS ao Control Plane.

## 2. PostgreSQL e migrações

Crie um banco e uma conta de serviço com permissões limitadas ao banco AppBridge. Configure a
connection string como segredo de ambiente ou do serviço:

```text
ConnectionStrings__AppBridge=Host=127.0.0.1;Port=5432;Database=appbridge;Username=appbridge;Password=<segredo>
```

Restaure a ferramenta local e aplique as migrações:

```sh
dotnet tool restore
dotnet ef database update --project src/AppBridge.ControlPlane
```

O manifesto local fixa `dotnet-ef` na mesma versão principal do EF Core. Para gerar migrações, use
`dotnet ef migrations add NomeDaMigracao --project src/AppBridge.ControlPlane` e revise o SQL antes
de aplicar em um ambiente compartilhado.

## 3. Identidade e token do Control Plane

No Entra são dois registros (ADR-0023):

- **A API do AppBridge**, que expõe o escopo delegado `access_as_user`. O identificador dela (client ID
  ou App ID URI, conforme o `aud` que o Entra emitir) é `IdentityProvider__Audience`.
- **O launcher**, como desktop/public client com permissão e consentimento para esse escopo. O client ID
  dele é `IdentityProvider__ClientApplicationId`.

Configure a API para emitir access token v2 (`accessTokenAcceptedVersion: 2`), que traz `azp` e `uti`.
O Control Plane só aceita o access token dessa API pedido pelo launcher, emitido há no máximo 10 minutos
e ainda não trocado. Se `ClientApplicationId` não estiver configurado, a troca responde `503`. Configure no
ambiente do Control Plane:

```text
IdentityProvider__Authority=https://login.microsoftonline.com/<entra-tenant-id>/v2.0
IdentityProvider__Audience=<client-id-ou-app-id-uri-da-api>
IdentityProvider__ClientApplicationId=<client-id-do-launcher>
IdentityProvider__RequiredScope=access_as_user
IdentityProvider__MaxTokenAgeMinutes=10
IdentityProvider__TenantMappings__<entra-tenant-id>=<tenant-id-interno>
ControlPlaneTokens__Issuer=https://appbridge.exemplo.interno
ControlPlaneTokens__Audience=appbridge-launcher
ControlPlaneTokens__SigningKey=<chave-base64-com-pelo-menos-32-bytes>
AuthenticationSessions__IdleLifetimeDays=7
AuthenticationSessions__MaximumSessionLifetimeDays=30
```

Gere uma chave criptograficamente aleatória de pelo menos 32 bytes e forneça-a por cofre ou variável
de ambiente. Não a grave neste repositório. Em desenvolvimento, o Control Plane gera uma chave
temporária em memória se `ControlPlaneTokens__SigningKey` estiver ausente; tokens deixam de valer ao
reiniciar.

O access token dura 30 minutos por padrão. O refresh token renova por até 7 dias sem uso, limitado a
30 dias desde o login; os dois prazos da sessão aceitam configuração de 1 a 90 dias. O refresh é
rotacionado a cada uso, e o banco guarda somente hashes. Não configure valores acima do limite
operacional documentado no ADR-0020. A estação envia o refresh token somente por TLS.

No Windows, o launcher guarda access/refresh tokens em uma credencial genérica local do usuário do
Credential Manager; não são sincronizados entre estações. No console do launcher, escolha `0` para
sair da conta, ou execute `AppBridge.Launcher.exe --logout`. As duas formas tentam revogar a sessão
remota e removem a credencial local. Se o Control Plane estiver inacessível, a sessão local é apagada,
mas a revogação remota não acontece: o refresh grant no servidor continua utilizável até expirar
(até 7 dias ocioso, no máximo 30 dias). O launcher retorna erro para destacar esse estado. Depois de implantar T-303,
usuários com token emitido pela versão anterior devem entrar novamente: o JWT antigo não contém `sid`.

`IdentityProvider__TenantMappings` associa o claim `tid` validado ao UUID interno. Tenant, contas,
grupos, associações, permissões, pools, hosts e certificado de assinatura precisam ser provisionados
por procedimento operacional aprovado antes de login e lançamento. Não há API administrativa de
provisionamento no MVP-0a. O seed do catálogo pressupõe que o tenant e o pool já existem.

## 4. Certificado, RDS e catálogo

Cadastre em `signing_certificate` apenas metadados do certificado ativo (tenant, thumbprint SHA-256,
subject, validade e status). A chave privada deve permanecer não exportável no certificado local da
máquina do Control Plane, acessível à conta de serviço de menor privilégio. Distribua a impressão
digital às estações por GPO, conforme ADR-0009.

O backend de lançamento invoca `rdpsign.exe /sha256 <thumbprint> /q <arquivo.rdp>`. O `SessionRegistry`
reutiliza a sessão aberta do usuário no pool; sem ela, a seleção usa hosts `online` do pool e a
capacidade cadastrada. Cada lançamento concedido sem sessão registra uma sessão com vínculo pendente,
que ocupa vaga por `SessionRegistry__PendingBindingMinutes` minutos (padrão 10, aceito de 1 a 60,
PRE-29) até a reconciliação vinculá-la.

A reconciliação (T-602, ADR-0022) consulta o Connection Broker a cada `SessionReconciler__IntervalSeconds`
(padrão 60, aceito de 15 a 3600). Configure `RdsSession__ConnectionBroker=<fqdn-do-broker>`. A conta de
serviço precisa ler sessões no broker e traduzir contas do domínio em SID. Sem broker configurado ou
alcançável, o ciclo só fecha como `stale_expired` as sessões sem sinal há mais de
`SessionReconciler__StaleAfterMinutes` (padrão 30, aceito de 5 a 1440). Em desenvolvimento, fora do
Windows, esse é o comportamento esperado e o log registra um aviso por ciclo. `CancelSessionAsync` encerra sessões conhecidas
via `Invoke-RDUserLogoff`; esse caminho precisa de Connection Broker e módulo RemoteDesktop na máquina
Windows do Control Plane.

Edite `src/AppBridge.ControlPlane/data/catalog.seed.json` ou configure `CatalogSeed__Path` para outro
arquivo JSON. O formato raiz é `tenants[]`; cada tenant tem `tenantId` e `applications[]` com `id`,
`displayName`, `iconRef` (opcional), `remoteAppAlias`, `hostPoolId`, `launchMode` e `status`. O arquivo
incluído tem uma lista vazia, portanto não publica aplicativos por padrão.

Coloque os ícones PNG numa raiz de arquivos fora do banco. Por padrão, `icon_ref` é relativo a
`src/AppBridge.ControlPlane/data/icons`; sobrescreva a raiz com `CatalogAssets__RootPath` (caminho
absoluto ou relativo ao content root do Control Plane). Por exemplo, `accounting/domain.png` referencia
`<raiz>/accounting/domain.png`. O serviço recusa caminhos absolutos, `..`, links simbólicos, extensão
que não seja `.png`, arquivos sem a assinatura PNG correta e arquivos maiores que 1 MiB. O endpoint
confirma a permissão vigente antes de ler o arquivo; por isso, não publique os ícones por um servidor
estático sem autenticação. Em instalação com mais de uma instância do Control Plane, configure a mesma
raiz compartilhada e mantenha os arquivos iguais em todas as instâncias.

## 5. Iniciar localmente

Restaure e inicie o Control Plane. O perfil de desenvolvimento usa `127.0.0.1:5080` e gera chave de
token efêmera:

```sh
dotnet run --project src/AppBridge.ControlPlane
curl -fsS http://127.0.0.1:5080/health
```

Para o launcher, configure na estação:

```text
APPBRIDGE_API_BASE_URL=https://control-plane.exemplo.interno/
APPBRIDGE_ENTRA_CLIENT_ID=<client-id-guid>
APPBRIDGE_ENTRA_AUTHORITY=https://login.microsoftonline.com/organizations
APPBRIDGE_ENTRA_SCOPE=api://<api-client-id-guid>/access_as_user
```

`APPBRIDGE_ENTRA_AUTHORITY` pode apontar para um tenant específico. A API aceita HTTPS; HTTP é
permitido apenas quando o endereço é localhost. Configure no registro Entra um escopo delegado de API
e conceda consentimento ao public client; `APPBRIDGE_ENTRA_SCOPE` deve nomear exatamente esse
escopo. O launcher abre autenticação interativa no navegador,
troca o token com `POST /v1/auth/session`, lista apenas aplicativos autorizados e abre o selecionado
com `mstsc.exe`. O `.rdp` temporário é removido quando vence o TTL do servidor ou quando o processo
recebe interrupção graciosa. Desde a T-303 (S016), a sessão fica no Credential Manager e é renovada sem
novo login interativo; veja a seção 3.

## 6. Build e testes

```sh
dotnet build src/AppBridge.ControlPlane/AppBridge.ControlPlane.csproj --configuration Release
dotnet build src/AppBridge.Launcher/AppBridge.Launcher.csproj --configuration Release
ConnectionStrings__AppBridgeTest='Host=127.0.0.1;Port=5432;Database=appbridge_test;Username=appbridge;Password=<segredo>' \
  dotnet test tests/AppBridge.ControlPlane.Tests/AppBridge.ControlPlane.Tests.csproj --configuration Release \
    --coverlet --coverlet-output-format cobertura \
    --coverlet-exclude-by-file '**/Program.cs' --coverlet-exclude-by-file '**/Migrations/*.cs'
python3 scripts/check-coverage.py 80
```

A suíte PostgreSQL aplica as migrações na fixture. O limiar RP-08 conta linhas de código do Control
Plane, excluindo a composição de inicialização (`Program.cs`) e código gerado de migração. Para usar o
launcher de verdade, rode-o em uma estação Windows com cliente Entra configurado, catálogo e
permissões provisionados, Control Plane Windows conectado ao RDS, e estação autorizada a alcançar o
Session Host pela rede privada. A API não expõe 3389 e o Control Plane não fornece fallback de `.rdp`
sem assinatura.
