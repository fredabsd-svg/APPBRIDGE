using Microsoft.Extensions.Logging;

namespace AppBridge.ControlPlane.Infrastructure.Auditing;

/// <summary>See <see cref="IAuditWriter"/>.</summary>
public sealed class AuditWriter(AppBridgeDbContext context, ILogger<AuditWriter> logger) : IAuditWriter
{
    public async Task<TResult> ExecuteAsync<TAudit, TResult>(
        TAudit auditEntry,
        Func<AppBridgeDbContext, TResult> grant,
        CancellationToken cancellationToken = default)
        where TAudit : class
    {
        context.Set<TAudit>().Add(auditEntry);

        TResult result;
        try
        {
            result = grant(context);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // ADR-0007 condition 2: recorded at minimum in the structured application log, even
            // though the very thing that failed is the database trail — this line is what survives
            // when the row doesn't.
            logger.LogError(
                ex,
                "Audit write failed for {AuditType}; the operation it would have accompanied was not committed",
                typeof(TAudit).Name);
            throw new AuditWriteFailedException(
                $"Failed to durably record a {typeof(TAudit).Name} audit entry; the accompanying operation was not committed.",
                ex);
        }

        return result;
    }

    public Task ExecuteAsync<TAudit>(
        TAudit auditEntry,
        Action<AppBridgeDbContext> grant,
        CancellationToken cancellationToken = default)
        where TAudit : class
        => ExecuteAsync<TAudit, object?>(
            auditEntry,
            ctx =>
            {
                grant(ctx);
                return null;
            },
            cancellationToken);
}
