using AppBridge.ControlPlane.Application.Abstractions.Auditing;
using AppBridge.ControlPlane.Infrastructure.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace AppBridge.ControlPlane.Tests.Middleware;

public class AuditingMiddlewareTests
{
    private readonly Mock<ILogger<AuditingMiddleware>> _loggerMock;
    private readonly Mock<IAuditingService> _auditingServiceMock;

    public AuditingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<AuditingMiddleware>>();
        _auditingServiceMock = new Mock<IAuditingService>();
    }

    [Fact]
    public async Task InvokeAsync_WithUnauthenticatedRequest_SkipsAuditing()
    {
        // Arrange
        var middleware = new AuditingMiddleware(
            next: async (ctx) => { ctx.Response.StatusCode = 200; await Task.CompletedTask; },
            logger: _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/health/status";
        context.User = new ClaimsPrincipal();

        // Act
        await middleware.InvokeAsync(context, _auditingServiceMock.Object);

        // Assert
        _auditingServiceMock.Verify(x => x.LogAccessAsync(It.IsAny<AppBridge.ControlPlane.Application.Dtos.Auditing.AuditLogDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithHealthCheckEndpoint_SkipsAuditing()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId),
            new Claim("tenant_id", tenantId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var middleware = new AuditingMiddleware(
            next: async (ctx) => { ctx.Response.StatusCode = 200; await Task.CompletedTask; },
            logger: _loggerMock.Object);

        var context = new DefaultHttpContext { User = principal };
        context.Request.Method = "GET";
        context.Request.Path = "/api/health/status";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        // Act
        await middleware.InvokeAsync(context, _auditingServiceMock.Object);

        // Assert
        _auditingServiceMock.Verify(x => x.LogAccessAsync(It.IsAny<AppBridge.ControlPlane.Application.Dtos.Auditing.AuditLogDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithAuthenticatedRequest_CallsAuditingService()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("unique_name", "user@domain.com"),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var middleware = new AuditingMiddleware(
            next: async (ctx) => { ctx.Response.StatusCode = 200; await Task.CompletedTask; },
            logger: _loggerMock.Object);

        var context = new DefaultHttpContext { User = principal };
        context.Request.Method = "GET";
        context.Request.Path = "/api/users";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        _auditingServiceMock
            .Setup(x => x.LogAccessAsync(It.IsAny<AppBridge.ControlPlane.Application.Dtos.Auditing.AuditLogDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, _auditingServiceMock.Object);

        // Assert
        _auditingServiceMock.Verify(
            x => x.LogAccessAsync(It.IsAny<AppBridge.ControlPlane.Application.Dtos.Auditing.AuditLogDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithSuccessfulResponse_LogsWithSuccessResult()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("unique_name", "user@domain.com"),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var middleware = new AuditingMiddleware(
            next: async (ctx) => { ctx.Response.StatusCode = 200; await Task.CompletedTask; },
            logger: _loggerMock.Object);

        var context = new DefaultHttpContext { User = principal };
        context.Request.Method = "GET";
        context.Request.Path = "/api/users";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        _auditingServiceMock
            .Setup(x => x.LogAccessAsync(It.IsAny<AppBridge.ControlPlane.Application.Dtos.Auditing.AuditLogDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, _auditingServiceMock.Object);

        // Assert
        _auditingServiceMock.Verify(
            x => x.LogAccessAsync(
                It.Is<AppBridge.ControlPlane.Application.Dtos.Auditing.AuditLogDto>(
                    a => a.Result == "Success"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithErrorResponse_LogsWithFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("unique_name", "user@domain.com"),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var middleware = new AuditingMiddleware(
            next: async (ctx) => { ctx.Response.StatusCode = 500; await Task.CompletedTask; },
            logger: _loggerMock.Object);

        var context = new DefaultHttpContext { User = principal };
        context.Request.Method = "POST";
        context.Request.Path = "/api/users";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        _auditingServiceMock
            .Setup(x => x.LogAccessAsync(It.IsAny<AppBridge.ControlPlane.Application.Dtos.Auditing.AuditLogDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, _auditingServiceMock.Object);

        // Assert
        _auditingServiceMock.Verify(
            x => x.LogAccessAsync(
                It.Is<AppBridge.ControlPlane.Application.Dtos.Auditing.AuditLogDto>(
                    a => a.Result == "Failure" && a.FailureReason == "HTTP 500"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_CapturesSourceIpFromRequest()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("unique_name", "user@domain.com"),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var middleware = new AuditingMiddleware(
            next: async (ctx) => { ctx.Response.StatusCode = 200; await Task.CompletedTask; },
            logger: _loggerMock.Object);

        var context = new DefaultHttpContext { User = principal };
        context.Request.Method = "GET";
        context.Request.Path = "/api/users";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.42");

        _auditingServiceMock
            .Setup(x => x.LogAccessAsync(It.IsAny<AppBridge.ControlPlane.Application.Dtos.Auditing.AuditLogDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, _auditingServiceMock.Object);

        // Assert
        _auditingServiceMock.Verify(
            x => x.LogAccessAsync(
                It.Is<AppBridge.ControlPlane.Application.Dtos.Auditing.AuditLogDto>(
                    a => a.SourceIp == "203.0.113.42"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_CapturesUserAgentFromHeaders()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("unique_name", "user@domain.com"),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var middleware = new AuditingMiddleware(
            next: async (ctx) => { ctx.Response.StatusCode = 200; await Task.CompletedTask; },
            logger: _loggerMock.Object);

        var context = new DefaultHttpContext { User = principal };
        context.Request.Method = "GET";
        context.Request.Path = "/api/users";
        context.Request.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        _auditingServiceMock
            .Setup(x => x.LogAccessAsync(It.IsAny<AppBridge.ControlPlane.Application.Dtos.Auditing.AuditLogDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, _auditingServiceMock.Object);

        // Assert
        _auditingServiceMock.Verify(
            x => x.LogAccessAsync(
                It.Is<AppBridge.ControlPlane.Application.Dtos.Auditing.AuditLogDto>(
                    a => a.UserAgent == "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithAuditingServiceException_CatchesAndReturnsError()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("unique_name", "user@domain.com"),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var middleware = new AuditingMiddleware(
            next: async (ctx) => { ctx.Response.StatusCode = 200; await Task.CompletedTask; },
            logger: _loggerMock.Object);

        var context = new DefaultHttpContext { User = principal };
        context.Request.Method = "GET";
        context.Request.Path = "/api/users";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        _auditingServiceMock
            .Setup(x => x.LogAccessAsync(It.IsAny<AppBridge.ControlPlane.Application.Dtos.Auditing.AuditLogDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Audit logging failed"));

        // Act
        await middleware.InvokeAsync(context, _auditingServiceMock.Object);

        // Assert
        context.Response.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task InvokeAsync_ExtractsUserIdentifierFromUniqueNameClaim()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("unique_name", "testuser@domain.com"),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var middleware = new AuditingMiddleware(
            next: async (ctx) => { ctx.Response.StatusCode = 200; await Task.CompletedTask; },
            logger: _loggerMock.Object);

        var context = new DefaultHttpContext { User = principal };
        context.Request.Method = "GET";
        context.Request.Path = "/api/users";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        _auditingServiceMock
            .Setup(x => x.LogAccessAsync(It.IsAny<AppBridge.ControlPlane.Application.Dtos.Auditing.AuditLogDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, _auditingServiceMock.Object);

        // Assert
        _auditingServiceMock.Verify(
            x => x.LogAccessAsync(
                It.Is<AppBridge.ControlPlane.Application.Dtos.Auditing.AuditLogDto>(
                    a => a.ActorIdentifier == "testuser@domain.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
