using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AppBridge.Launcher;

internal sealed class AppBridgeApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient httpClient;
    private readonly Guid correlationId = Guid.CreateVersion7();
    private string? accessToken;

    public AppBridgeApiClient(Uri baseAddress)
    {
        httpClient = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(20)
        };
        var clientVersion = typeof(AppBridgeApiClient).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        httpClient.DefaultRequestHeaders.Add("X-AppBridge-Client", clientVersion);
        httpClient.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId.ToString("D"));
    }

    public Guid CorrelationId => correlationId;

    public async Task AuthenticateAsync(string identityToken, string workstationName, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("v1/auth/session",
            new AuthenticationSessionRequest(identityToken, workstationName), JsonOptions, cancellationToken);
        var result = await ReadSuccessfulResponseAsync<AuthenticationSessionResponse>(response, cancellationToken);
        accessToken = result.AccessToken;
    }

    public async Task<IReadOnlyList<RemoteApplication>> GetApplicationsAsync(CancellationToken cancellationToken)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "v1/applications");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var result = await ReadSuccessfulResponseAsync<ApplicationListResponse>(response, cancellationToken);
        return result.Items;
    }

    public async Task<LaunchResponse> CreateLaunchAsync(
        Guid applicationId,
        string workstationName,
        CancellationToken cancellationToken)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "v1/launches");
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("D"));
        request.Content = JsonContent.Create(
            new LaunchRequest(applicationId, "user_initiated", workstationName), options: JsonOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadSuccessfulResponseAsync<LaunchResponse>(response, cancellationToken);
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string path)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("A sessão do AppBridge ainda não foi autenticada.");
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static async Task<T> ReadSuccessfulResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return body ?? throw new AppBridgeApiException("O Control Plane devolveu uma resposta vazia.");
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var error = ParseError(errorBody);
        throw new AppBridgeApiException(
            $"{error.Code}: {error.Message} (HTTP {(int)response.StatusCode}).");
    }

    private static (string Code, string Message) ParseError(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            var code = root.TryGetProperty("appbridgeCode", out var codeElement)
                ? codeElement.GetString()
                : null;
            var message = root.TryGetProperty("detail", out var detailElement)
                ? detailElement.GetString()
                : null;
            return (code ?? "CONTROL_PLANE_ERROR", message ?? "A solicitação não foi concluída.");
        }
        catch (JsonException)
        {
            return ("CONTROL_PLANE_ERROR", "A solicitação não foi concluída.");
        }
    }

    public void Dispose() => httpClient.Dispose();

    private sealed record AuthenticationSessionRequest(string IdentityToken, string WorkstationName);
    private sealed record AuthenticationSessionResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record ApplicationListResponse(IReadOnlyList<RemoteApplication> Items, string? NextCursor);
    private sealed record LaunchRequest(Guid ApplicationId, string Purpose, string WorkstationName);
}

internal sealed record RemoteApplication(Guid Id, string DisplayName, string? Description, bool Available);
internal sealed record LaunchResponse(string RdpFile, DateTimeOffset ExpiresAt, Guid CorrelationId);

internal sealed class AppBridgeApiException(string message) : Exception(message);
