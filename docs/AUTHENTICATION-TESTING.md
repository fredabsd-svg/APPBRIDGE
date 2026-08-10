# Testes de Autenticação e Autorização — T-02.3

> Guia de teste dos componentes de autenticação JWT e autorização implementados em T-02.3.
> Status: MVP-0a foundation

---

## 1. Componentes testáveis

### 1.1 JwtTokenService

**Responsabilidade:** Geração e validação de tokens JWT HS256 com claims multi-tenant.

**Testes unitários:**

| Caso | Entrada | Esperado | Status |
|------|---------|----------|--------|
| **Generate_ValidUser_ReturnsSignedToken** | userId, userIdentifier, tenantId válidos | Token JWT codificado, claims contêm `sub`, `unique_name`, `tenant_id` | Implementar |
| **Generate_TokenHasCorrectExpiry** | Parâmetros válidos | `exp` claim == now + JwtOptions.ExpiryMinutes | Implementar |
| **Validate_ValidToken_ReturnsTrue** | Token gerado válido | `true` | Implementar |
| **Validate_ExpiredToken_ReturnsFalse** | Token expirado (exp < now) | `false` | Implementar |
| **Validate_InvalidSignature_ReturnsFalse** | Token assinado com chave errada | `false` | Implementar |
| **Validate_MalformedToken_ReturnsFalse** | String não-JWT | `false` | Implementar |
| **Validate_ClockSkewTolerance** | Token expira em 2s, clock skew é 5s | `true` | Implementar |

**Teste de integração:**

```csharp
// Simula fluxo completo: gera token, aguarda expiração simulada, valida falha
[Fact]
public async Task GenerateAndValidateToken_Lifecycle()
{
    // Arrange
    var userId = Guid.NewGuid().ToString();
    var tenantId = Guid.NewGuid();
    
    // Act
    var token = _jwtTokenService.GenerateToken(userId, "user@domain.com", tenantId);
    var isValid = _jwtTokenService.ValidateToken(token);
    
    // Assert
    Assert.True(isValid);
    Assert.Contains("Bearer", token); // ou validate estrutura
}
```

---

### 1.2 AuthenticationService

**Responsabilidade:** Autenticar usuários contra banco de dados, retornar token JWT.

**Testes unitários:**

| Caso | Entrada | Mock | Esperado | Status |
|------|---------|------|----------|--------|
| **Authenticate_ValidUser_ReturnsToken** | userIdentifier, password, tenantId | User existe, ativo, não-deletado | AuthTokenDto com token válido | Implementar |
| **Authenticate_UserNotFound_ThrowsUnauthorized** | identifier inexistente | `null` | `UnauthorizedAccessException` | Implementar |
| **Authenticate_UserInactive_ThrowsUnauthorized** | User.IsActive == false | User existe, mas inativo | `UnauthorizedAccessException` | Implementar |
| **Authenticate_UserDeleted_ThrowsUnauthorized** | User.DeletedAt != null | User com soft-delete | `UnauthorizedAccessException` | Implementar |
| **Authenticate_WrongTenant_ThrowsUnauthorized** | tenantId não-matching | User de outro tenant | `UnauthorizedAccessException` | Implementar |
| **ValidateUser_UserExists_ReturnsTrue** | identifier, tenantId válidos | User existe, ativo | `true` | Implementar |
| **ValidateUser_UserNotFound_ReturnsFalse** | identifier inexistente | `null` | `false` | Implementar |

**Teste de integração com DbContext (InMemory):**

```csharp
[Fact]
public async Task Authenticate_ValidUserInDatabase_ReturnsTokenWithCorrectClaims()
{
    // Arrange
    var tenantId = Guid.NewGuid();
    var userId = Guid.NewGuid();
    var user = new User
    {
        Id = userId,
        TenantId = tenantId,
        Identifier = "user@domain.com",
        DisplayName = "Test User",
        Email = "user@domain.com",
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };
    
    _dbContext.Users.Add(user);
    await _dbContext.SaveChangesAsync();
    
    // Act
    var token = await _authenticationService.AuthenticateAsync(
        "user@domain.com", "password", tenantId);
    
    // Assert
    Assert.NotNull(token.AccessToken);
    Assert.Equal("Bearer", token.TokenType);
    Assert.True(_jwtTokenService.ValidateToken(token.AccessToken));
}
```

