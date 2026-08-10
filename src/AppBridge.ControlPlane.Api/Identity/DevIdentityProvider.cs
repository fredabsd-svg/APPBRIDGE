using AppBridge.ControlPlane.Infrastructure.Identity;

namespace AppBridge.ControlPlane.Api.Identity;

/// <summary>
/// Stands in for real Entra ID/AD DS validation (ADR-0017 §5) so <c>POST /v1/auth/session</c> can
/// be run and tested locally in this sandbox, where no such infrastructure exists yet — E-01 hasn't
/// bought hardware, there is no AD DS domain or Entra tenant to validate against. Registered
/// <b>only</b> under <c>Development</c> (<c>Program.cs</c>); if this ever runs anywhere else, the
/// consequence is that any string of the right shape authenticates as anyone — see R-032.
///
/// Accepts only <c>"dev:{externalSubject}:{adDomain}"</c>, deliberately not "anything non-empty",
/// so a test can't mistake this for permissive-by-default behavior.
/// </summary>
public sealed class DevIdentityProvider : IIdentityProvider
{
    public Task<IdentityValidationResult> ValidateAsync(string identityToken, CancellationToken cancellationToken = default)
    {
        var parts = identityToken.Split(':', 3);
        if (parts.Length != 3
            || parts[0] != "dev"
            || string.IsNullOrWhiteSpace(parts[1])
            || string.IsNullOrWhiteSpace(parts[2]))
        {
            return Task.FromResult(IdentityValidationResult.Invalid());
        }

        return Task.FromResult(IdentityValidationResult.Valid(externalSubject: parts[1], adDomain: parts[2]));
    }
}
