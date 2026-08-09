namespace AppBridge.ControlPlane.Core.Configuration;

public class DatabaseOptions
{
    public string ConnectionString { get; set; } = null!;
    public int CommandTimeout { get; set; } = 30;
    public int MaxPoolSize { get; set; } = 20;
}
