using Microsoft.AspNetCore.Mvc;

namespace AppBridge.ControlPlane.Api.Endpoints;

/// <summary>
/// Problem Details for the <c>/v1/auth/*</c> endpoints, per ADR-0012 §2 and the error table in
/// API.md §2 — URN type, Portuguese title/detail (RNF-043), stable <c>appbridgeCode</c>. Shared
/// across <c>/session</c>, <c>/refresh</c> and <c>/logout</c> (T-303): the same failure — an
/// unrecognized identity, a disabled user, a suspended tenant, an unwritable audit trail — means
/// the same code regardless of which of the three endpoints hit it.
/// </summary>
public static class AuthProblems
{
    public static ProblemDetails InvalidIdentityToken(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status401Unauthorized,
        "INVALID_IDENTITY_TOKEN",
        "Não foi possível validar sua identidade.",
        "O token apresentado é inválido ou expirou. Faça login novamente.",
        correlationId);

    public static ProblemDetails RefreshExpired(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status401Unauthorized,
        "REFRESH_EXPIRED",
        "Sua sessão expirou.",
        "Faça login novamente para continuar.",
        correlationId);

    public static ProblemDetails UserDisabled(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status403Forbidden,
        "USER_DISABLED",
        "Sua conta está desabilitada.",
        "Fale com o administrador do seu escritório para reativar o acesso.",
        correlationId);

    public static ProblemDetails TenantSuspended(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status403Forbidden,
        "TENANT_SUSPENDED",
        "O acesso da sua empresa está suspenso.",
        "Fale com o suporte do AppBridge para regularizar o acesso.",
        correlationId);

    public static ProblemDetails AuditUnavailable(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status503ServiceUnavailable,
        "AUDIT_UNAVAILABLE",
        "Não foi possível concluir a operação no momento.",
        "O registro de auditoria está indisponível. Tente novamente em instantes.",
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
