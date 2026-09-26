using System.Buffers.Binary;
using System.Linq.Expressions;
using System.Security.Cryptography;
using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using AppBridge.ControlPlane.Launching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppBridge.ControlPlane.Services;

public sealed class SessionRegistryOptions
{
    /// <summary>PRE-29: tempo que uma sessão ainda sem vínculo no RDS ocupa vaga (ADR-0021).</summary>
    public int PendingBindingMinutes { get; set; } = 10;

    public TimeSpan PendingBindingWindow => TimeSpan.FromMinutes(Math.Clamp(PendingBindingMinutes, 1, 60));
}

/// <summary>Ciclo de vida da sessão no lançamento: reutilização, registro e vínculo (T-601, ADR-0021).</summary>
public sealed class SessionRegistry(
    AppDbContext dbContext,
    TenantContext tenantContext,
    ISessionBackend sessionBackend,
    IOptions<SessionRegistryOptions> options)
{
    /// <summary>
    /// Sessão que conta para reutilização e capacidade: aberta e já vinculada ao RDS, ou pendente e
    /// ainda dentro da janela de vínculo.
    /// </summary>
    public static Expression<Func<RemoteSession, bool>> Occupying(DateTimeOffset pendingCutoff)
        => session => session.EndedAt == null
            && (session.BackendSessionId != null || session.LastSeenAt > pendingCutoff);

    public async Task<SessionBackendTarget?> PlaceAsync(
        RemoteApplication application,
        UserAccount user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("O registro de sessão exige TenantContext.");
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("O registro de sessão exige a transação do lançamento.");
        }

        // Prelaunch e primeiro clique chegam juntos; o segundo espera e reutiliza a sessão do primeiro.
        var lockKey = PlacementLockKey(tenantId, user.Id);
        await dbContext.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken);

        var pendingCutoff = now - options.Value.PendingBindingWindow;
        var existing = await (
            from session in dbContext.Sessions.Where(Occupying(pendingCutoff))
            join host in dbContext.SessionHosts
                on new { session.TenantId, session.SessionHostId } equals new { host.TenantId, SessionHostId = host.Id }
            where session.UserAccountId == user.Id
                && host.HostPoolId == application.HostPoolId
                && host.Status != SessionHostStatus.Offline
            orderby session.LastSeenAt descending
            select new { Host = host, SessionId = session.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            // Host em drenagem não recebe sessão nova, mas a sessão existente continua sendo do usuário.
            return new SessionBackendTarget(existing.Host, true, existing.SessionId);
        }

        return await sessionBackend.ResolveHostAsync(application, user, cancellationToken);
    }

    /// <summary>Registra a sessão do lançamento concedido, na mesma transação do <c>launch</c>.</summary>
    public async Task<Guid> RegisterAsync(
        SessionBackendTarget target,
        UserAccount user,
        string sourceIp,
        string workstationName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (target.SessionId is Guid sessionId)
        {
            var session = await dbContext.Sessions.SingleAsync(row => row.Id == sessionId, cancellationToken);
            if (session.BackendSessionId is null && session.LastSeenAt < now)
            {
                // Reúso é sinal de vida da sessão pendente; a vinculada só a reconciliação atualiza.
                session.LastSeenAt = now;
            }

            return sessionId;
        }

        var registered = new RemoteSession
        {
            TenantId = user.TenantId,
            UserAccountId = user.Id,
            SessionHostId = target.Host.Id,
            BackendSessionId = null,
            StartedAt = now,
            LastSeenAt = now,
            SourceIp = sourceIp,
            WorkstationName = workstationName
        };
        dbContext.Sessions.Add(registered);
        return registered.Id;
    }

    private static long PlacementLockKey(Guid tenantId, Guid userAccountId)
    {
        var prefix = "appbridge:session-placement:"u8;
        Span<byte> material = stackalloc byte[prefix.Length + 32];
        prefix.CopyTo(material);
        tenantId.TryWriteBytes(material.Slice(prefix.Length, 16));
        userAccountId.TryWriteBytes(material.Slice(prefix.Length + 16, 16));
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(material, hash);
        return BinaryPrimitives.ReadInt64LittleEndian(hash);
    }
}
