namespace AppBridge.ControlPlane.Api.Endpoints;

/// <summary>
/// Error bodies for <c>POST /v1/launches</c> (API.md §4, ADR-0012 §2). A plain record, not
/// <c>Microsoft.AspNetCore.Mvc.ProblemDetails</c> like <c>AuthProblems</c> uses: this endpoint
/// serializes every response — success or error — through the same path so a replayed idempotent
/// response is byte-identical to the original (<see cref="Idempotency.IIdempotencyStore"/>), and a
/// plain record sidesteps ASP.NET Core's <c>ProblemDetails</c> converter machinery, which ordinary
/// <c>JsonSerializer.Serialize</c> calls outside <c>Results.Problem</c> don't reliably pick up.
/// </summary>
public static class LaunchProblems
{
    public static LaunchProblemBody ApplicationNotFound(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status404NotFound,
        "APPLICATION_NOT_FOUND",
        "Aplicativo não encontrado.",
        "O aplicativo solicitado não existe ou não está mais disponível.",
        correlationId);

    public static LaunchProblemBody PermissionRevoked(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status403Forbidden,
        "PERMISSION_REVOKED",
        "Você não tem acesso a este aplicativo.",
        "Sua permissão para este aplicativo foi revogada ou nunca existiu. Fale com o administrador do seu escritório.",
        correlationId);

    public static LaunchProblemBody ApplicationUnavailable(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status422UnprocessableEntity,
        "APPLICATION_UNAVAILABLE",
        "Aplicativo temporariamente indisponível.",
        "Nenhum servidor está disponível para este aplicativo no momento. Tente novamente em instantes.",
        correlationId);

    public static LaunchProblemBody SigningUnavailable(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status503ServiceUnavailable,
        "SIGNING_UNAVAILABLE",
        "Não foi possível preparar a conexão.",
        "O serviço de assinatura está indisponível no momento. Tente novamente em instantes.",
        correlationId);

    public static LaunchProblemBody AuditUnavailable(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status503ServiceUnavailable,
        "AUDIT_UNAVAILABLE",
        "Não foi possível concluir a operação no momento.",
        "O registro de auditoria está indisponível. Tente novamente em instantes.",
        correlationId);

    public static LaunchProblemBody IdempotencyConflict(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status409Conflict,
        "IDEMPOTENCY_CONFLICT",
        "Requisição em conflito.",
        "A mesma chave de idempotência foi usada com uma requisição diferente.",
        correlationId);

    public static LaunchProblemBody InvalidPurpose(string instance, string correlationId) => Build(
        instance,
        StatusCodes.Status400BadRequest,
        "INVALID_PURPOSE",
        "Requisição inválida.",
        "O campo 'purpose' precisa ser 'user_initiated' ou 'prelaunch'.",
        correlationId);

    private static LaunchProblemBody Build(
        string instance, int status, string code, string title, string detail, string correlationId) => new(
        Type: $"urn:appbridge:problem:{code.ToLowerInvariant().Replace('_', '-')}",
        Title: title,
        Status: status,
        Detail: detail,
        Instance: instance,
        CorrelationId: correlationId,
        AppbridgeCode: code);
}

/// <summary>Same field set as ADR-0012 §2's Problem Details example, flattened.</summary>
public sealed record LaunchProblemBody(
    string Type, string Title, int Status, string Detail, string Instance, string CorrelationId, string AppbridgeCode);
