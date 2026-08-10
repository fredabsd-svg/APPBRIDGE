using System.Net;
using System.Text.Json;
using AppBridge.ControlPlane.Api.Models;

namespace AppBridge.ControlPlane.Infrastructure.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse
        {
            TraceId = context.TraceIdentifier,
        };

        var (statusCode, code, message) = GetErrorDetails(exception);

        context.Response.StatusCode = statusCode;

        response.Code = code;
        response.Message = message;

        return context.Response.WriteAsJsonAsync(response);
    }

    private static (int statusCode, string code, string message) GetErrorDetails(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Authentication failed or token is invalid."),

            KeyNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "The requested resource was not found."),

            ArgumentException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_ERROR", exception.Message),

            InvalidOperationException when exception.Message.Contains("audit") =>
                (StatusCodes.Status503ServiceUnavailable, "SERVICE_UNAVAILABLE", "Audit logging failed. Please try again later."),

            InvalidOperationException =>
                (StatusCodes.Status400BadRequest, "INVALID_OPERATION", exception.Message),

            _ =>
                (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred. Please try again later."),
        };
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
