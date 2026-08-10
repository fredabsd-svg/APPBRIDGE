using AppBridge.ControlPlane.Domain.Identity;

namespace AppBridge.ControlPlane.Infrastructure.Identity;

/// <summary>Issues the AppBridge-signed access token (ADR-0017 §1) — the Control Plane's own session token, distinct from the identity token validated by <see cref="IIdentityProvider"/>.</summary>
public interface ISessionTokenIssuer
{
    SessionToken IssueAccessToken(UserAccount user, Guid tenantId, IReadOnlyList<string> roles);
}

public sealed record SessionToken(string Value, DateTimeOffset ExpiresAt);
