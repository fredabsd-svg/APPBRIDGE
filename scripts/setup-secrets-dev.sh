#!/bin/bash

# setup-secrets-dev.sh — Configure dotnet user-secrets para desenvolvimento local
# Uso: ./scripts/setup-secrets-dev.sh
# Ou: ./scripts/setup-secrets-dev.sh --skip-certificate

set -euo pipefail

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;36m'
NC='\033[0m' # No Color

write_success() { echo -e "${GREEN}✓${NC} $1"; }
write_error() { echo -e "${RED}✗${NC} $1"; exit 1; }
write_info() { echo -e "${BLUE}ℹ${NC} $1"; }
write_warning() { echo -e "${YELLOW}⚠${NC} $1"; }

# Parâmetros
SKIP_CERTIFICATE=false
PROJECT_PATH="src/AppBridge.ControlPlane"

if [[ $# -gt 0 && "$1" == "--skip-certificate" ]]; then
    SKIP_CERTIFICATE=true
fi

# Verificar se dotnet está instalado
if ! command -v dotnet &> /dev/null; then
    write_error "dotnet CLI não encontrado. Instale .NET SDK 8.0+: https://dotnet.microsoft.com/download"
fi

write_info "AppBridge — Setup de Secrets para Desenvolvimento"
write_info "=================================================="
write_info ""

# Verificar se projeto existe
if [[ ! -d "$PROJECT_PATH" ]]; then
    write_error "Diretório não encontrado: $PROJECT_PATH"
fi

# Ir para o diretório do projeto
cd "$PROJECT_PATH" || exit 1

# 1. Inicializar user-secrets
write_info "1. Inicializando user-secrets..."
dotnet user-secrets init 2>/dev/null || true
write_success "User-secrets inicializado"
write_info ""

# 2. Configurar secrets
write_info "2. Configurando secrets..."

# Conta administrativa
write_info "   → Conta administrativa"
dotnet user-secrets set "AdminAccount:Username" "svc-appbridge" > /dev/null
dotnet user-secrets set "AdminAccount:Password" "AppBridge@Dev2026!" > /dev/null
write_success "AdminAccount configurado"

# Banco de dados
write_info "   → Banco de dados (PostgreSQL)"
DB_CONNECTION="Host=localhost;Port=5432;Database=appbridge_dev;Username=postgres;Password=postgres"
dotnet user-secrets set "Database:ConnectionString" "$DB_CONNECTION" > /dev/null
write_success "Database:ConnectionString configurado"

# Entra ID / Azure AD (placeholder para dev)
write_info "   → Autenticação Entra ID (placeholders para dev)"
dotnet user-secrets set "Authentication:AzureAd:TenantId" "00000000-0000-0000-0000-000000000000" > /dev/null
dotnet user-secrets set "Authentication:AzureAd:ClientId" "00000000-0000-0000-0000-000000000001" > /dev/null
dotnet user-secrets set "Authentication:AzureAd:ClientSecret" "appbridge-dev-secret-not-for-production" > /dev/null
write_success "Authentication:AzureAd configurado (com valores placeholder)"
write_warning "IMPORTANTE: Substituir pelos valores reais antes de usar com Entra ID"

# Certificado RDP
if [[ "$SKIP_CERTIFICATE" == false ]]; then
    write_info "   → Certificado de assinatura RDP (auto-assinado)"

    CERT_DIR="$HOME/certs"
    CERT_PATH="$CERT_DIR/appbridge-signing.pfx"
    CERT_PASSWORD="AppBridge@Signing2026!"

    mkdir -p "$CERT_DIR"

    # Verificar se certificado já existe
    if [[ -f "$CERT_PATH" ]]; then
        write_warning "Certificado já existe: $CERT_PATH"
        read -p "Sobrescrever? (s/n): " overwrite
        if [[ "$overwrite" == "s" ]]; then
            rm "$CERT_PATH"
            write_info "Certificado antigo removido"
        else
            write_info "Mantendo certificado existente"
        fi
    fi

    # Gerar certificado auto-assinado se não existir
    if [[ ! -f "$CERT_PATH" ]]; then
        write_info "Gerando certificado auto-assinado..."

        # Gerar chave privada e certificado
        openssl req -x509 -newkey rsa:2048 -keyout "$CERT_DIR/appbridge-signing.key" \
            -out "$CERT_DIR/appbridge-signing.crt" -days 365 -nodes \
            -subj "/CN=AppBridge RDP Signing"

        # Converter para PKCS#12 (.pfx)
        openssl pkcs12 -export -out "$CERT_PATH" \
            -inkey "$CERT_DIR/appbridge-signing.key" \
            -in "$CERT_DIR/appbridge-signing.crt" \
            -passout pass:"$CERT_PASSWORD"

        # Remover arquivos intermediários
        rm "$CERT_DIR/appbridge-signing.key" "$CERT_DIR/appbridge-signing.crt"

        write_success "Certificado criado: $CERT_PATH"
    fi

    dotnet user-secrets set "RdpSigning:CertificatePath" "$CERT_PATH" > /dev/null
    dotnet user-secrets set "RdpSigning:CertificatePassword" "$CERT_PASSWORD" > /dev/null
    write_success "RdpSigning configurado"
fi

# JWT (opcional)
write_info "   → JWT (opcional para MVP-0)"
JWT_KEY=$(openssl rand -base64 32)
dotnet user-secrets set "Jwt:SigningKey" "$JWT_KEY" > /dev/null
dotnet user-secrets set "Jwt:Issuer" "https://localhost:5001" > /dev/null
dotnet user-secrets set "Jwt:Audience" "appbridge-launcher" > /dev/null
write_success "JWT configurado"

# Logging
write_info "   → Logging"
dotnet user-secrets set "Logging:LogLevel:Default" "Information" > /dev/null
dotnet user-secrets set "Logging:LogLevel:Microsoft" "Warning" > /dev/null
write_success "Logging configurado"

# 3. Listar todos os secrets
write_info ""
write_info "3. Secrets configurados:"
write_info "========================"
dotnet user-secrets list | while read -r line; do
    write_success "$line"
done

write_info ""
write_success "Setup completado com sucesso!"
write_info ""
write_info "Próximos passos:"
write_info "1. Revisar valores placeholder (Authentication:AzureAd)"
write_info "2. Testar conexão com banco: dotnet run --project Api"
write_info "3. Ver documentação completa em docs/SECRETS-SETUP.md"
