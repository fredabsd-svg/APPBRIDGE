using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Infrastructure.Conventions;

/// <summary>
/// Renames every column to snake_case (ADR-0011 §6) after all <c>IEntityTypeConfiguration</c>
/// classes have run, so no configuration needs to spell out <c>HasColumnName</c> for every
/// property by hand. Table names are NOT touched here — each configuration sets its own via
/// <c>ToTable(...)</c>, matching MODELO-DE-DADOS.md exactly rather than trusting an automated
/// pluralization/singularization pass to guess it correctly.
///
/// Constraint and index names are deliberately NOT auto-converted: EF Core's defaults
/// ("PK_Tenant", "FK_Session_Tenant_TenantId") don't snake-case cleanly with a naive per-character
/// pass (e.g. "PK_" would become "p_k_"), and ADR-0011 §6 specifies real prefixes
/// (<c>ix_</c>/<c>uq_</c>/<c>fk_</c>/<c>ck_</c>) that only make sense named explicitly, which the
/// configurations that need one do via <c>HasDatabaseName</c>/<c>HasConstraintName</c>.
///
/// The conversion is a simple per-character scan (insert '_' before each interior uppercase
/// letter). It does not handle consecutive-uppercase acronyms (e.g. "HTTPServer" would become
/// "h_t_t_p_server") — none of the current entities have one. Revisit this if one is introduced.
/// </summary>
public static class SnakeCaseNamingConvention
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    public static string ToSnakeCase(string name)
    {
        return string.Concat(
            name.Select((c, i) => i > 0 && char.IsUpper(c)
                ? "_" + char.ToLowerInvariant(c)
                : char.ToLowerInvariant(c).ToString()));
    }
}
