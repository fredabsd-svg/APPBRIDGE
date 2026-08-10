using AppBridge.ControlPlane.Api.Models;
using AppBridge.ControlPlane.Infrastructure.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace AppBridge.ControlPlane.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock;
    private readonly ExceptionHandlingMiddleware _middleware;

    public ExceptionHandlingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        _middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("Test exception"),
            _loggerMock.Object);
    }

    [Fact]
    public async Task InvokeAsync_WithUnauthorizedAccessException_Returns401()
    {
        // Arrange
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new UnauthorizedAccessException("Invalid credentials"),
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        httpContext.Response.ContentType.Should().Be("application/json");

        var body = await GetResponseBody(httpContext);
        body.Code.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task InvokeAsync_WithKeyNotFoundException_Returns404()
    {
        // Arrange
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new KeyNotFoundException("Resource not found"),
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var body = await GetResponseBody(httpContext);
        body.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task InvokeAsync_WithArgumentException_Returns400()
    {
        // Arrange
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ArgumentException("Invalid argument"),
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var body = await GetResponseBody(httpContext);
        body.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task InvokeAsync_WithAuditInvalidOperationException_Returns503()
    {
        // Arrange
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("audit logging failed"),
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);

        var body = await GetResponseBody(httpContext);
        body.Code.Should().Be("SERVICE_UNAVAILABLE");
    }

    [Fact]
    public async Task InvokeAsync_WithGenericInvalidOperationException_Returns400()
    {
        // Arrange
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("Generic operation failed"),
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var body = await GetResponseBody(httpContext);
        body.Code.Should().Be("INVALID_OPERATION");
    }

    [Fact]
    public async Task InvokeAsync_WithUnexpectedException_Returns500()
    {
        // Arrange
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new NotImplementedException("Feature not implemented"),
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var body = await GetResponseBody(httpContext);
        body.Code.Should().Be("INTERNAL_ERROR");
    }

    [Fact]
    public async Task InvokeAsync_AllResponses_IncludeTraceId()
    {
        // Arrange
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new UnauthorizedAccessException("Test"),
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id-12345";
        httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        var body = await GetResponseBody(httpContext);
        body.TraceId.Should().Be("test-trace-id-12345");
    }

    [Fact]
    public async Task InvokeAsync_SuccessfulRequest_DoesNotCatchException()
    {
        // Arrange
        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.CompletedTask,
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = StatusCodes.Status200OK;

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_SetsJsonContentType()
    {
        // Arrange
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ArgumentException("Test"),
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        httpContext.Response.ContentType.Should().Be("application/json");
    }

    private static async Task<ErrorResponse> GetResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<ErrorResponse>(json)!;
    }
}
