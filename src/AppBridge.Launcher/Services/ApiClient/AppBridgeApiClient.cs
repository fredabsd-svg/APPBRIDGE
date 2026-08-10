using System.Net.Http.Headers;
using System.Text.Json;
using AppBridge.Launcher.Models;

namespace AppBridge.Launcher.Services.ApiClient;

/// <summary>
/// HTTP client for AppBridge Control Plane API.
/// Handles authentication, token management, and API communication.
/// </summary>
public class AppBridgeApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AppBridgeApiClient> _logger;
    private string? _accessToken;
    private DateTimeOffset _tokenExpiration;

    public AppBridgeApiClient(HttpClient httpClient, ILogger<AppBridgeApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public void SetBaseAddress(string baseAddress)
    {
        _httpClient.BaseAddress = new Uri(baseAddress);
    }

    public async Task<AuthTokenResponse?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { email, password };
            var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/auth/login", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Authentication failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<AuthTokenResponse>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result != null)
            {
                _accessToken = result.AccessToken;
                _tokenExpiration = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn);
                SetAuthHeader();
                _logger.LogInformation("Authentication successful");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication request failed");
            throw;
        }
    }

    public async Task<IEnumerable<ApplicationDto>?> GetApplicationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsTokenValid())
            {
                _logger.LogWarning("Token expired, need to re-authenticate");
                return null;
            }

            var response = await _httpClient.GetAsync("/api/applications", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch applications with status {StatusCode}", response.StatusCode);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<List<ApplicationDto>>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            _logger.LogInformation("Fetched {Count} applications", result?.Count ?? 0);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch applications");
            throw;
        }
    }

    public async Task<LaunchSessionResponseDto?> LaunchApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsTokenValid())
            {
                _logger.LogWarning("Token expired, cannot launch application");
                return null;
            }

            var request = new { applicationId };
            var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/sessions", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to launch application with status {StatusCode}", response.StatusCode);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<LaunchSessionResponseDto>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result != null)
            {
                _logger.LogInformation("Session {SessionId} created successfully", result.SessionId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch application");
            throw;
        }
    }

    private void SetAuthHeader()
    {
        if (_accessToken != null)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }
    }

    private bool IsTokenValid()
    {
        return !string.IsNullOrEmpty(_accessToken) && DateTimeOffset.UtcNow < _tokenExpiration.AddSeconds(-30);
    }
}

public record AuthTokenResponse(string AccessToken, int ExpiresIn);

public record ApplicationDto(
    Guid Id,
    string Identifier,
    string DisplayName,
    string? Description,
    string RemoteAppName,
    string? IconUrl,
    bool IsPublished,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record LaunchSessionResponseDto(
    Guid SessionId,
    string RdpFile,
    int ValiditySeconds);
