using System.Net;
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
    private readonly WindowsCredentialStore credentialStore;
    private readonly SemaphoreSlim sessionLock = new(1, 1);
    private readonly Guid correlationId = Guid.CreateVersion7();
    private SessionCredentials? session;

    public AppBridgeApiClient(Uri baseAddress, WindowsCredentialStore credentialStore)
    {
        this.credentialStore = credentialStore;
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

    public async Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken)
    {
        SessionCredentials? saved;
        try
        {
            saved = credentialStore.Read();
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            credentialStore.Delete();
            return false;
        }

        if (saved is null)
        {
            return false;
        }

        if (saved.RefreshExpiresAt <= DateTimeOffset.UtcNow)
        {
            ClearSession();
            return false;
        }

        session = saved;
        try
        {
            await EnsureAccessTokenAsync(cancellationToken);
            return true;
        }
        catch (AppBridgeApiException exception) when (exception.Code == "REFRESH_EXPIRED")
        {
            ClearSession();
            return false;
        }
    }

    public async Task AuthenticateAsync(string identityToken, string workstationName, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("v1/auth/session",
            new AuthenticationSessionRequest(identityToken, workstationName), JsonOptions, cancellationToken);
        var result = await ReadSuccessfulResponseAsync<AuthenticationSessionResponse>(response, cancellationToken);
        SetSession(new SessionCredentials(
            result.AccessToken,
            result.ExpiresAt,
            result.RefreshToken,
            result.RefreshExpiresAt));
    }

    public async Task<IReadOnlyList<RemoteApplication>> GetApplicationsAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAuthenticatedAsync(
            () => CreateAuthenticatedRequest(HttpMethod.Get, "v1/applications"), cancellationToken);
        var result = await ReadSuccessfulResponseAsync<ApplicationListResponse>(response, cancellationToken);
        return result.Items;
    }

    public async Task<LaunchResponse> CreateLaunchAsync(
        Guid applicationId,
        string workstationName,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = Guid.CreateVersion7();
        using var response = await SendAuthenticatedAsync(() =>
        {
            var request = CreateAuthenticatedRequest(HttpMethod.Post, "v1/launches");
            request.Headers.Add("Idempotency-Key", idempotencyKey.ToString("D"));
            request.Content = JsonContent.Create(
                new LaunchRequest(applicationId, "user_initiated", workstationName), options: JsonOptions);
            return request;
        }, cancellationToken);
        return await ReadSuccessfulResponseAsync<LaunchResponse>(response, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendAuthenticatedAsync(
                () => CreateAuthenticatedRequest(HttpMethod.Post, "v1/auth/logout"), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw await CreateApiExceptionAsync(response, cancellationToken);
            }
        }
        finally
        {
            ClearSession();
        }
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        await sessionLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureAccessTokenAsync(cancellationToken);
            var response = await SendOnceAsync(requestFactory, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return response;
            }

            response.Dispose();
            await RefreshSessionAsync(cancellationToken);
            return await SendOnceAsync(requestFactory, cancellationToken);
        }
        finally
        {
            sessionLock.Release();
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        using var request = requestFactory();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private async Task EnsureAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (session is null)
        {
            throw new InvalidOperationException("A sessão do AppBridge ainda não foi autenticada.");
        }

        if (session.RefreshExpiresAt <= DateTimeOffset.UtcNow)
        {
            ClearSession();
            throw new AppBridgeApiException("REFRESH_EXPIRED", "Sua sessão não pode ser renovada. Entre novamente.", HttpStatusCode.Unauthorized);
        }

        if (session.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            await RefreshSessionAsync(cancellationToken);
        }
    }

    private async Task RefreshSessionAsync(CancellationToken cancellationToken)
    {
        if (session is null || session.RefreshExpiresAt <= DateTimeOffset.UtcNow)
        {
            ClearSession();
            throw new AppBridgeApiException("REFRESH_EXPIRED", "Sua sessão não pode ser renovada. Entre novamente.", HttpStatusCode.Unauthorized);
        }

        using var response = await httpClient.PostAsJsonAsync(
            "v1/auth/refresh",
            new AuthenticationRefreshRequest(session.RefreshToken),
            JsonOptions,
            cancellationToken);
        try
        {
            var result = await ReadSuccessfulResponseAsync<AuthenticationRefreshResponse>(response, cancellationToken);
            SetSession(new SessionCredentials(
                result.AccessToken,
                result.ExpiresAt,
                result.RefreshToken,
                result.RefreshExpiresAt));
        }
        catch (AppBridgeApiException exception) when (exception.Code == "REFRESH_EXPIRED")
        {
            ClearSession();
            throw;
        }
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string path)
        => new(method, path);

    private static async Task<T> ReadSuccessfulResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return body ?? throw new AppBridgeApiException(
                "EMPTY_RESPONSE", "O Control Plane devolveu uma resposta vazia.", response.StatusCode);
        }

        throw await CreateApiExceptionAsync(response, cancellationToken);
    }

    private static async Task<AppBridgeApiException> CreateApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var error = ParseError(errorBody);
        return new AppBridgeApiException(error.Code, error.Message, response.StatusCode);
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

    private void SetSession(SessionCredentials value)
    {
        credentialStore.Write(value);
        session = value;
    }

    private void ClearSession()
    {
        session = null;
        credentialStore.Delete();
    }

    public void ClearSavedSession() => ClearSession();

    public void Dispose()
    {
        sessionLock.Dispose();
        httpClient.Dispose();
    }

    private sealed record AuthenticationSessionRequest(string IdentityToken, string WorkstationName);
    private sealed record AuthenticationSessionResponse(
        string AccessToken,
        DateTimeOffset ExpiresAt,
        string RefreshToken,
        DateTimeOffset RefreshExpiresAt,
        AuthenticatedUser User);
    private sealed record AuthenticationRefreshRequest(string RefreshToken);
    private sealed record AuthenticationRefreshResponse(
        string AccessToken,
        DateTimeOffset ExpiresAt,
        string RefreshToken,
        DateTimeOffset RefreshExpiresAt);
    private sealed record ApplicationListResponse(IReadOnlyList<RemoteApplication> Items, string? NextCursor);
    private sealed record LaunchRequest(Guid ApplicationId, string Purpose, string WorkstationName);
}

internal sealed record AuthenticatedUser(Guid Id, string DisplayName, AuthenticatedTenant Tenant, string[] Roles);
internal sealed record AuthenticatedTenant(Guid Id, string Name);
internal sealed record RemoteApplication(Guid Id, string DisplayName, string? Description, bool Available);
internal sealed record LaunchResponse(string RdpFile, DateTimeOffset ExpiresAt, Guid CorrelationId);

internal sealed class AppBridgeApiException(string code, string message, HttpStatusCode statusCode)
    : Exception($"{code}: {message} (HTTP {(int)statusCode}).")
{
    public string Code { get; } = code;
    public HttpStatusCode StatusCode { get; } = statusCode;
}
