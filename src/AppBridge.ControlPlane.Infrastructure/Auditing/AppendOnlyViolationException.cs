using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Infrastructure.Auditing;

/// <summary>
/// T-701 (RNF-019): thrown when application code tries to change or remove a trail row —
/// <c>Launch</c>, <c>AccessEvent</c>, <c>PurgeRun</c>, anything deriving from
/// <c>AppendOnlyEntity</c> — through the ORM's normal entity-tracking path.
/// <see cref="AppBridgeDbContext.SaveChanges(bool)"/> throws this before any SQL runs, so a bug
/// that tries to "fix" a trail row in place fails loudly instead of silently succeeding.
///
/// The only legitimate removal is retention purge (T-703, ADR-0007): "expurgo só ocorre por
/// política de retenção... executado por rotina própria." That has to use a bulk operation
/// (<c>ExecuteDeleteAsync</c> or raw SQL) instead of loading rows into the change tracker and
/// calling <c>Remove</c> — a different code path this guard never observes, by construction, not
/// a bypass flag a future caller could leave on by mistake.
/// </summary>
public sealed class AppendOnlyViolationException(string entityTypeName, EntityState attemptedState)
    : Exception(
        $"{entityTypeName} is append-only (RNF-019) and cannot be " +
        $"{(attemptedState == EntityState.Deleted ? "deleted" : "updated")} by application code.");
