namespace AppBridge.ControlPlane.Application.Dtos.Authorization;

public class ApplicationAccessCheckDto
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ApplicationId { get; set; }
}
