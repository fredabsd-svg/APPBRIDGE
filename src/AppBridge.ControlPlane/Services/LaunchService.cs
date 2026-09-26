using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using AppBridge.ControlPlane.Launching;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AppBridge.ControlPlane.Services;

public sealed record LaunchRequest(Guid ApplicationId, LaunchPurpose Purpose, string WorkstationName);
public sealed record LaunchHostResponse(string DisplayName);
public sealed record LaunchResponse(
    Guid LaunchId,
    bool SessionReused,
    string RdpFile,
    DateTimeOffset ExpiresAt,
    LaunchHostResponse Host,
    Guid CorrelationId);
public sealed record LaunchCommandResult(int StatusCode, string? ErrorCode, LaunchResponse? Response);

public sealed class LaunchService(
    AppDbContext dbContext,
    TenantContext tenantContext,
    AuthorizationService authorizationService,
    ISessionBackend sessionBackend,
    SessionRegistry sessionRegistry,
    RedirectionPolicyResolver policyResolver,
    RdpDescriptorBuilder descriptorBuilder,
    IRdpFileSigner signer,
    AuditWriter auditWriter,
    ILogger<LaunchService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<LaunchCommandResult> LaunchAsync(
        Guid userAccountId,
        LaunchRequest request,
        Guid idempotencyKey,
        string sourceIp,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            throw new InvalidOperationException("O lançamento exige TenantContext.");
        }

        if (request.ApplicationId == Guid.Empty
            || !Enum.IsDefined(request.Purpose)
            || string.IsNullOrWhiteSpace(request.WorkstationName)
            || request.WorkstationName.Length > 128
            || request.WorkstationName.Contains('\r')
            || request.WorkstationName.Contains('\n'))
        {
            return new LaunchCommandResult(StatusCodes.Status400BadRequest, "MALFORMED_REQUEST", null);
        }

        var requestHash = ComputeRequestHash(userAccountId, request);
        var now = DateTimeOffset.UtcNow;

        try
        {
            return await auditWriter.ExecuteAsync<LaunchCommandResult>(null, async transactionToken =>
            {
                var existing = await dbContext.LaunchIdempotencyRecords.SingleOrDefaultAsync(
                    record => record.IdempotencyKey == idempotencyKey,
                    transactionToken);
                if (existing is not null)
                {
                    return await ExistingResultAsync(existing, requestHash, now, transactionToken);
                }

                var reservation = new LaunchIdempotencyRecord
                {
                    TenantId = tenantContext.TenantId.Value,
                    IdempotencyKey = idempotencyKey,
                    RequestHash = requestHash,
                    ExpiresAt = now.AddSeconds(60),
                    ResponseJson = "{}"
                };
                dbContext.LaunchIdempotencyRecords.Add(reservation);
                // A chave única serializa duas tentativas simultâneas da mesma operação.
                await dbContext.SaveChangesAsync(transactionToken);

                var user = await dbContext.UserAccounts.SingleOrDefaultAsync(
                    account => account.Id == userAccountId,
                    transactionToken);
                if (user is null || user.Status != UserAccountStatus.Active)
                {
                    return await StoreResultAsync(reservation,
                        new LaunchCommandResult(StatusCodes.Status403Forbidden, "USER_DISABLED", null), transactionToken);
                }

                var application = await dbContext.Applications.SingleOrDefaultAsync(
                    candidate => candidate.Id == request.ApplicationId
                        && candidate.Status == ApplicationStatus.Published,
                    transactionToken);
                if (application is null)
                {
                    return await StoreResultAsync(reservation,
                        new LaunchCommandResult(StatusCodes.Status404NotFound, "APPLICATION_NOT_FOUND", null), transactionToken);
                }

                if (!await authorizationService.CanLaunchAsync(user.Id, application.Id, now, transactionToken))
                {
                    var denied = NewLaunch(user, application, null, request, LaunchOutcome.DeniedPermission,
                        "permission not effective", sourceIp, now, correlationId);
                    dbContext.Launches.Add(denied);
                    return await StoreResultAsync(reservation,
                        new LaunchCommandResult(StatusCodes.Status403Forbidden, "PERMISSION_REVOKED", null), transactionToken);
                }

                var target = await sessionRegistry.PlaceAsync(application, user, now, transactionToken);
                if (target is null)
                {
                    dbContext.Launches.Add(NewLaunch(user, application, null, request, LaunchOutcome.DeniedHostUnavailable,
                        "no online host with capacity", sourceIp, now, correlationId));
                    return await StoreResultAsync(reservation,
                        new LaunchCommandResult(StatusCodes.Status422UnprocessableEntity, "APPLICATION_UNAVAILABLE", null), transactionToken);
                }

                try
                {
                    var policy = await policyResolver.ResolveAsync(application.Id, transactionToken);
                    var descriptor = descriptorBuilder.Build(application, target.Host, user, policy);
                    var certificate = await dbContext.SigningCertificates
                        .Where(candidate => candidate.Status == SigningCertificateStatus.Active
                            && candidate.ValidFrom <= now
                            && candidate.ValidTo > now)
                        .OrderByDescending(candidate => candidate.ActivatedAt)
                        .FirstOrDefaultAsync(transactionToken);

                    if (certificate is null)
                    {
                        throw new RdpSigningException("Não há certificado de assinatura válido configurado.");
                    }

                    var signedDescriptor = await signer.SignAsync(descriptor, certificate.Thumbprint, transactionToken);
                    var expiresAt = now.AddSeconds(60);
                    var sessionId = await sessionRegistry.RegisterAsync(
                        target, user, sourceIp, request.WorkstationName, now, transactionToken);
                    var launch = NewLaunch(user, application, sessionId, request, LaunchOutcome.Granted,
                        null, sourceIp, now, correlationId);
                    dbContext.Launches.Add(launch);
                    var response = new LaunchResponse(
                        launch.Id,
                        target.SessionReused,
                        Convert.ToBase64String(signedDescriptor),
                        expiresAt,
                        new LaunchHostResponse("Servidor de aplicativos"),
                        correlationId);
                    return await StoreResultAsync(reservation,
                        new LaunchCommandResult(StatusCodes.Status201Created, null, response), transactionToken);
                }
                catch (Exception exception) when (exception is RdpSigningException or RdpDescriptorException or RdpPolicyException)
                {
                    if (request.Purpose == LaunchPurpose.Prelaunch
                        && !target.SessionReused
                        && target.SessionId is Guid newSessionId)
                    {
                        try
                        {
                            await sessionBackend.CancelSessionAsync(newSessionId, "prelaunch_failed", transactionToken);
                            dbContext.AccessEvents.Add(new AccessEvent
                            {
                                TenantId = tenantContext.TenantId.Value,
                                UserAccountId = user.Id,
                                EventType = AccessEventType.SessionEnded,
                                Result = AccessEventResult.Success,
                                FailureReason = "prelaunch_failed",
                                SourceIp = sourceIp,
                                WorkstationName = request.WorkstationName,
                                OccurredAt = DateTimeOffset.UtcNow,
                                CorrelationId = correlationId
                            });
                        }
                        catch (RdsSessionException cancellationException)
                        {
                            logger.LogError(cancellationException,
                                "Não foi possível cancelar a sessão criada pelo prelaunch; correlationId {correlationId}", correlationId);
                        }
                    }

                    logger.LogError(exception, "Não foi possível preparar um descritor RDP; correlationId {correlationId}", correlationId);
                    var signingFailed = exception is RdpSigningException;
                    var errorCode = exception switch
                    {
                        RdpPolicyException => "EXCEPTION_REASON_REQUIRED",
                        RdpDescriptorException => "APPLICATION_UNAVAILABLE",
                        _ => "SIGNING_UNAVAILABLE"
                    };
                    var statusCode = errorCode switch
                    {
                        "SIGNING_UNAVAILABLE" => StatusCodes.Status503ServiceUnavailable,
                        _ => StatusCodes.Status422UnprocessableEntity
                    };
                    dbContext.Launches.Add(NewLaunch(user, application, target.SessionId, request,
                        signingFailed ? LaunchOutcome.ErrorSigning : LaunchOutcome.ErrorInternal,
                        "RDP descriptor generation or signing failed", sourceIp, now, correlationId));
                    return await StoreResultAsync(reservation,
                        new LaunchCommandResult(statusCode, errorCode, null), transactionToken);
                }
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            dbContext.ChangeTracker.Clear();
            var concurrentResult = await dbContext.LaunchIdempotencyRecords.SingleOrDefaultAsync(
                record => record.IdempotencyKey == idempotencyKey,
                cancellationToken);
            if (concurrentResult is null)
            {
                throw;
            }

            return await ExistingResultAsync(concurrentResult, requestHash, DateTimeOffset.UtcNow, cancellationToken);
        }
    }

    private async Task<LaunchCommandResult> ExistingResultAsync(
        LaunchIdempotencyRecord record,
        string requestHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(record.RequestHash),
                Encoding.ASCII.GetBytes(requestHash)))
        {
            return new LaunchCommandResult(StatusCodes.Status409Conflict, "IDEMPOTENCY_CONFLICT", null);
        }

        if (record.ExpiresAt <= now)
        {
            if (record.ResponseJson is not null)
            {
                record.ResponseJson = null;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return new LaunchCommandResult(StatusCodes.Status409Conflict, "IDEMPOTENCY_KEY_EXPIRED", null);
        }

        if (string.IsNullOrWhiteSpace(record.ResponseJson))
        {
            return new LaunchCommandResult(StatusCodes.Status409Conflict, "IDEMPOTENCY_IN_PROGRESS", null);
        }

        return JsonSerializer.Deserialize<LaunchCommandResult>(record.ResponseJson, JsonOptions)
            ?? new LaunchCommandResult(StatusCodes.Status409Conflict, "IDEMPOTENCY_IN_PROGRESS", null);
    }

    private async Task<LaunchCommandResult> StoreResultAsync(
        LaunchIdempotencyRecord reservation,
        LaunchCommandResult result,
        CancellationToken cancellationToken)
    {
        reservation.ResponseJson = JsonSerializer.Serialize(result, JsonOptions);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static Launch NewLaunch(
        UserAccount user,
        RemoteApplication application,
        Guid? sessionId,
        LaunchRequest request,
        LaunchOutcome outcome,
        string? denialReason,
        string sourceIp,
        DateTimeOffset requestedAt,
        Guid correlationId)
        => new()
        {
            TenantId = user.TenantId,
            UserAccountId = user.Id,
            ApplicationId = application.Id,
            SessionId = sessionId,
            Purpose = request.Purpose,
            RequestedAt = requestedAt,
            Outcome = outcome,
            DenialReason = denialReason,
            SourceIp = sourceIp,
            WorkstationName = request.WorkstationName,
            RdpExpiresAt = requestedAt.AddSeconds(60),
            CorrelationId = correlationId
        };

    private static string ComputeRequestHash(Guid userAccountId, LaunchRequest request)
    {
        // O usuário entra no hash: a chave de outro usuário do tenant não devolve o .rdp dele.
        var canonical = JsonSerializer.SerializeToUtf8Bytes(new
        {
            userAccountId,
            applicationId = request.ApplicationId,
            purpose = request.Purpose,
            workstationName = request.WorkstationName
        }, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(canonical));
    }

    private static bool IsIdempotencyConflict(DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uq_launch_idempotency_tenant_key"
        };
}
