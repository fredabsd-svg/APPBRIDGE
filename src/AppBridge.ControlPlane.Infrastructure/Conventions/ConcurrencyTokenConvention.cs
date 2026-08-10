using AppBridge.ControlPlane.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AppBridge.ControlPlane.Infrastructure.Conventions;

/// <summary>
/// Maps every <see cref="AuditedEntity.RowVersion"/> onto PostgreSQL's own <c>xmin</c> system
/// column instead of a hand-rolled counter — one less place for increment logic to be wrong, and
/// exactly what the Npgsql provider recommends for optimistic concurrency. Runs AFTER
/// <see cref="SnakeCaseNamingConvention"/>, which is why this is a second pass and not folded
/// into the same loop: the blanket rename would otherwise turn "xmin" into "row_version" and
/// silently break the mapping to the real system column.
/// </summary>
public static class ConcurrencyTokenConvention
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersion = entityType.FindProperty(nameof(AuditedEntity.RowVersion));
            if (rowVersion is null)
            {
                continue;
            }

            rowVersion.SetColumnName("xmin");
            rowVersion.SetColumnType("xid");
            rowVersion.ValueGenerated = ValueGenerated.OnAddOrUpdate;
            rowVersion.IsConcurrencyToken = true;
        }
    }
}
