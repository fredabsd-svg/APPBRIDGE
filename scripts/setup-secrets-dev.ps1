<#
.SYNOPSIS
    Configure dotnet user-secrets para desenvolvimento local
.DESCRIPTION
    Inicializa e configura todos os secrets necessários para o Control Plane API
.PARAMETER SkipCertificate
    Se $true, pula a geração do certificado auto-assinado
.PARAMETER ProjectPath
    Caminho para o projeto (padrão: src/AppBridge.ControlPlane)
.EXAMPLE
    .\setup-secrets-dev.ps1
    .\setup-secrets-dev.ps1 -SkipCertificate $true
#>

param(
    [bool]$SkipCertificate = $false,
    [string]$ProjectPath = "src/AppBridge.ControlPlane"
)

$ErrorActionPreference = "Stop"

# Cores para output
function Write-Success { Write-Host -ForegroundColor Green "✓ $args" }
function Write-Error { Write-Host -ForegroundColor Red "✗ $args" }
function Write-Info { Write-Host -ForegroundColor Cyan "ℹ $args" }
function Write-Warning { Write-Host -ForegroundColor Yellow "⚠ $args" }

# Verificar se dotnet está instalado
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet CLI não encontrado. Instale .NET SDK 8.0+: https://dotnet.microsoft.com/download"
    exit 1
}

Write-Info "AppBridge — Setup de Secrets para Desenvolvimento"
Write-Info "=================================================="
Write-Info ""

# Ir para o diretório do projeto
if (-not (Test-Path $ProjectPath)) {
    Write-Error "Diretório não encontrado: $ProjectPath"
    exit 1
}

Push-Location $ProjectPath
try {
    # 1. Inicializar user-secrets
    Write-Info "1. Inicializando user-secrets..."
    dotnet user-secrets init 2>$null
    Write-Success "User-secrets inicializado"
    Write-Info ""

    # 2. Configurar secrets
    Write-Info "2. Configurando secrets..."

    # Conta administrativa
    Write-Info "   → Conta administrativa"
    dotnet user-secrets set "AdminAccount:Username" "svc-appbridge" | Out-Null
    dotnet user-secrets set "AdminAccount:Password" "AppBridge@Dev2026!" | Out-Null
    Write-Success "AdminAccount configurado"

    # Banco de dados
    Write-Info "   → Banco de dados (PostgreSQL)"
    $dbConnection = "Host=localhost;Port=5432;Database=appbridge_dev;Username=postgres;Password=postgres"
    dotnet user-secrets set "Database:ConnectionString" $dbConnection | Out-Null
    Write-Success "Database:ConnectionString configurado"

    # Entra ID / Azure AD (placeholder para dev)
    Write-Info "   → Autenticação Entra ID (placeholders para dev)"
    dotnet user-secrets set "Authentication:AzureAd:TenantId" "00000000-0000-0000-0000-000000000000" | Out-Null
    dotnet user-secrets set "Authentication:AzureAd:ClientId" "00000000-0000-0000-0000-000000000001" | Out-Null
    dotnet user-secrets set "Authentication:AzureAd:ClientSecret" "appbridge-dev-secret-not-for-production" | Out-Null
    Write-Success "Authentication:AzureAd configurado (com valores placeholder)"
    Write-Warning "   IMPORTANTE: Substituir pelos valores reais antes de usar com Entra ID"

    # Certificado RDP
    if (-not $SkipCertificate) {
        Write-Info "   → Certificado de assinatura RDP (auto-assinado)"

        $certDir = "$env:USERPROFILE\certs"
        if (-not (Test-Path $certDir)) {
            New-Item -ItemType Directory -Path $certDir | Out-Null
            Write-Success "Diretório de certificados criado: $certDir"
        }

        $certPath = "$certDir\appbridge-signing.pfx"
        $certPassword = "AppBridge@Signing2026!"

        # Verificar se certificado já existe
        if (Test-Path $certPath) {
            Write-Warning "Certificado já existe: $certPath"
            $overwrite = Read-Host "Sobrescrever? (s/n)"
            if ($overwrite -ne "s") {
                Write-Info "Mantendo certificado existente"
            } else {
                Remove-Item $certPath
                Write-Info "Certificado antigo removido"
            }
        }

        # Gerar certificado auto-assinado se não existir
        if (-not (Test-Path $certPath)) {
            Write-Info "Gerando certificado auto-assinado..."

            $cert = New-SelfSignedCertificate -CertStoreLocation "cert:\CurrentUser\My" `
                -Subject "CN=AppBridge RDP Signing" `
                -KeyUsage DigitalSignature `
                -NotAfter (Get-Date).AddYears(1)

            $secPassword = ConvertTo-SecureString -String $certPassword -AsPlainText -Force
            Export-PfxCertificate -Cert $cert `
                -FilePath $certPath `
                -Password $secPassword | Out-Null

            Write-Success "Certificado criado: $certPath"
        }

        dotnet user-secrets set "RdpSigning:CertificatePath" $certPath | Out-Null
        dotnet user-secrets set "RdpSigning:CertificatePassword" $certPassword | Out-Null
        Write-Success "RdpSigning configurado"
    }

    # JWT (opcional)
    Write-Info "   → JWT (opcional para MVP-0)"
    $jwtKey = [Convert]::ToBase64String((1..32 | ForEach-Object {[byte](Get-Random -Maximum 256)}))
    dotnet user-secrets set "Jwt:SigningKey" $jwtKey | Out-Null
    dotnet user-secrets set "Jwt:Issuer" "https://localhost:5001" | Out-Null
    dotnet user-secrets set "Jwt:Audience" "appbridge-launcher" | Out-Null
    Write-Success "JWT configurado"

    # Logging
    Write-Info "   → Logging"
    dotnet user-secrets set "Logging:LogLevel:Default" "Information" | Out-Null
    dotnet user-secrets set "Logging:LogLevel:Microsoft" "Warning" | Out-Null
    Write-Success "Logging configurado"

    # 3. Listar todos os secrets
    Write-Info ""
    Write-Info "3. Secrets configurados:"
    Write-Info "========================"
    dotnet user-secrets list | ForEach-Object { Write-Success $_ }

    Write-Info ""
    Write-Success "Setup completado com sucesso!"
    Write-Info ""
    Write-Info "Próximos passos:"
    Write-Info "1. Revisar valores placeholder (Authentication:AzureAd)"
    Write-Info "2. Testar conexão com banco: dotnet run --project Api"
    Write-Info "3. Ver documentação completa em docs/SECRETS-SETUP.md"

} finally {
    Pop-Location
}
