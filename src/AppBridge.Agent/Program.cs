using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

var options = new ServiceCollection()
    .AddHostOptions(configure => configure.ShutdownTimeout = TimeSpan.FromSeconds(15))
    .BuildServiceProvider();

await Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureServices((context, services) =>
    {
        // Add services here
    })
    .RunAsync();
