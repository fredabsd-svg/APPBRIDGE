using AppBridge.Launcher.Services.ApiClient;
using AppBridge.Launcher.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace AppBridge.Launcher;

public partial class App : Application
{
    private Window? m_window;
    private IServiceProvider? _serviceProvider;

    public App()
    {
        this.InitializeComponent();
        ConfigureServices();
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddHttpClient<AppBridgeApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:7000");
            client.DefaultRequestHeaders.Add("User-Agent", "AppBridge.Launcher/0.0.1-alpha");
        })
        .ConfigureHttpClientDefaults(builder =>
        {
            builder.ConfigureHttpClientDefaults(config =>
            {
                config.HttpClientActions.Add(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                });
            });
        });

        services.AddScoped<ApplicationListViewModel>();
        services.AddScoped<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        m_window = (_serviceProvider?.GetRequiredService<MainWindow>()) ?? new MainWindow();
        m_window.Activate();
    }
}
