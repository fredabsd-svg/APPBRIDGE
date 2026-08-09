# Setup de Desenvolvimento — AppBridge

## Pré-requisitos

- **.NET 8.0+** (ou mais recente)
- **PostgreSQL 14+** (local ou Docker)
- **Windows 10/11** (para compilar components Windows-specific)
- **Git**

## Passos Iniciais

### 1. Clone e Configure o Repositório

```bash
git clone https://github.com/fredabsd-svg/APPBRIDGE.git
cd APPBRIDGE
git checkout main  # ou sua feature branch
```

### 2. Instale as Ferramentas .NET

```bash
dotnet --version  # confirmar versão 8.0+
dotnet tool install -g dotnet-format
dotnet tool install -g dotnet-ef
```

### 3. Configure Secrets Locais (T-00.3)

Nenhum secret deve estar em `appsettings.json` ou código. Use `dotnet user-secrets` por ambiente.

#### Secrets do Development

```bash
cd src/AppBridge.ControlPlane

# Configure chaves necessárias
dotnet user-secrets init

dotnet user-secrets set "AdminAccount:Username" "svc-appbridge"
dotnet user-secrets set "AdminAccount:Password" "(será solicitado)"

dotnet user-secrets set "Database:ConnectionString" "Host=localhost;Port=5432;Database=appbridge_dev;Username=postgres;Password=..."

dotnet user-secrets set "AzureAd:TenantId" "(seu tenant ID)"
dotnet user-secrets set "AzureAd:ClientId" "(seu app ID)"
dotnet user-secrets set "AzureAd:ClientSecret" "(seu secret)"

dotnet user-secrets set "RdpSigning:CertificatePath" "/path/to/signing-cert.pfx"
dotnet user-secrets set "RdpSigning:CertificatePassword" "(64 chars aleatórios)"

# Verificar secrets carregados
dotnet user-secrets list
```

### 4. Configure o Banco de Dados

#### Via Docker (recomendado para dev)

```bash
docker run -d \
  --name appbridge-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=appbridge_dev \
  -p 5432:5432 \
  postgres:15-alpine
```

#### Via PostgreSQL Local

```bash
createdb appbridge_dev
psql -U postgres appbridge_dev < backup.sql  # se houver
```

### 5. Execute Migrations

```bash
cd src/AppBridge.ControlPlane

# Aplicar migrations
dotnet ef database update

# Ou seed data de desenvolvimento
dotnet ef database update -- --seed
```

### 6. Teste o Build

```bash
# Build da solução
dotnet build src/

# Rodar testes unitários
dotnet test tests/AppBridge.ControlPlane.Tests/

# Rodar testes de integração (exige DB)
dotnet test tests/AppBridge.ControlPlane.Tests/Integration/
```

### 7. Inicie o Control Plane

```bash
cd src/AppBridge.ControlPlane.Api

dotnet run
```

Server deve estar em `https://localhost:5001` (ou porta configurada).

## Variáveis de Ambiente

### Arquivo `.env.local` (Git-ignored)

Opcionalmente, crie arquivo na raiz:

```ini
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=https://localhost:5001

# DB
DB_HOST=localhost
DB_PORT=5432
DB_NAME=appbridge_dev
DB_USER=postgres
DB_PASSWORD=...

# Entra/AD
AZURE_TENANT_ID=...
AZURE_CLIENT_ID=...
AZURE_CLIENT_SECRET=...
```

Depois carregue no terminal (Linux/Mac):
```bash
source .env.local
```

Ou no PowerShell (Windows):
```powershell
Get-Content .env.local | ForEach-Object { if ($_.Trim() -and !$_.StartsWith('#')) { $var, $val = $_.Split('='); [Environment]::SetEnvironmentVariable($var, $val) } }
```

## Estrutura de Pastas

```
/src
  /AppBridge.ControlPlane
    /Core                  (domínio, entidades)
    /Application           (casos de uso, serviços)
    /Infrastructure        (DB, AD, certificados, RDS)
    /Api                   (controllers, DTOs, middleware)
  /AppBridge.Launcher      (WinUI 3, cliente Windows)
  /AppBridge.Agent         (Windows Service)

/tests
  /AppBridge.ControlPlane.Tests
    /Unit                  (testes de unidade)
    /Integration           (testes de integração)
    /Security              (testes de violação multi-tenant)

/docs                      (documentação)
  /adr                     (Architecture Decision Records)
  /auditoria               (audit logs de sessões)
  /operacao                (playbooks operacionais)
```

## Workflow de Desenvolvimento

### Branch Strategy

```bash
# Criar feature branch a partir de main
git checkout main
git pull origin main
git checkout -b feat/T-00.2-setup-repo  # ou feat/issue-123

# Desenvolver...
git add .
git commit -m "feat: descrição da mudança (T-00.2)"

# Push
git push -u origin feat/T-00.2-setup-repo

# Abrir PR no GitHub
```

### Commit Messages (Conventional Commits)

```
feat: adiciona endpoint de catálogo (T-04.1)
fix: corrige race condition em SessionReconciler
test: adiciona testes de multi-tenant (T-01a.4)
docs: atualiza SETUP-DEV.md
refactor: extrai RdpFileSigner para interface
chore: atualiza dependências

# Referência obrigatória:
Refs: T-XX.X, RF-YYY, RNF-ZZZ
```

## Troubleshooting

### "Cannot find dotnet"
```bash
dotnet --version
# Se não encontrar, instale: https://dotnet.microsoft.com/download
```

### "Connection refused" ao DB
```bash
# Verificar se PostgreSQL está rodando
docker ps | grep postgres
# ou
psql -U postgres -c "SELECT version();"
```

### "Secret 'X' not found"
```bash
# Verificar secrets carregados
dotnet user-secrets list

# Adicionar novamente
dotnet user-secrets set "ChaveAusente" "valor"
```

### Erro de migration
```bash
# Remover e recriar DB (dev only!)
dotnet ef database drop
dotnet ef database update
```

## CI/CD Local

Simular GitHub Actions localmente:

```bash
# Install act: https://github.com/nektos/act

# Rodar workflow de build
act push -j build-and-test

# Rodar workflow de secrets
act push -j detect-secrets
```

## Próximos Passos

- [ ] Configurar IDE (VS Code, Rider, Visual Studio)
- [ ] Ler `docs/ARQUITETURA.md` (modelo C4 + sequências)
- [ ] Ler `docs/BACKLOG_MVP0A_PRIORIZADO.md` (épicos e tarefas)
- [ ] Começar com issue `T-02.1` (setup de DB)

## Referências

- RP-01 a RP-09 (regras do projeto)
- ADR-0001 a ADR-0014 (decisões arquiteturais)
- BACKLOG_MVP0A_PRIORIZADO.md (épicos)
