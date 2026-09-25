using System.Security.Claims;
using AppBridge.ControlPlane.Authentication;
using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppBridge.ControlPlane.Services;

public sealed record AuthenticationSessionRequest(string IdentityToken, string WorkstationName);
public sealed record AuthenticatedUser(Guid Id, string DisplayName, AuthenticatedTenant Tenant, string[] Roles);
public sealed record AuthenticatedTenant(Guid Id, string Name);
public sealed record AuthenticationSessionResponse(string AccessToken, DateTimeOffset ExpiresAt, AuthenticatedUser User);
public sealed record AuthenticationSessionResult(int StatusCode, string? ErrorCode, AuthenticationSessionResponse? Response);

public sealed class AuthenticationSessionService(
    IIdentityTokenValidator identityTokenValidator,
    IOptions<IdentityProviderOptions> identityProviderOptions,
    AppDbContext dbContext,
    TenantContext tenantContext,
    AuditWriter auditWriter,
    ControlPlaneTokenIssuer tokenIssuer,
    ILogger<AuthenticationSessionService> logger)
{
    public async Task<AuthenticationSessionResult> ExchangeAsync(
        AuthenticationSessionRequest request,
        string sourceIp,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdentityToken)
            || string.IsNullOrWhiteSpace(request.WorkstationName)
            || request.WorkstationName.Length > 128
            || request.WorkstationName.Contains('\r')
            || request.WorkstationName.Contains('\n'))
        {
            return new AuthenticationSessionResult(StatusCodes.Status400BadRequest, "MALFORMED_REQUEST", null);
        }

        ClaimsPrincipal identity;
        try
        {
            identity = await identityTokenValidator.ValidateAsync(request.IdentityToken, cancellationToken);
        }
        catch (IdentityTokenRejectedException)
        {
            logger.LogWarning(
                "Token de identidade inválido ou expirado de {sourceIp}; correlationId {correlationId}",
                sourceIp,
                correlationId);
            return new AuthenticationSessionResult(StatusCodes.Status401Unauthorized, "INVALID_IDENTITY_TOKEN", null);
        }

        var externalTenantId = identity.FindFirst("tid")?.Value;
        var externalSubject = identity.FindFirst("oid")?.Value ?? identity.FindFirst("sub")?.Value;
        var mappings = identityProviderOptions.Value.TenantMappings;
        if (string.IsNullOrWhiteSpace(externalTenantId)
            || string.IsNullOrWhiteSpace(externalSubject)
            || !mappings.TryGetValue(externalTenantId, out var configuredTenantId)
            || !Guid.TryParse(configuredTenantId, out var tenantId))
        {
            logger.LogWarning(
                "Identidade sem vínculo de tenant provisionado a partir de {sourceIp}; correlationId {correlationId}",
                sourceIp,
                correlationId);
            return new AuthenticationSessionResult(StatusCodes.Status403Forbidden, "USER_NOT_PROVISIONED", null);
        }

        tenantContext.Bind(tenantId);
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(cancellationToken);
        if (tenant is null)
        {
            logger.LogError("O tenant configurado para Entra ID não existe; correlationId {correlationId}", correlationId);
            return new AuthenticationSessionResult(StatusCodes.Status403Forbidden, "USER_NOT_PROVISIONED", null);
        }

        var account = await dbContext.UserAccounts
            .SingleOrDefaultAsync(user => user.ExternalSubject == externalSubject, cancellationToken);
        var resultCode = tenant.Status != TenantStatus.Active
            ? "TENANT_SUSPENDED"
            : account is null
                ? "USER_NOT_PROVISIONED"
                : account.Status != UserAccountStatus.Active
                    ? "USER_DISABLED"
                    : null;
        var auditEvent = new AccessEvent
        {
            TenantId = tenantId,
            UserAccountId = account?.Id,
            EventType = AccessEventType.Authentication,
            Result = resultCode is null ? AccessEventResult.Success : AccessEventResult.Failure,
            FailureReason = resultCode,
            SourceIp = sourceIp,
            WorkstationName = request.WorkstationName,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId
        };

        return await auditWriter.ExecuteAsync(auditEvent, _ =>
        {
            if (resultCode is not null || account is null)
            {
                var statusCode = resultCode == "TENANT_SUSPENDED"
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status403Forbidden;
                return Task.FromResult(new AuthenticationSessionResult(statusCode, resultCode, null));
            }

            var issued = tokenIssuer.Issue(account, tenant);
            return Task.FromResult(new AuthenticationSessionResult(
                StatusCodes.Status201Created,
                null,
                new AuthenticationSessionResponse(
                    issued.Token,
                    issued.ExpiresAt,
                    new AuthenticatedUser(
                        account.Id,
                        account.DisplayName,
                        new AuthenticatedTenant(tenant.Id, tenant.Name),
                        ["user"]))));
        }, cancellationToken);
    }
}
