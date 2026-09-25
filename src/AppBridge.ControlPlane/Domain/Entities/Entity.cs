namespace AppBridge.ControlPlane.Domain.Entities;

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public interface IVersionedEntity
{
    DateTimeOffset UpdatedAt { get; set; }
    long RowVersion { get; set; }
}

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
}

public abstract class MutableEntity : Entity, IVersionedEntity
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid UpdatedBy { get; set; }
    public long RowVersion { get; set; } = 1;
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

public abstract class TenantVersionedEntity : Entity, ITenantEntity, IVersionedEntity
{
    public Guid TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid UpdatedBy { get; set; }
    public long RowVersion { get; set; } = 1;
}

public abstract class TenantMutableEntity : MutableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
}

public abstract class TenantAppendOnlyEntity : Entity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid CreatedBy { get; set; }
}
