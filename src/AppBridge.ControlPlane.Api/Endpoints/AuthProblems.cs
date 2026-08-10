using Microsoft.AspNetCore.Mvc;

namespace AppBridge.ControlPlane.Api.Endpoints;

/// <summary>Problem Details for <c>/v1/auth/session</c>, per ADR-0012 §2 and the error table in API.md §2 — URN type, Portuguese title/detail (RNF-043), stable <c>appbridgeCode</c>.</summary>
public static class AuthProblems
{
    private const string Instance = "/v1/auth/session";

    public static ProblemDetails InvalidIdentityToken(string correlationId) => Build(
        StatusCodes.Status401Unauthorized,
        "INVALID_IDENTITY_TOKEN",
        "Não foi possível validar sua identidade.",
        "O token apresentado é inválido ou expirou. Faça login novamente.",
        correlationId);

    public static ProblemDetails UserDisabled(string correlationId) => Build(
        StatusCodes.Status403Forbidden,
        "USER_DISABLED",
        "Sua conta está desabilitada.",
        "Fale com o administrador do seu escritório para reativar o acesso.",
        correlationId);

    public static ProblemDetails TenantSuspended(string correlationId) => Build(
        StatusCodes.Status403Forbidden,
        "TENANT_SUSPENDED",
        "O acesso da sua empresa está suspenso.",
        "Fale com o suporte do AppBridge para regularizar o acesso.",
        correlationId);

    public static ProblemDetails AuditUnavailable(string correlationId) => Build(
        StatusCodes.Status503ServiceUnavailable,
        "AUDIT_UNAVAILABLE",
        "Não foi possível concluir o login no momento.",
        "O registro de auditoria está indisponível. Tente novamente em instantes.",
        correlationId);

    private static ProblemDetails Build(int status, string code, string title, string detail, string correlationId) => new()
    {
        Type = $"urn:appbridge:problem:{code.ToLowerInvariant().Replace('_', '-')}",
        Title = title,
        Status = status,
        Detail = detail,
        Instance = Instance,
        Extensions =
        {
            ["correlationId"] = correlationId,
            ["appbridgeCode"] = code,
        },
    };
}
