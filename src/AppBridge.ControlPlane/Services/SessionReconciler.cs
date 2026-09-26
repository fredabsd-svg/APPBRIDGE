using System.Text.Json;
using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using AppBridge.ControlPlane.Launching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppBridge.ControlPlane.Services;

public sealed class SessionReconcilerOptions
{
    /// <summary>PRE-30: intervalo entre ciclos de reconciliação.</summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>PRE-31: sem resposta do backend, sessão sem sinal por este tempo é fechada como <c>stale_expired</c>.</summary>
    public int StaleAfterMinutes { get; set; } = 30;

    public TimeSpan Interval => TimeSpan.FromSeconds(Math.Clamp(IntervalSeconds, 15, 3600));
    public TimeSpan StaleAfter => TimeSpan.FromMinutes(Math.Clamp(StaleAfterMinutes, 5, 1440));
}

public sealed record SessionReconciliationResult(
    bool BackendAvailable,
    int Refreshed,
    int Bound,
    int Closed,
    int Unadopted);

/// <summary>Reconcilia as sessões abertas de um tenant com o backend (T-602, ADR-0022).</summary>
public sealed class SessionReconciliationService(
    AppDbContext dbContext,
    TenantContext tenantContext,
    ISessionBackend sessionBackend,
    IOptions<SessionRegistryOptions> registryOptions,
    IOptions<SessionReconcilerOptions> reconcilerOptions,
    ILogger<SessionReconciliationService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SessionReconciliationResult> ReconcileAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("A reconciliação exige TenantContext.");
        var hosts = await dbContext.SessionHosts.AsNoTracking().ToListAsync(cancellationToken);
        if (hosts.Count == 0)
        {
            return new SessionReconciliationResult(true, 0, 0, 0, 0);
        }

        // O backend é consultado fora da transação para não prender as travas dos lançamentos.
        IReadOnlyList<BackendSessionSnapshot>? snapshot;
        try
        {
            snapshot = await sessionBackend.ListActiveSessionsAsync(hosts, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "O backend de sessão não respondeu; só a expiração por inatividade será aplicada.");
            snapshot = null;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var userIds = await dbContext.Sessions
            .Where(session => session.EndedAt == null)
            .Select(session => session.UserAccountId)
            .Distinct()
            .OrderBy(userId => userId)
            .ToListAsync(cancellationToken);
        foreach (var userId in userIds)
        {
            var lockKey = SessionRegistry.UserLockKey(tenantId, userId);
            await dbContext.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken);
        }

        // Só usuários já travados: sessão aberta depois da lista fica para o próximo ciclo.
        var openSessions = await dbContext.Sessions
            .Where(session => session.EndedAt == null && userIds.Contains(session.UserAccountId))
            .OrderBy(session => session.StartedAt)
            .ToListAsync(cancellationToken);
        var hostKeys = hosts.ToDictionary(host => host.Id, host => RdsSessionListParser.NormalizeHost(host.Fqdn));
        var cycleId = Guid.CreateVersion7();
        var result = snapshot is null
            ? ExpireStale(openSessions, now, cycleId)
            : Reconcile(openSessions, snapshot, hostKeys, await LoadUserSidsAsync(userIds, cancellationToken), now, cycleId);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private SessionReconciliationResult Reconcile(
        List<RemoteSession> openSessions,
        IReadOnlyList<BackendSessionSnapshot> snapshot,
        Dictionary<Guid, string> hostKeys,
        Dictionary<Guid, string> userSids,
        DateTimeOffset now,
        Guid cycleId)
    {
        var live = snapshot
            .Select(session => (Host: RdsSessionListParser.NormalizeHost(session.HostFqdn), Session: session))
            .ToList();
        var claimed = new HashSet<(string Host, string Id)>();
        int refreshed = 0, bound = 0, closed = 0;

        foreach (var session in openSessions.Where(session => session.BackendSessionId is not null))
        {
            var key = (HostKey(hostKeys, session), session.BackendSessionId!);
            if (live.Any(item => item.Host == key.Item1 && item.Session.BackendSessionId == key.Item2))
            {
                claimed.Add(key);
                session.LastSeenAt = now;
                refreshed++;
            }
            else
            {
                Close(session, SessionEndReason.ReconciledMissing, "reconciled_missing", now, cycleId);
                closed++;
            }
        }

        var pendingCutoff = now - registryOptions.Value.PendingBindingWindow;
        foreach (var session in openSessions.Where(session => session.BackendSessionId is null))
        {
            var hostKey = HostKey(hostKeys, session);
            var match = userSids.TryGetValue(session.UserAccountId, out var sid)
                ? live
                    .Where(item => item.Host == hostKey
                        && string.Equals(item.Session.UserSid, sid, StringComparison.OrdinalIgnoreCase)
                        && !claimed.Contains((item.Host, item.Session.BackendSessionId)))
                    .OrderByDescending(item => item.Session.CreatedAt ?? DateTimeOffset.MinValue)
                    .Select(item => item.Session)
                    .FirstOrDefault()
                : null;

            if (match is not null)
            {
                claimed.Add((hostKey, match.BackendSessionId));
                session.BackendSessionId = match.BackendSessionId;
                session.LastSeenAt = now;
                AddEvent(session, AccessEventType.SessionStarted, null, now, cycleId, null);
                bound++;
            }
            else if (session.LastSeenAt <= pendingCutoff)
            {
                Close(session, SessionEndReason.ReconciledMissing, "never_connected", now, cycleId);
                closed++;
            }
        }

        var hostSet = hostKeys.Values.ToHashSet(StringComparer.Ordinal);
        var unadopted = live.Count(item => hostSet.Contains(item.Host) && !claimed.Contains((item.Host, item.Session.BackendSessionId)));
        if (unadopted > 0)
        {
            logger.LogInformation("Sessões no backend sem registro no AppBridge, não adotadas: {Count}.", unadopted);
        }

        return new SessionReconciliationResult(true, refreshed, bound, closed, unadopted);
    }

    private SessionReconciliationResult ExpireStale(List<RemoteSession> openSessions, DateTimeOffset now, Guid cycleId)
    {
        var staleCutoff = now - reconcilerOptions.Value.StaleAfter;
        var closed = 0;
        foreach (var session in openSessions.Where(session => session.LastSeenAt <= staleCutoff))
        {
            Close(session, SessionEndReason.StaleExpired, "stale_expired", now, cycleId);
            closed++;
        }

        return new SessionReconciliationResult(false, 0, 0, closed, 0);
    }

    private async Task<Dictionary<Guid, string>> LoadUserSidsAsync(List<Guid> userIds, CancellationToken cancellationToken)
        => await dbContext.UserAccounts
            .Where(user => userIds.Contains(user.Id) && user.AdObjectSid != "")
            .ToDictionaryAsync(user => user.Id, user => user.AdObjectSid, cancellationToken);

    private void Close(RemoteSession session, SessionEndReason reason, string auditReason, DateTimeOffset now, Guid cycleId)
    {
        session.EndedAt = now;
        session.EndReason = reason;
        AddEvent(session, AccessEventType.SessionEnded, auditReason, now, cycleId,
            (long)Math.Max(0, (now - session.StartedAt).TotalSeconds));
    }

    private void AddEvent(
        RemoteSession session,
        AccessEventType eventType,
        string? reason,
        DateTimeOffset now,
        Guid cycleId,
        long? durationSeconds)
    {
        var payload = JsonSerializer.SerializeToDocument(new
        {
            sessionId = session.Id,
            sessionHostId = session.SessionHostId,
            endReason = reason,
            durationSeconds
        }, JsonOptions);
        dbContext.AccessEvents.Add(new AccessEvent
        {
            TenantId = session.TenantId,
            UserAccountId = session.UserAccountId,
            EventType = eventType,
            Result = AccessEventResult.Success,
            FailureReason = reason,
            SourceIp = session.SourceIp,
            WorkstationName = session.WorkstationName,
            OccurredAt = now,
            CorrelationId = cycleId,
            Payload = payload
        });
    }

    private static string HostKey(Dictionary<Guid, string> hostKeys, RemoteSession session)
        => hostKeys.TryGetValue(session.SessionHostId, out var key) ? key : string.Empty;
}

