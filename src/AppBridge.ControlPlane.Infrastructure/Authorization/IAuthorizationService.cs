namespace AppBridge.ControlPlane.Infrastructure.Authorization;

/// <summary>
/// The single decision point for "este usuário pode lançar este aplicativo agora?" (RF-007,
/// RF-021, RF-039 — ARQUITETURA.md §5.2, componente <c>AuthorizationService</c>). Vigência is
/// read straight from <c>ApplicationPermission.EffectiveFrom</c>/<c>EffectiveTo</c>
/// (MODELO-DE-DADOS.md §5.2 — revogação nunca apaga a linha, apenas fecha
/// <c>EffectiveTo</c>), with no caching layer, so a revoked permission denies the very next
/// check: that immediacy is what makes RNF-030 ("permissão revogada nega o lançamento seguinte
/// em ≤ 60 s") true by construction rather than by a TTL tuned to fit under 60 s.
///
/// Tenant isolation is not re-implemented here: both <c>UserGroupMemberships</c> and
/// <c>ApplicationPermissions</c> are already tenant-scoped <c>DbSet</c>s (ADR-0004, T-203), so a
/// cross-tenant lookup returns nothing without this service doing anything special about it.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// True when <paramref name="userAccountId"/> belongs to a group holding a currently
    /// effective <see cref="Domain.Catalog.ApplicationPermission"/> for
    /// <paramref name="applicationId"/> — i.e. <c>EffectiveFrom</c> has passed and
    /// <c>EffectiveTo</c> is either unset or still in the future.
    /// </summary>
    Task<bool> HasActivePermissionAsync(
        Guid userAccountId, Guid applicationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// T-402: the bulk form <c>CatalogService</c> needs to filter a list instead of gating a single
    /// launch — ARQUITETURA.md §4 draws <c>CatalogService --&gt; AuthorizationService</c> for exactly
    /// this reason, so the vigência window (same rule as <see cref="HasActivePermissionAsync"/>)
    /// lives in one place rather than being re-implemented per caller. The catalog endpoint still
    /// applies its own <c>Application.Status</c> gate on top — an application can be authorized and
    /// still not <c>Published</c>.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetAuthorizedApplicationIdsAsync(
        Guid userAccountId, CancellationToken cancellationToken = default);
}
