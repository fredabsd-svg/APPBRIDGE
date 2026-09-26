using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using AppBridge.ControlPlane.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppBridge.ControlPlane.Launching;

public sealed class RdsSessionBackend(
    AppDbContext dbContext,
    IOptions<RdsSessionOptions> options,
    IOptions<SessionRegistryOptions> registryOptions,
    ILogger<RdsSessionBackend> logger) : ISessionBackend
{
    private static readonly Regex HostNamePattern = new(
        "^(?=.{1,253}\\z)[A-Za-z0-9](?:[A-Za-z0-9.-]*[A-Za-z0-9])?\\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<SessionBackendTarget?> ResolveHostAsync(
        RemoteApplication application,
        UserAccount user,
        CancellationToken cancellationToken)
    {
        // A reutilização é regra do SessionRegistry (ADR-0021); aqui só se escolhe o host.
        var hosts = await dbContext.SessionHosts
            .Where(host => host.HostPoolId == application.HostPoolId && host.Status == SessionHostStatus.Online)
            .ToListAsync(cancellationToken);
        if (hosts.Count == 0)
        {
            return null;
        }

        var hostIds = hosts.Select(host => host.Id).ToList();
        var pendingCutoff = DateTimeOffset.UtcNow - registryOptions.Value.PendingBindingWindow;
        var load = await dbContext.Sessions
            .Where(SessionRegistry.Occupying(pendingCutoff))
            .Where(session => hostIds.Contains(session.SessionHostId))
            .GroupBy(session => session.SessionHostId)
            .Select(group => new { HostId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.HostId, row => row.Count, cancellationToken);

        var candidate = hosts
            .Select(host => new { Host = host, Load = load.GetValueOrDefault(host.Id) })
            .Where(row => row.Load < row.Host.MaxSessions)
            .OrderBy(row => row.Load)
            .ThenBy(row => row.Host.Fqdn, StringComparer.Ordinal)
            .Select(row => row.Host)
            .FirstOrDefault();

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

        if (session.BackendSessionId is null)
        {
            // Sessão ainda sem vínculo no RDS: não há o que encerrar no host (ADR-0021).
            CloseSession(session);
            await dbContext.SaveChangesAsync(cancellationToken);
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
        CloseSession(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("A sessão RDS foi encerrada após a falha de lançamento.");
    }

    private static void CloseSession(RemoteSession session)
    {
        session.EndedAt = DateTimeOffset.UtcNow;
        session.LastSeenAt = session.EndedAt.Value;
        session.EndReason = SessionEndReason.Logoff;
    }

    public async Task<IReadOnlyList<BackendSessionSnapshot>> ListActiveSessionsAsync(
        IReadOnlyCollection<SessionHost> hosts,
        CancellationToken cancellationToken)
    {
        var broker = options.Value.ConnectionBroker;
        if (string.IsNullOrWhiteSpace(broker) || !HostNamePattern.IsMatch(broker))
        {
            throw new RdsSessionException("RdsSession:ConnectionBroker não está configurado com um FQDN válido.");
        }

        // A conta vira SID no próprio Windows do Control Plane: a comparação nunca é por nome (ADR-0022).
        var command = $$"""
            Import-Module RemoteDesktop;
            $rows = @(Get-RDUserSession -ConnectionBroker '{{broker}}' -ErrorAction Stop | ForEach-Object {
                $sid = $null
                try { $sid = (New-Object System.Security.Principal.NTAccount($_.DomainName, $_.UserName)).Translate([System.Security.Principal.SecurityIdentifier]).Value } catch { }
                $created = $null
                if ($_.CreateTime) { $created = $_.CreateTime.ToUniversalTime().ToString('o') }
                [pscustomobject]@{ host = [string]$_.HostServer; id = [string]$_.UnifiedSessionId; sid = $sid; created = $created }
            });
            ConvertTo-Json -Compress -InputObject $rows
            """;
        var output = await RunPowerShellAsync(command, "consulta de sessões RDS", cancellationToken);
        var hostNames = hosts.Select(host => RdsSessionListParser.NormalizeHost(host.Fqdn)).ToHashSet(StringComparer.Ordinal);
        return RdsSessionListParser.Parse(output)
            .Where(session => hostNames.Contains(RdsSessionListParser.NormalizeHost(session.HostFqdn)))
            .ToList();
    }

    private Task InvokeLogoffAsync(string hostName, int unifiedSessionId, CancellationToken cancellationToken)
        => RunPowerShellAsync(
            $"Import-Module RemoteDesktop; Invoke-RDUserLogoff -HostServer '{hostName}' -UnifiedSessionID {unifiedSessionId.ToString(CultureInfo.InvariantCulture)} -Force -ErrorAction Stop",
            "encerramento de sessão RDS",
            cancellationToken);

    private async Task<string> RunPowerShellAsync(string command, string operation, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new RdsSessionException("O backend RDS exige Windows Server.");
        }

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
            throw new RdsSessionException($"A {operation} expirou.", exception);
        }

        if (process.ExitCode != 0)
        {
            logger.LogError("O comando de {Operation} falhou com código {ExitCode}.", operation, process.ExitCode);
            throw new RdsSessionException($"Não foi possível concluir a {operation}.");
        }

        return await stdout;
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
