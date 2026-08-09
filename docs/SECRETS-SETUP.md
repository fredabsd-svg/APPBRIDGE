# Configuração de Secrets — AppBridge

**Status:** Template para desenvolvimento local  
**Segurança:** ⚠️ Nunca commit secrets; use `dotnet user-secrets` para desenvolvimento  
**CI/CD:** GitHub Actions usa repository secrets (não documentados aqui)

---

## 1. Visão Geral

O AppBridge usa três camadas de secrets:

| Ambiente | Armazenamento | Escopo | Quem acessa |
|----------|---------------|--------|------------|
| **Development** | `~/.microsoft/usersecrets/` (local) | Máquina do dev | Apenas dev |
| **Staging** | Key Vault (Azure) ou variáveis de CI | Servidor de teste | Serviço AppBridge |
| **Production** | Key Vault (Azure) ou HSM | Servidor de produção | Serviço AppBridge |

**Regra:** Nenhum secret em `.git`, `appsettings.json`, variáveis de ambiente ou logs.

---

## 2. Configuração no Development

### 2.1 Inicializar User-Secrets

```bash
cd src/AppBridge.ControlPlane
dotnet user-secrets init
```

Isso cria `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json` onde `<UserSecretsId>` vem do `.csproj`.

### 2.2 Configurar Secrets por Categoria

#### Conta administrativa do Control Plane

```bash
dotnet user-secrets set "AdminAccount:Username" "svc-appbridge"
dotnet user-secrets set "AdminAccount:Password" "senha-super-secreta-min-16-chars"
```

**Nota:** A senha deve ter ≥ 16 caracteres, incluir maiúsculas, números e símbolos.

#### Conexão com banco de dados

```bash
dotnet user-secrets set "Database:ConnectionString" \
  "Host=localhost;Port=5432;Database=appbridge_dev;Username=postgres;Password=postgres"
```

Ou, se usar Docker PostgreSQL:

```bash
dotnet user-secrets set "Database:ConnectionString" \
  "Host=host.docker.internal;Port=5432;Database=appbridge_dev;Username=postgres;Password=postgres"
```

#### Autenticação Entra ID / Azure AD

```bash
dotnet user-secrets set "Authentication:AzureAd:TenantId" "sua-tenant-id-aqui"
dotnet user-secrets set "Authentication:AzureAd:ClientId" "seu-client-id-aqui"
dotnet user-secrets set "Authentication:AzureAd:ClientSecret" "seu-client-secret-aqui"
```

**Onde encontrar?**
- Entra ID > Aplicativos > Seu app (AppBridge Launcher)
- Copiar **Tenant ID** (Overview) e **Client ID** (Overview)
- Ir a **Certificados e segredos** > Segredos do cliente > copiar valor

#### Assinatura de arquivos RDP

```bash
# Caminho local para certificado (apenas desenvolvimento)
dotnet user-secrets set "RdpSigning:CertificatePath" \
  "$HOME/certs/appbridge-signing.pfx"

# Senha do certificado (64 caracteres aleatórios para produção)
dotnet user-secrets set "RdpSigning:CertificatePassword" \
  "sua-senha-certificado-aqui"
```

**Gerar certificado auto-assinado para dev:**

```bash
# macOS / Linux
openssl req -x509 -newkey rsa:2048 -keyout key.pem -out cert.pem -days 365 -nodes
openssl pkcs12 -export -out appbridge-signing.pfx -inkey key.pem -in cert.pem

# Windows PowerShell
$cert = New-SelfSignedCertificate -CertStoreLocation "cert:\CurrentUser\My" `
  -Subject "CN=AppBridge RDP Signing" -KeyUsage DigitalSignature
Export-PfxCertificate -Cert $cert -FilePath "$HOME\certs\appbridge-signing.pfx" `
  -Password (ConvertTo-SecureString -String "sua-senha" -AsPlainText -Force)
```

#### JWT (opcional para MVP-0)

```bash
dotnet user-secrets set "JwtSigningKey" "sua-chave-simetrica-256bits-base64-aqui"
dotnet user-secrets set "JwtIssuer" "https://localhost:5001"
dotnet user-secrets set "JwtAudience" "appbridge-launcher"
```

**Gerar chave JWT (256 bits = 32 bytes base64):**

```bash
# macOS / Linux
openssl rand -base64 32

# Windows PowerShell
[Convert]::ToBase64String((1..32 | ForEach-Object {[byte](Get-Random -Maximum 256)}))
```

#### Logging e observabilidade (opcional)

```bash
dotnet user-secrets set "Logging:LogLevel:Default" "Information"
dotnet user-secrets set "Logging:LogLevel:Microsoft" "Warning"
dotnet user-secrets set "Serilog:MinimumLevel" "Information"
```

### 2.3 Verificar Secrets Carregados

```bash
dotnet user-secrets list
```

Deve exibir todas as chaves (não os valores).

### 2.4 Listar Secrets de um Projeto Específico

```bash
cd src/AppBridge.ControlPlane.Api
dotnet user-secrets list
```

---

