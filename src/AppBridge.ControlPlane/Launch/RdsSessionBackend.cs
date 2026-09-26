using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppBridge.ControlPlane.Launching;

public sealed class RdsSessionBackend(
    AppDbContext dbContext,
    IOptions<RdsSessionOptions> options,
    ILogger<RdsSessionBackend> logger) : ISessionBackend
{
    public async Task<SessionBackendTarget?> ResolveHostAsync(
        RemoteApplication application,
        UserAccount user,
        CancellationToken cancellationToken)
    {
        var existing = await (
            from session in dbContext.Sessions
            join host in dbContext.SessionHosts on new { session.TenantId, session.SessionHostId } equals new { host.TenantId, SessionHostId = host.Id }
            where session.UserAccountId == user.Id
                && session.EndedAt == null
                && host.HostPoolId == application.HostPoolId
                && host.Status == SessionHostStatus.Online
            orderby session.LastSeenAt descending
            select new { Host = host, Session = session })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return new SessionBackendTarget(existing.Host, true, existing.Session.Id);
        }

        var candidate = await dbContext.SessionHosts
            .Where(host => host.HostPoolId == application.HostPoolId
                && host.Status == SessionHostStatus.Online
                && dbContext.Sessions.Count(session => session.SessionHostId == host.Id && session.EndedAt == null) < host.MaxSessions)
            .OrderBy(host => dbContext.Sessions.Count(session => session.SessionHostId == host.Id && session.EndedAt == null))
            .ThenBy(host => host.Fqdn)
            .FirstOrDefaultAsync(cancellationToken);

        return candidate is null ? null : new SessionBackendTarget(candidate, false, null);
    }

    public async Task CancelSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions.SingleOrDefaultAsync(
            row => row.Id == sessionId && row.EndedAt == null,
            cancellationToken);
        if (session is null)
        {
            return;
        }

        var host = await dbContext.SessionHosts.SingleOrDefaultAsync(
            row => row.Id == session.SessionHostId,
            cancellationToken);
        if (host is null || !Regex.IsMatch(host.Fqdn, "^[A-Za-z0-9.-]{1,253}\\z", RegexOptions.CultureInvariant))
        {
            throw new RdsSessionException("O host da sessão RDS não é válido.");
        }

        if (!int.TryParse(session.BackendSessionId, NumberStyles.None, CultureInfo.InvariantCulture, out var unifiedSessionId))
        {
            throw new RdsSessionException("O identificador da sessão RDS não é válido.");
        }

        await InvokeLogoffAsync(host.Fqdn, unifiedSessionId, cancellationToken);
        session.EndedAt = DateTimeOffset.UtcNow;
        session.LastSeenAt = session.EndedAt.Value;
        session.EndReason = SessionEndReason.Logoff;
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("A sessão RDS foi encerrada após a falha de lançamento.");
    }

    private async Task InvokeLogoffAsync(string hostName, int unifiedSessionId, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new RdsSessionException("O backend RDS exige Windows Server.");
        }

        var command = $"Import-Module RemoteDesktop; Invoke-RDUserLogoff -HostServer '{hostName}' -UnifiedSessionID {unifiedSessionId.ToString(CultureInfo.InvariantCulture)} -Force -ErrorAction Stop";
        var startInfo = new ProcessStartInfo
        {
            FileName = options.Value.PowerShellPath ?? "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(command);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Value.CommandTimeoutSeconds, 1, 60)));
        using var process = Process.Start(startInfo)
            ?? throw new RdsSessionException("Não foi possível iniciar o comando de administração RDS.");
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(stdout, stderr);
        }
        catch (OperationCanceledException exception)
        {
            TryKill(process);
            throw new RdsSessionException("O encerramento de sessão RDS expirou.", exception);
        }

        if (process.ExitCode != 0)
        {
            logger.LogError("O comando de encerramento RDS falhou com código {ExitCode}.", process.ExitCode);
            throw new RdsSessionException("Não foi possível encerrar a sessão RDS.");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // O processo já encerrou.
        }
    }
}

public sealed class RdsSessionException(string message, Exception? innerException = null)
    : Exception(message, innerException);
