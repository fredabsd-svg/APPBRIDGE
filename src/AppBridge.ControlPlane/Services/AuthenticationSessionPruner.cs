using AppBridge.ControlPlane.Data;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Services;

/// <summary>Remove sessões sem validade há 30 dias e os hashes usados para detectar replay.</summary>
public sealed class AuthenticationSessionPruner(
    IServiceScopeFactory scopeFactory,
    ILogger<AuthenticationSessionPruner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var removed = await dbContext.Database.ExecuteSqlRawAsync("""
                    DELETE FROM auth_session
                    WHERE absolute_expires_at <= CURRENT_TIMESTAMP - INTERVAL '30 days'
                       OR revoked_at <= CURRENT_TIMESTAMP - INTERVAL '30 days'
                    """, stoppingToken);
                if (removed > 0)
                {
                    logger.LogInformation("Sessões AppBridge antigas removidas: {rowCount}.", removed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Não foi possível remover sessões AppBridge antigas.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
