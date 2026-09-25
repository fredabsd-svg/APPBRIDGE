namespace AppBridge.Launcher;

internal static class Program
{
    public static async Task<int> Main(string[] args)
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

            var logoutOnly = args.Length == 1 && args[0] == "--logout";
            if (args.Length > 0 && !logoutOnly)
            {
                Console.Error.WriteLine("Uso: AppBridge.Launcher [--logout]");
                return 2;
            }

            var settings = LauncherSettings.Load();
            var credentialStore = new WindowsCredentialStore();
            using var api = new AppBridgeApiClient(settings.ApiBaseAddress, credentialStore);
            var workstationName = Environment.MachineName;

            if (logoutOnly)
            {
                try
                {
                    if (!await api.TryRestoreSessionAsync(cancellation.Token))
                    {
                        Console.WriteLine("Não há sessão salva neste usuário do Windows.");
                        return 0;
                    }

                    await api.LogoutAsync(cancellation.Token);
                    Console.WriteLine("Sessão encerrada.");
                    return 0;
                }
                catch (Exception exception) when (exception is AppBridgeApiException or HttpRequestException or TaskCanceledException)
                {
                    api.ClearSavedSession();
                    Console.WriteLine("A sessão local foi removida, mas o Control Plane não confirmou o encerramento remoto.");
                    return 1;
                }
            }

            if (!await api.TryRestoreSessionAsync(cancellation.Token))
            {
                await AuthenticateAsync(settings, api, workstationName, cancellation.Token);
            }

            IReadOnlyList<RemoteApplication> applications;
            try
            {
                applications = await api.GetApplicationsAsync(cancellation.Token);
            }
            catch (AppBridgeApiException exception) when (exception.Code == "REFRESH_EXPIRED")
            {
                Console.WriteLine("A sessão salva foi encerrada. Entre novamente.");
                await AuthenticateAsync(settings, api, workstationName, cancellation.Token);
                applications = await api.GetApplicationsAsync(cancellation.Token);
            }

            var selection = SelectApplication(applications);
            if (selection is null)
            {
                Console.WriteLine("Lançamento cancelado.");
                return 0;
            }

            if (selection.Logout)
            {
                await api.LogoutAsync(cancellation.Token);
                Console.WriteLine("Sessão encerrada.");
                return 0;
            }

            var selectedApplication = applications.Single(application => application.Id == selection.ApplicationId);
            LaunchResponse launch;
            try
            {
                launch = await api.CreateLaunchAsync(selectedApplication.Id, workstationName, cancellation.Token);
            }
            catch (AppBridgeApiException exception) when (exception.Code == "REFRESH_EXPIRED")
            {
                Console.WriteLine("A sessão salva foi encerrada. Entre novamente.");
                await AuthenticateAsync(settings, api, workstationName, cancellation.Token);
                applications = await api.GetApplicationsAsync(cancellation.Token);
                var stillAuthorized = applications.SingleOrDefault(application => application.Id == selectedApplication.Id);
                if (stillAuthorized is null)
                {
                    Console.WriteLine("O aplicativo não está mais autorizado para esta conta.");
                    return 1;
                }

                launch = await api.CreateLaunchAsync(stillAuthorized.Id, workstationName, cancellation.Token);
            }

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

    private static async Task AuthenticateAsync(
        LauncherSettings settings,
        AppBridgeApiClient api,
        string workstationName,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("Entrando no AppBridge...");
        var identityTokens = new EntraIdentityTokenProvider(settings);
        var identityToken = await identityTokens.AcquireIdentityTokenAsync(cancellationToken);
        await api.AuthenticateAsync(identityToken, workstationName, cancellationToken);
    }

    private static UserSelection? SelectApplication(IReadOnlyList<RemoteApplication> applications)
    {
        if (applications.Count == 0)
        {
            Console.WriteLine("Nenhum aplicativo publicado está autorizado para esta conta.");
        }
        else
        {
            Console.WriteLine("Aplicativos disponíveis:");
        }

        for (var index = 0; index < applications.Count; index++)
        {
            Console.WriteLine($"  {index + 1}. {applications[index].DisplayName}");
        }

        Console.WriteLine("  0. Encerrar sessão");
        while (true)
        {
            Console.Write("Número do aplicativo, 0 para sair da conta ou Enter para cancelar: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            if (input == "0")
            {
                return new UserSelection(null, true);
            }

            if (int.TryParse(input, out var selectedIndex)
                && selectedIndex > 0
                && selectedIndex <= applications.Count)
            {
                return new UserSelection(applications[selectedIndex - 1].Id, false);
            }

            Console.WriteLine("Escolha um número da lista.");
        }
    }

    private sealed record UserSelection(Guid? ApplicationId, bool Logout);
}
