using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Runtime.ExceptionServices;

namespace AppBridge.ControlPlane.Services;

/// <summary>Executa a alteração de segurança e a trilha no mesmo commit do PostgreSQL.</summary>
public sealed class AuditWriter(
    AppDbContext dbContext,
    TenantContext tenantContext,
    ILogger<AuditWriter> logger)
{
    public async Task<T> ExecuteAsync<T>(
        AccessEvent? auditEvent,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null)
        {
            throw new InvalidOperationException("A operação auditada exige TenantContext.");
        }

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction;
        try
        {
            transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Auditoria indisponível ao iniciar a transação.");
            throw new AuditUnavailableException(exception);
        }

        await using (transaction)
        {
            T result;
            try
            {
                result = await operation(cancellationToken);
            }
            catch (Exception exception)
            {
                await RollbackAsync(transaction);
                ExceptionDispatchInfo.Capture(exception).Throw();
                throw;
            }

            try
            {
                if (auditEvent is not null)
                {
                    auditEvent.TenantId = tenantContext.TenantId.Value;
                    dbContext.AccessEvents.Add(auditEvent);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                await RollbackAsync(transaction);
                logger.LogError(exception, "A transação auditada foi revertida.");
                throw new AuditUnavailableException(exception);
            }

            return result;
        }
    }

    private async Task RollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao reverter a operação auditada.");
        }
    }
}

public sealed class AuditUnavailableException(Exception innerException)
    : Exception("Não foi possível registrar a operação na trilha de auditoria.", innerException);