---

### 1.3 AuthController

**Responsabilidade:** Endpoint HTTP POST /api/auth/login que retorna token.

**Testes de API (xUnit + HttpClient):**

| Método | Rota | Entrada | Esperado | Status |
|--------|------|---------|----------|--------|
| **POST** | `/api/auth/login` | `{ tenantId, userIdentifier, password }` válidos | 200 OK, `{ accessToken, tokenType, expiresIn }` | Implementar |
| **POST** | `/api/auth/login` | tenantId inválido (UUID) | 400 Bad Request | Implementar |
| **POST** | `/api/auth/login` | Campos faltando | 400 Bad Request, mensagem descritiva | Implementar |
| **POST** | `/api/auth/login` | Credenciais inválidas | 401 Unauthorized | Implementar |
| **POST** | `/api/auth/login` | User desativado | 401 Unauthorized | Implementar |

**Teste com TestServer:**

```csharp
[Fact]
public async Task Login_ValidCredentials_Returns200WithToken()
{
    // Arrange
    var tenantId = Guid.NewGuid();
    var loginRequest = new
    {
        TenantId = tenantId.ToString(),
        UserIdentifier = "user@domain.com",
        Password = "password123"
    };
    
    // Act
    var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginRequest);
    var tokenResponse = await response.Content.ReadAsAsync<AuthTokenDto>();
    
    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.NotNull(tokenResponse.AccessToken);
    Assert.Equal("Bearer", tokenResponse.TokenType);
}
```

---

### 1.4 HealthController (proteção de endpoint)

**Responsabilidade:** Demonstrar autorização via `[Authorize]` atributo.

**Testes de API:**

| Rota | Auth | Esperado | Status |
|------|------|----------|--------|
| `GET /api/health/status` | Sem token | 200 OK (público) | Implementar |
| `GET /api/health/protected` | Sem token | 401 Unauthorized | Implementar |
| `GET /api/health/protected` | Token válido | 200 OK, retorna userId e tenantId | Implementar |
| `GET /api/health/protected` | Token expirado | 401 Unauthorized | Implementar |
| `GET /api/health/protected` | Token inválido | 401 Unauthorized | Implementar |

---

## 2. Fluxo de teste manual (Postman/curl)

### 2.1 Preparar dados de teste

**Pré-condição:** PostgreSQL rodando com schema inicial (T-02.2).

```sql
-- Cria tenant de teste
INSERT INTO tenants (id, identifier, name, is_active, created_at, updated_at)
VALUES (
  '550e8400-e29b-41d4-a716-446655440000'::uuid,
  'test-tenant',
  'Test Tenant',
  true,
  now(),
  now()
);

-- Cria usuário de teste
INSERT INTO users (
  id, tenant_id, identifier, display_name, email, is_active,
  created_at, updated_at
)
VALUES (
  '660e8400-e29b-41d4-a716-446655440000'::uuid,
  '550e8400-e29b-41d4-a716-446655440000'::uuid,
  'test@domain.com',
  'Test User',
  'test@domain.com',
  true,
  now(),
  now()
);
```

### 2.2 Testar login

```bash
# POST /api/auth/login
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "550e8400-e29b-41d4-a716-446655440000",
    "userIdentifier": "test@domain.com",
    "password": "test-password"
  }'

# Esperado: 200 OK
# {
#   "accessToken": "eyJhbGciOiJIUzI1NiIs...",
#   "tokenType": "Bearer",
#   "expiresIn": 3600
# }
```

### 2.3 Testar acesso público

```bash
# GET /api/health/status (público)
curl https://localhost:5001/api/health/status

# Esperado: 200 OK
# {
#   "status": "healthy",
#   "timestamp": "2026-08-10T14:30:00Z"
# }
```

### 2.4 Testar acesso protegido sem token

