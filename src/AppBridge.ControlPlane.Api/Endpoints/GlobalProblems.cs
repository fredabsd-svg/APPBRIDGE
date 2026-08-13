using Microsoft.AspNetCore.Mvc;

namespace AppBridge.ControlPlane.Api.Endpoints;

/// <summary>
/// Problem Details for the codes in API.md §9 that don't belong to one endpoint group — they can
/// happen on any route, not just <c>/auth/*</c> (<see cref="AuthProblems"/>) or
/// <c>/launches</c> (<see cref="LaunchProblems"/>): a malformed body, an expired/invalid bearer
/// token on any authenticated route, or an exception nobody anticipated. Consumed from
/// <c>GlobalExceptionHandler</c> and from the JWT bearer challenge handler in
/// <c>Program.cs</c>, not from an endpoint method — there's no single request handler that "owns"
/// these situations.
/// </summary>
public static class GlobalProblems
{
    public static ProblemDetails MalformedRequest(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status400BadRequest,
        "MALFORMED_REQUEST",
        "Requisição inválida.",
        "O corpo da requisição não pôde ser interpretado.",
        correlationId);

    public static ProblemDetails SessionExpired(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status401Unauthorized,
        "SESSION_EXPIRED",
        "Sua sessão expirou.",
        "O token do AppBridge é inválido ou expirou. Renove-o ou faça login novamente.",
        correlationId);

    /// <summary>
    /// RNF-043: no hostname, file path, SQL, exception message or component version ever reaches
    /// this body — the real exception goes to the structured log via <c>ILogger</c>
    /// (<c>GlobalExceptionHandler</c>), keyed by the same <paramref name="correlationId"/> that
    /// reaches the caller, which is what support actually needs.
    /// </summary>
    public static ProblemDetails InternalError(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status500InternalServerError,
        "INTERNAL_ERROR",
        "Falha inesperada.",
        "Algo deu errado. Se o problema persistir, informe o identificador de correlação ao suporte.",
        correlationId);

    private static ProblemDetails Build(
        string instance, int status, string code, string title, string detail, string correlationId) => new()
    {
        Type = $"urn:appbridge:problem:{code.ToLowerInvariant().Replace('_', '-')}",
        Title = title,
        Status = status,
        Detail = detail,
        Instance = instance,
        Extensions =
        {
            ["correlationId"] = correlationId,
            ["appbridgeCode"] = code,
        },
    };
}
