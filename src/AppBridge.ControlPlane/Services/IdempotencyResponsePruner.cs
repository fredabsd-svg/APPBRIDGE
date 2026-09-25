using AppBridge.ControlPlane.Data;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Services;

/// <summary>Apaga o RDP assinado expirado; a chave fica como tombstone para negar reapresentação.</summary>
public sealed class IdempotencyResponsePruner(
    IServiceScopeFactory scopeFactory,
    ILogger<IdempotencyResponsePruner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // Tarefa de serviço explícita: limpa cache temporário de todos os tenants sem ler seus dados.
                var cleared = await dbContext.Database.ExecuteSqlRawAsync("""
                    UPDATE launch_idempotency
                    SET response_json = NULL,
                        updated_at = CURRENT_TIMESTAMP,
                        updated_by = '00000000-0000-0000-0000-000000000000',
                        row_version = row_version + 1
                    WHERE expires_at <= CURRENT_TIMESTAMP
                      AND response_json IS NOT NULL
                    """, stoppingToken);
                if (cleared > 0)
                {
                    logger.LogInformation("Respostas de idempotência expiradas removidas: {rowCount}.", cleared);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Não foi possível remover respostas de idempotência expiradas.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
