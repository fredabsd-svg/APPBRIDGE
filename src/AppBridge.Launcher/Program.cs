namespace AppBridge.Launcher;

internal static class Program
{
    public static async Task<int> Main()
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("O AppBridge Launcher MVP-0a só pode abrir RemoteApps em estações Windows.");
                return 2;
            }

            var settings = LauncherSettings.Load();
            using var api = new AppBridgeApiClient(settings.ApiBaseAddress);
            var identityTokens = new EntraIdentityTokenProvider(settings);
            var workstationName = Environment.MachineName;

            Console.WriteLine("Entrando no AppBridge...");
            var identityToken = await identityTokens.AcquireIdentityTokenAsync(cancellation.Token);
            await api.AuthenticateAsync(identityToken, workstationName, cancellation.Token);

            var applications = await api.GetApplicationsAsync(cancellation.Token);
            if (applications.Count == 0)
            {
                Console.WriteLine("Nenhum aplicativo publicado está autorizado para esta conta.");
                return 0;
            }

            var selectedApplication = SelectApplication(applications);
            if (selectedApplication is null)
            {
                Console.WriteLine("Lançamento cancelado.");
                return 0;
            }

            var launch = await api.CreateLaunchAsync(selectedApplication.Id, workstationName, cancellation.Token);
            Console.WriteLine($"Abrindo {selectedApplication.DisplayName}...");
            await new LaunchCoordinator().LaunchAsync(launch, cancellation.Token);
            if (cancellation.IsCancellationRequested)
            {
                return 0;
            }

            Console.WriteLine($"Lançamento concluído. Referência: {launch.CorrelationId:D}");
            return 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Console.WriteLine("Operação cancelada.");
            return 0;
        }
        catch (LauncherConfigurationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
        catch (AppBridgeApiException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Não foi possível iniciar o AppBridge: {exception.Message}");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static RemoteApplication? SelectApplication(IReadOnlyList<RemoteApplication> applications)
    {
        Console.WriteLine("Aplicativos disponíveis:");
        for (var index = 0; index < applications.Count; index++)
        {
            Console.WriteLine($"  {index + 1}. {applications[index].DisplayName}");
        }

        while (true)
        {
            Console.Write("Número do aplicativo (ou Enter para sair): ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            if (int.TryParse(input, out var selectedIndex)
                && selectedIndex > 0
                && selectedIndex <= applications.Count)
            {
                return applications[selectedIndex - 1];
            }

            Console.WriteLine("Escolha um número da lista.");
        }
    }
}