```bash
# GET /api/health/protected (sem Authorization header)
curl https://localhost:5001/api/health/protected

# Esperado: 401 Unauthorized
```

### 2.5 Testar acesso protegido com token

```bash
# GET /api/health/protected (com token válido)
curl -H "Authorization: Bearer <token-from-2.2>" \
  https://localhost:5001/api/health/protected

# Esperado: 200 OK
# {
#   "status": "authenticated",
#   "userId": "660e8400-e29b-41d4-a716-446655440000",
#   "tenantId": "550e8400-e29b-41d4-a716-446655440000",
#   "timestamp": "2026-08-10T14:30:00Z"
# }
```

---

## 3. Cobertura de teste esperada

**Target:** 80% cobertura no Application + Infrastructure layers (RP-08).

### 3.1 Linhas críticas (alta prioridade)

- [ ] JwtTokenService.GenerateToken() — geração de claims
- [ ] JwtTokenService.ValidateToken() — validação de assinatura e expiração
- [ ] AuthenticationService.AuthenticateAsync() — fluxo completo
- [ ] AuthenticationService.ValidateUserAsync() — consulta de usuário
- [ ] AuthController.Login() — validação de entrada e resposta

### 3.2 Casos de erro (cobertura de exceção)

- [ ] Token inválido → `false`
- [ ] Token expirado → `false`
- [ ] Usuário não encontrado → `UnauthorizedAccessException`
- [ ] Usuário inativo → `UnauthorizedAccessException`
- [ ] Tenant mismatch → `UnauthorizedAccessException`

---

## 4. Tarefas de implementação

| Tarefa | Descrição | Prioridade | Bloqueia |
|--------|-----------|-----------|----------|
| T-02.3.1 | Escrever testes unitários para JwtTokenService | Alta | Nenhuma |
| T-02.3.2 | Escrever testes de integração para AuthenticationService | Alta | T-02.3.1 |
| T-02.3.3 | Escrever testes de API para AuthController | Alta | T-02.3.2 |
| T-02.3.4 | Escrever testes de HealthController | Média | T-02.3.3 |
| T-02.3.5 | Executar teste manual completo (curl/Postman) | Alta | T-02.3.4 |
| T-02.3.6 | Gerar relatório de cobertura (SonarQube/coverlet) | Média | T-02.3.5 |
| **T-02.6** | Implementar middleware de contexto de tenant | Bloqueia T-02.4 | — |

---

## 5. Premissas

| ID | Premissa | Confirmar com |
|----|----------|---------------|
| PRE-30 | Password hashing será implementado em T-02.6 (MVP-0 placeholder) | Frederico |
| PRE-31 | Token expiry de 60 minutos (JwtOptions.ExpiryMinutes) é aceitável para MVP-0 | Frederico |
| PRE-32 | Não há requisito de refresh token ou token revocation em MVP-0 | `REQUISITOS.md` RF-031 |

---

## 6. Notas de implementação

- **Password verification placeholder:** AuthenticationService.AuthenticateAsync() não valida senha porque hashing será feito em T-02.6. Hoje aceita qualquer senha.
- **Azure AD integração:** ADR-0001 especifica suporte a Azure AD/Entra ID em fases futuras. MVP-0 usa apenas banco de dados.
- **Tenant context:** Token JWT carrega `tenant_id` claim, mas a execução de request não isola ainda a tenant (fará parte de T-02.6 middleware).
- **Swagger integration:** Bearer scheme documentado em Program.cs; teste direto via Swagger UI em /swagger.

---

## 7. Verificação de sucesso

✅ T-02.3 considerada completa quando:

1. [ ] Todas as 8 testes unitários de JwtTokenService passam
2. [ ] Todas as 6 testes de integração de AuthenticationService passam
3. [ ] Todas as 3 testes de API de AuthController passam
4. [ ] Todos os 5 testes de HealthController passam
5. [ ] Cobertura total ≥ 80% (Application + Infrastructure)
6. [ ] Teste manual completo executado sem erros (seção 2)
7. [ ] Documentação atualizada em STATUS.md

---

Próxima tarefa: **T-02.4 — Audit Logging Framework (blocker pattern)**
