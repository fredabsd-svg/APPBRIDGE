using Microsoft.AspNetCore.Mvc;

namespace AppBridge.ControlPlane.Api.Endpoints;

/// <summary>
/// Problem Details for <c>/v1/applications/*</c> (API.md §3), same shape and reasoning as
/// <see cref="AuthProblems"/> — a plain <c>Results.NotFound()</c> (no body) is indistinguishable
/// from any other 404 a client might hit and carries no <c>correlationId</c>, unlike every other
/// error response in the API (T-505).
/// </summary>
public static class CatalogProblems
{
    public static ProblemDetails ApplicationNotFound(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status404NotFound,
        "APPLICATION_NOT_FOUND",
        "Aplicativo não encontrado.",
        "O aplicativo solicitado não existe ou não está mais disponível.",
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