## 3. Acesso aos Secrets na Aplicação

### 3.1 Via `appsettings.json` (não contém valores reais)

```json
{
  "Database": {
    "ConnectionString": "Host=localhost;Port=5432;Database=appbridge_dev;Username=postgres;Password=***"
  },
  "Authentication": {
    "AzureAd": {
      "TenantId": "***",
      "ClientId": "***"
    }
  }
}
```

### 3.2 Via `Program.cs` (ASP.NET Core)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Automaticamente carrega secrets do user-secrets em Development
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// Acessar um secret
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var azureAdOptions = builder.Configuration.GetSection("Authentication:AzureAd");

// Com strongly-typed options
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection("Database")
);
```

### 3.3 Injetar via Options Pattern

```csharp
public class DatabaseOptions
{
    public string ConnectionString { get; set; }
    public int CommandTimeout { get; set; }
    public int PoolSize { get; set; }
}

public class MyRepository
{
    private readonly IOptions<DatabaseOptions> _options;

    public MyRepository(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
    }

    public async Task GetData()
    {
        using var conn = new NpgsqlConnection(_options.ConnectionString);
        // ...
    }
}
```

---

## 4. Ciclo de Vida: Desenvolvimento → Staging → Produção

### 4.1 Desenvolvimento (Local)
- Secrets: `~/.microsoft/usersecrets/`
- Validação: `dotnet user-secrets list`
- Ciclo: Edit → Test → Commit (sem secrets)

### 4.2 Staging (CI/CD GitHub Actions)

```yaml
# .github/workflows/deploy-staging.yml
jobs:
  deploy-staging:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Set up .NET
        uses: actions/setup-dotnet@v4
      - name: Restore
        run: dotnet restore src/
      - name: Build
        env:
          Database__ConnectionString: ${{ secrets.STAGING_DB_CONNECTION }}
          Authentication__AzureAd__TenantId: ${{ secrets.STAGING_AZURE_TENANT }}
        run: dotnet build src/ --configuration Release
      - name: Deploy
        run: ./scripts/deploy-staging.sh
```

### 4.3 Produção (Key Vault)

Certificado de assinatura RDP:
- Armazenado em **Azure Key Vault** (não é exportável)
- Acesso: Apenas identidade gerenciada do App Service / Container
- Auditoria: Key Vault audit logs (quem acessou, quando, para quê)

---

## 5. Operações Comuns

### Resetar Secrets (Development)

```bash
cd src/AppBridge.ControlPlane
dotnet user-secrets clear
dotnet user-secrets init
# Reconfigurar manualmente ou via script
```

### Migrar Secrets Entre Máquinas

```bash
# Máquina A: Exportar (não é recomendado para produção)
cat ~/.microsoft/usersecrets/<UserSecretsId>/secrets.json

# Máquina B: Importar
mkdir -p ~/.microsoft/usersecrets/<UserSecretsId>/
# Colar conteúdo do secrets.json
```

**Melhor prática:** Use um Key Vault compartilhado (Azure Key Vault, Hashicorp Vault, etc).

### Validar Secrets no CI

```bash
# Testar se a aplicação consegue acessar secrets
dotnet run --project src/AppBridge.ControlPlane.Api -- \
  --validate-secrets
```

---

## 6. Checklist de Segurança

- ✅ Nenhum secret em `.git` ou `appsettings.json`
- ✅ Secrets em variáveis de ambiente ou Key Vault apenas
- ✅ `.gitignore.local` bloqueia `user-secrets/`, `.env`, `*.pfx`, `*.key`
- ✅ Detect-secrets valida cada push (`.secrets.baseline`)
- ✅ Certificado RDP é auto-assinado em Dev, gerenciado em Prod
- ✅ Senhas ≥ 16 caracteres; chaves criptográficas ≥ 256 bits
- ✅ Logs nunca contêm secrets (regex de redação em appender)
- ✅ Rotação de secrets planejada (Azure Key Vault rotation policies)

---

## 7. Troubleshooting

### "Secret 'X' not found"
```bash
# Verificar se foi inicializado
ls ~/.microsoft/usersecrets/*/secrets.json

# Reinicializar
cd src/AppBridge.ControlPlane
dotnet user-secrets init
dotnet user-secrets set "ChaveAusente" "valor"
```

### "Connection refused" ao banco
```bash
# Verificar string de conexão
dotnet user-secrets list | grep Database

# Testar conexão
psql "Host=localhost;Port=5432;Database=appbridge_dev;Username=postgres"
```

### Certificado não encontrado
```bash
# Verificar caminho
dotnet user-secrets list | grep RdpSigning

# Testar se arquivo existe
ls -la $(dotnet user-secrets get "RdpSigning:CertificatePath")
```

---

## 8. Referências

- Microsoft Docs: [Safe storage of app secrets](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)
- Azure Key Vault: [Overview](https://docs.microsoft.com/en-us/azure/key-vault/general/)
- OWASP: [Secrets Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html)

---

**Última atualização:** 2026-08-09  
**Status:** Pronto para implementação
