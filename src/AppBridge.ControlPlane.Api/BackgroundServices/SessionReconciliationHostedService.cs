using AppBridge.ControlPlane.Infrastructure.Sessions;

namespace AppBridge.ControlPlane.Api.BackgroundServices;

/// <summary>
/// T-602: runs <see cref="ISessionReconciler.ReconcileStaleSessionsAsync"/> on a timer — the
/// "periodic" half of "reconciliação periódica" (ADR-0006's own phrase for this kind of sweep) that
/// the <c>stale_expired</c> defence needs to mean anything; a reconciler nobody ever calls closes
/// nothing.
///
/// <c>ISessionReconciler</c> is Scoped (it depends on the Scoped <c>AppBridgeDbContext</c> and
/// <c>TenantContext</c> — the same DI shape every request-scoped service in this codebase uses), so
/// each tick opens its own <see cref="IServiceScope"/> via <see cref="IServiceScopeFactory"/>,
/// exactly the pattern ASP.NET Core's own docs prescribe for a <see cref="BackgroundService"/>
/// consuming Scoped dependencies — there is no HTTP request here to scope one automatically.
///
/// One tick throwing must not stop the next one: a transient database blip closing zero sessions
/// this cycle is a rounding error against a 12-hour window (<see cref="SessionReconcilerOptions"/>);
/// the loop silently dying would mean nothing is ever reconciled again until the process restarts.
/// </summary>
public sealed class SessionReconciliationHostedService(
    IServiceScopeFactory scopeFactory, ILogger<SessionReconciliationHostedService> logger)
    : BackgroundService
{
    /// <summary>
    /// <c>PREMISSA</c> (STATUS.md PRE-33): 5 minutes — frequent enough that "em até um ciclo"
    /// (T-602's acceptance criterion) means something close to real time, cheap enough that a sweep
    /// over the whole <c>session</c> table five times an hour costs nothing at dogfood scale.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<ISessionReconciler>();
                var closed = await reconciler.ReconcileStaleSessionsAsync(stoppingToken);
                if (closed > 0)
                {
                    logger.LogInformation("Session reconciliation closed {Count} stale session(s)", closed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Session reconciliation cycle failed; will retry next cycle");
            }
        }
    }
}
