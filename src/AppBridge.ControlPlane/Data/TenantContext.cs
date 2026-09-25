namespace AppBridge.ControlPlane.Data;

/// <summary>Tenant da requisição atual, vinculado pela autenticação do servidor.</summary>
public sealed class TenantContext
{
    public Guid? TenantId { get; private set; }
    public bool IsBound => TenantId.HasValue;
    public Guid EffectiveTenantId => TenantId.GetValueOrDefault();

    public void Bind(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId não pode ser vazio.", nameof(tenantId));
        }

        if (TenantId is not null && TenantId != tenantId)
        {
            throw new InvalidOperationException("O TenantContext desta requisição já foi vinculado.");
        }

        TenantId = tenantId;
    }
}