/// <summary>Executa a reconciliação periodicamente, tenant a tenant, só para tenants ativos.</summary>
public sealed class SessionReconciler(
    IServiceScopeFactory scopeFactory,
    IOptions<SessionReconcilerOptions> options,
    ILogger<SessionReconciler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval);
        do
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "O ciclo de reconciliação de sessões falhou.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        List<Guid> tenantIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            // Sem TenantContext o filtro global esconde tudo; a lista de tenants é a única leitura transversal.
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            tenantIds = await dbContext.Database
                .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM tenant WHERE status = 'active' AND deleted_at IS NULL ORDER BY id")
                .ToListAsync(cancellationToken);
        }

        foreach (var tenantId in tenantIds)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<TenantContext>().Bind(tenantId);
                var result = await scope.ServiceProvider.GetRequiredService<SessionReconciliationService>()
                    .ReconcileAsync(DateTimeOffset.UtcNow, cancellationToken);
                if (result.Bound + result.Closed > 0)
                {
                    logger.LogInformation(
                        "Reconciliação do tenant {TenantId}: {Bound} vinculadas, {Closed} fechadas, backend disponível: {BackendAvailable}.",
                        tenantId, result.Bound, result.Closed, result.BackendAvailable);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Um tenant com falha não impede os demais; o próximo ciclo tenta de novo.
                logger.LogError(exception, "A reconciliação de sessões do tenant {TenantId} falhou.", tenantId);
            }
        }
    }
}
