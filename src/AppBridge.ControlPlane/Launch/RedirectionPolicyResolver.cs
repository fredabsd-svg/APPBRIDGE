using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Launching;

public sealed class RedirectionPolicyResolver(AppDbContext dbContext)
{
    public async Task<RedirectionPolicy> ResolveAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var baseline = await dbContext.RedirectionPolicies
            .SingleOrDefaultAsync(policy => policy.ApplicationId == null, cancellationToken)
            ?? DefaultPolicy();
        var applicationPolicy = await dbContext.RedirectionPolicies
            .SingleOrDefaultAsync(policy => policy.ApplicationId == applicationId, cancellationToken);

        if (applicationPolicy is null)
        {
            return baseline;
        }

        if (WidensPolicy(baseline, applicationPolicy) && string.IsNullOrWhiteSpace(applicationPolicy.ExceptionReason))
        {
            throw new RdpPolicyException("A política por aplicativo amplia a política base sem justificativa.");
        }

        return applicationPolicy;
    }

    private static RedirectionPolicy DefaultPolicy() => new()
    {
        AllowPrinter = true,
        AllowSmartcard = true,
        AllowClipboard = true,
        AllowAudioOut = true,
        AllowDrives = false,
        AllowSerialPorts = false,
        AllowAudioIn = false,
        AllowOtherUsb = false
    };

    private static bool WidensPolicy(RedirectionPolicy baseline, RedirectionPolicy candidate)
        => (!baseline.AllowPrinter && candidate.AllowPrinter)
            || (!baseline.AllowSmartcard && candidate.AllowSmartcard)
            || (!baseline.AllowClipboard && candidate.AllowClipboard)
            || (!baseline.AllowAudioOut && candidate.AllowAudioOut)
            || (!baseline.AllowDrives && candidate.AllowDrives)
            || (!baseline.AllowSerialPorts && candidate.AllowSerialPorts)
            || (!baseline.AllowAudioIn && candidate.AllowAudioIn)
            || (!baseline.AllowOtherUsb && candidate.AllowOtherUsb);
}

public sealed class RdpPolicyException(string message) : Exception(message);
