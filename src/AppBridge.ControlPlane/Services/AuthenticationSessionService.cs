using System.Data;
using System.Security.Claims;
using AppBridge.ControlPlane.Authentication;
using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AppBridge.ControlPlane.Services;

public sealed record AuthenticationSessionRequest(string IdentityToken, string WorkstationName);
public sealed record AuthenticationRefreshRequest(string RefreshToken);
public sealed record AuthenticatedUser(Guid Id, string DisplayName, AuthenticatedTenant Tenant, string[] Roles);
public sealed record AuthenticatedTenant(Guid Id, string Name);
public sealed record AuthenticationSessionResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt,
    AuthenticatedUser User);
public sealed record AuthenticationRefreshResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt);
public sealed record AuthenticationSessionResult(int StatusCode, string? ErrorCode, AuthenticationSessionResponse? Response);
public sealed record AuthenticationRefreshResult(int StatusCode, string? ErrorCode, AuthenticationRefreshResponse? Response);

public sealed class AuthenticationSessionService(
    IIdentityTokenValidator identityTokenValidator,
    IOptions<IdentityProviderOptions> identityProviderOptions,
    IOptions<RefreshTokenOptions> refreshTokenOptions,
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
            || !IsValidWorkstationName(request.WorkstationName))
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
                return Task.FromResult(new AuthenticationSessionResult(
                    StatusCodes.Status403Forbidden, resultCode, null));
            }

            var now = DateTimeOffset.UtcNow;
            var sessionId = Guid.CreateVersion7();
            var absoluteExpiresAt = now.AddDays(refreshTokenOptions.Value.MaximumSessionLifetimeDays);
            var refreshExpiresAt = Min(
                now.AddDays(refreshTokenOptions.Value.IdleLifetimeDays),
                absoluteExpiresAt);
            var refreshToken = RefreshTokenValue.Create(tenantId, sessionId);
            var session = new AuthenticationSession
            {
                Id = sessionId,
                TenantId = tenantId,
                UserAccountId = account.Id,
                WorkstationName = request.WorkstationName,
                LastUsedAt = now,
                ExpiresAt = refreshExpiresAt,
                AbsoluteExpiresAt = absoluteExpiresAt
            };

            dbContext.AuthenticationSessions.Add(session);
            dbContext.AuthenticationRefreshTokens.Add(new AuthenticationRefreshToken
            {
                TenantId = tenantId,
                SessionId = sessionId,
                TokenHash = RefreshTokenValue.Hash(refreshToken),
                ExpiresAt = refreshExpiresAt
            });
            account.LastLoginAt = now;

            var accessToken = tokenIssuer.Issue(account, tenant, sessionId);
            return Task.FromResult(new AuthenticationSessionResult(
                StatusCodes.Status201Created,
                null,
                new AuthenticationSessionResponse(
                    accessToken.Token,
                    accessToken.ExpiresAt,
                    refreshToken,
                    refreshExpiresAt,
                    new AuthenticatedUser(
                        account.Id,
                        account.DisplayName,
                        new AuthenticatedTenant(tenant.Id, tenant.Name),
                        ["user"]))));
        }, cancellationToken);
    }

    public async Task<AuthenticationRefreshResult> RefreshAsync(
        AuthenticationRefreshRequest request,
        string sourceIp,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (!RefreshTokenValue.TryParse(request.RefreshToken, out var parsed) || parsed is null)
        {
            return ExpiredRefresh();
        }

        tenantContext.Bind(parsed.TenantId);
        if (!await dbContext.Tenants.AnyAsync(cancellationToken))
        {
            return ExpiredRefresh();
        }

        var now = DateTimeOffset.UtcNow;
        using var refreshPayload = JsonDocument.Parse("{\"method\":\"refresh\"}");
        var auditEvent = new AccessEvent
        {
            TenantId = parsed.TenantId,
            EventType = AccessEventType.Authentication,
            Result = AccessEventResult.Failure,
            FailureReason = "REFRESH_EXPIRED",
            SourceIp = sourceIp,
            OccurredAt = now,
            CorrelationId = correlationId,
            Payload = refreshPayload
        };

        return await auditWriter.ExecuteAsync(auditEvent, async ct =>
        {
            await LockSessionAsync(parsed.TenantId, parsed.SessionId, ct);
            var session = await dbContext.AuthenticationSessions
                .SingleOrDefaultAsync(item => item.Id == parsed.SessionId, ct);
            if (session is null)
            {
                return ExpiredRefresh();
            }

            auditEvent.UserAccountId = session.UserAccountId;
            auditEvent.WorkstationName = session.WorkstationName;
            var presentedHash = RefreshTokenValue.Hash(request.RefreshToken);
            var storedToken = await dbContext.AuthenticationRefreshTokens
                .SingleOrDefaultAsync(item => item.SessionId == session.Id && item.TokenHash == presentedHash, ct);

            if (session.RevokedAt is not null)
            {
                return ExpiredRefresh();
            }

            if (storedToken?.ConsumedAt is not null)
            {
                session.RevokedAt = now;
                session.RevocationReason = "refresh_replay";
                auditEvent.FailureReason = "REFRESH_REPLAY";
                return ExpiredRefresh();
            }

            if (storedToken is null
                || storedToken.ExpiresAt <= now
                || session.ExpiresAt <= now
                || session.AbsoluteExpiresAt <= now)
            {
                return ExpiredRefresh();
            }

            var tenant = await dbContext.Tenants.SingleOrDefaultAsync(ct);
            var account = await dbContext.UserAccounts
                .SingleOrDefaultAsync(user => user.Id == session.UserAccountId, ct);
            if (tenant is null || tenant.Status != TenantStatus.Active
                || account is null || account.Status != UserAccountStatus.Active)
            {
                session.RevokedAt = now;
                session.RevocationReason = tenant is null || tenant.Status != TenantStatus.Active
                    ? "tenant_suspended"
                    : "account_disabled";
                return ExpiredRefresh();
            }

            storedToken.ConsumedAt = now;
            session.LastUsedAt = now;
            session.ExpiresAt = Min(
                now.AddDays(refreshTokenOptions.Value.IdleLifetimeDays),
                session.AbsoluteExpiresAt);
            var nextRefreshToken = RefreshTokenValue.Create(session.TenantId, session.Id);
            dbContext.AuthenticationRefreshTokens.Add(new AuthenticationRefreshToken
            {
                TenantId = session.TenantId,
                SessionId = session.Id,
                TokenHash = RefreshTokenValue.Hash(nextRefreshToken),
                ExpiresAt = session.ExpiresAt
            });

            auditEvent.Result = AccessEventResult.Success;
            auditEvent.FailureReason = null;
            var accessToken = tokenIssuer.Issue(account, tenant, session.Id);
            return new AuthenticationRefreshResult(
                StatusCodes.Status200OK,
                null,
                new AuthenticationRefreshResponse(
                    accessToken.Token,
                    accessToken.ExpiresAt,
                    nextRefreshToken,
                    session.ExpiresAt));
        }, cancellationToken);
    }

    public Task LogoutAsync(
        Guid userAccountId,
        Guid sessionId,
        string sourceIp,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var auditEvent = new AccessEvent
        {
            TenantId = tenantContext.EffectiveTenantId,
            UserAccountId = userAccountId,
            EventType = AccessEventType.Logout,
            Result = AccessEventResult.Success,
            SourceIp = sourceIp,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId
        };

        return LogoutCoreAsync(auditEvent, sessionId, userAccountId, cancellationToken);
    }

    private async Task LogoutCoreAsync(
        AccessEvent auditEvent,
        Guid sessionId,
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        await auditWriter.ExecuteAsync(auditEvent, async ct =>
        {
            await LockSessionAsync(tenantContext.EffectiveTenantId, sessionId, ct);
            var session = await dbContext.AuthenticationSessions
                .SingleOrDefaultAsync(item => item.Id == sessionId && item.UserAccountId == userAccountId, ct);
            if (session is null)
            {
                throw new InvalidOperationException("A sessão autenticada não existe.");
            }

            session.RevokedAt ??= DateTimeOffset.UtcNow;
            session.RevocationReason ??= "logout";
            auditEvent.WorkstationName = session.WorkstationName;
            return true;
        }, cancellationToken);
    }

    private async Task LockSessionAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT id FROM auth_session WHERE tenant_id = @tenant_id AND id = @session_id FOR UPDATE";
        var tenantParameter = command.CreateParameter();
        tenantParameter.ParameterName = "tenant_id";
        tenantParameter.DbType = DbType.Guid;
        tenantParameter.Value = tenantId;
        command.Parameters.Add(tenantParameter);
        var sessionParameter = command.CreateParameter();
        sessionParameter.ParameterName = "session_id";
        sessionParameter.DbType = DbType.Guid;
        sessionParameter.Value = sessionId;
        command.Parameters.Add(sessionParameter);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static bool IsValidWorkstationName(string? workstationName)
        => !string.IsNullOrWhiteSpace(workstationName)
            && workstationName.Length <= 128
            && !workstationName.Contains('\r')
            && !workstationName.Contains('\n');

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second)
        => first <= second ? first : second;

    private static AuthenticationRefreshResult ExpiredRefresh()
        => new(StatusCodes.Status401Unauthorized, "REFRESH_EXPIRED", null);
}
