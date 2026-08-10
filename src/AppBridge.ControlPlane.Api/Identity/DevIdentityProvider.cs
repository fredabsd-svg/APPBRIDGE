using AppBridge.ControlPlane.Infrastructure.Identity;

namespace AppBridge.ControlPlane.Api.Identity;

/// <summary>
/// Stands in for real Entra ID/AD DS validation (ADR-0017 §5) so <c>POST /v1/auth/session</c> can
/// be run and tested locally in this sandbox, where no such infrastructure exists yet — E-01 hasn't
/// bought hardware, there is no AD DS domain or Entra tenant to validate against. Registered
/// <b>only</b> under <c>Development</c> (<c>Program.cs</c>); if this ever runs anywhere else, the
/// consequence is that any string of the right shape authenticates as anyone — see R-032.
///
/// Accepts <c>"dev:{externalSubject}:{adDomain}"</c>, deliberately not "anything non-empty", so a
/// test can't mistake this for permissive-by-default behavior. An optional
/// <c>":{upn}:{displayName}"</c> suffix (T-302) stands in for a fresh directory read, letting a
/// test simulate an AD rename between two logins of the same identity without touching the
/// three-part form every earlier test already uses.
/// </summary>
public sealed class DevIdentityProvider : IIdentityProvider
{
    public Task<IdentityValidationResult> ValidateAsync(string identityToken, CancellationToken cancellationToken = default)
    {
        var parts = identityToken.Split(':', 5);
        if (parts.Length < 3
            || parts[0] != "dev"
            || string.IsNullOrWhiteSpace(parts[1])
            || string.IsNullOrWhiteSpace(parts[2]))
        {
            return Task.FromResult(IdentityValidationResult.Invalid());
        }

        var upn = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3] : null;
        var displayName = parts.Length > 4 && !string.IsNullOrWhiteSpace(parts[4]) ? parts[4] : null;

        return Task.FromResult(IdentityValidationResult.Valid(
            externalSubject: parts[1], adDomain: parts[2], upn: upn, displayName: displayName));
    }
}
