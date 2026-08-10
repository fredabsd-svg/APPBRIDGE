namespace AppBridge.ControlPlane.Application.Dtos.Applications;

public class ApplicationDto
{
    public Guid Id { get; set; }
    public string Identifier { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Description { get; set; }
    public string RemoteAppName { get; set; } = null!;
    public string? IconUrl { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class CreateApplicationDto
{
    public string Identifier { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Description { get; set; }
    public string RemoteAppName { get; set; } = null!;
    public string? IconUrl { get; set; }
}

public class UpdateApplicationDto
{
    public string DisplayName { get; set; } = null!;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
}

public class PublishApplicationDto
{
    public bool IsPublished { get; set; }
}
