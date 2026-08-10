namespace AppBridge.ControlPlane.Infrastructure.Identity;

/// <summary>
/// Validates the token the launcher presents after authenticating against Entra ID/AD DS (ADR-0001)
/// and resolves the two things the login endpoint needs before it can do anything else: who the
/// caller claims to be, and which AppBridge tenant that identity belongs to.
///
/// ADR-0017 §5: no implementation ships in this project for MVP-0 — there is no Entra tenant or
/// AD DS domain to validate against yet (E-01 infrastructure isn't built). The one implementation
/// that exists (<c>DevIdentityProvider</c>, Api project) is registered only under
/// <c>Development</c> and must never be mistaken for the real thing.
/// </summary>
public interface IIdentityProvider
{
    Task<IdentityValidationResult> ValidateAsync(string identityToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// <paramref name="AdDomain"/> resolves which AppBridge <c>Tenant</c> the attempt is for
/// (<c>Tenant.AdDomain</c>) — independent of whether <paramref name="ExternalSubject"/> matches a
/// provisioned <c>UserAccount</c>, so an unknown-user failure can still be attributed to a tenant
/// for the audit trail (MODELO-DE-DADOS.md §7.2's "user_account_id nulo é intencional").
///
/// <paramref name="Upn"/> and <paramref name="DisplayName"/> are the directory's current values
/// for those fields — read fresh on every validation, not cached — so a login can refresh a
/// <c>UserAccount</c> row whose AD-side presentation attributes drifted since the last one (T-302,
/// RF-002). Null means the provider didn't resolve one (e.g. <see cref="DevIdentityProvider"/>'s
/// short token form); a caller must not treat null as "clear the stored value".
/// </summary>
public sealed record IdentityValidationResult(
    bool IsValid, string? ExternalSubject, string? AdDomain, string? Upn = null, string? DisplayName = null)
{
    public static IdentityValidationResult Valid(
        string externalSubject, string adDomain, string? upn = null, string? displayName = null) =>
        new(true, externalSubject, adDomain, upn, displayName);

    public static IdentityValidationResult Invalid() => new(false, null, null);
}
