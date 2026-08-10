using System.Collections.ObjectModel;
using AppBridge.Launcher.Services.ApiClient;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppBridge.Launcher.ViewModels;

public partial class ApplicationListViewModel : ObservableObject
{
    private readonly AppBridgeApiClient _apiClient;
    private readonly ILogger<ApplicationListViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<ApplicationDto> applications = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private ApplicationDto? selectedApplication;

    public ApplicationListViewModel(AppBridgeApiClient apiClient, ILogger<ApplicationListViewModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [RelayCommand]
    public async Task LoadApplicationsAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var apps = await _apiClient.GetApplicationsAsync();
            if (apps == null)
            {
                ErrorMessage = "Failed to load applications";
                _logger.LogWarning("Failed to load applications");
                return;
            }

            Applications.Clear();
            foreach (var app in apps)
            {
                Applications.Add(app);
            }

            _logger.LogInformation("Loaded {Count} applications", apps.Count());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            _logger.LogError(ex, "Error loading applications");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task LaunchApplicationAsync()
    {
        if (SelectedApplication == null)
        {
            ErrorMessage = "No application selected";
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var response = await _apiClient.LaunchApplicationAsync(SelectedApplication.Id);
            if (response == null)
            {
                ErrorMessage = "Failed to launch application";
                _logger.LogWarning("Failed to launch application {AppId}", SelectedApplication.Id);
                return;
            }

            // Launch mstsc with RDP file
            await LaunchRdpFileAsync(response.RdpFile);
            _logger.LogInformation("Launched application {AppId} in session {SessionId}", SelectedApplication.Id, response.SessionId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            _logger.LogError(ex, "Error launching application");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static async Task LaunchRdpFileAsync(string rdpFileBase64)
    {
        try
        {
            var rdpBytes = Convert.FromBase64String(rdpFileBase64);
            var tempFile = Path.Combine(Path.GetTempPath(), $"appbridge_{Guid.NewGuid()}.rdp");

            await File.WriteAllBytesAsync(tempFile, rdpBytes);

            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "mstsc.exe",
                Arguments = $"\"{tempFile}\"",
                UseShellExecute = true,
            };

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start mstsc.exe");
            }

            await process.WaitForExitAsync();

            if (File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to launch RDP file", ex);
        }
    }
}
