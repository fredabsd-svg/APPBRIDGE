using AppBridge.ControlPlane.Application.Abstractions.Context;
using AppBridge.ControlPlane.Infrastructure.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace AppBridge.ControlPlane.Tests.Middleware;

public class TenantContextMiddlewareTests
{
    private readonly Mock<ITenantContextService> _tenantContextServiceMock;
    private readonly Mock<ILogger<TenantContextMiddleware>> _loggerMock;
    private readonly TenantContextMiddleware _middleware;

    public TenantContextMiddlewareTests()
    {
        _tenantContextServiceMock = new Mock<ITenantContextService>();
        _loggerMock = new Mock<ILogger<TenantContextMiddleware>>();
        _middleware = new TenantContextMiddleware(
            async ctx => await Task.CompletedTask,
            _loggerMock.Object);
    }

    [Fact]
    public async Task InvokeAsync_WithUnauthenticatedUser_SkipsContextSetting()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(); // No identity

        // Act
        await _middleware.InvokeAsync(httpContext, _tenantContextServiceMock.Object);

        // Assert
        _tenantContextServiceMock.Verify(x => x.SetContext(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never());
    }

    [Fact]
    public async Task InvokeAsync_WithValidClaims_SetsContext()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("sub", userId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };

        // Act
        await _middleware.InvokeAsync(httpContext, _tenantContextServiceMock.Object);

        // Assert
        _tenantContextServiceMock.Verify(
            x => x.SetContext(tenantId, userId),
            Times.Once());
    }

    [Fact]
    public async Task InvokeAsync_WithMissingTenantIdClaim_SkipsContextSetting()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };

        // Act
        await _middleware.InvokeAsync(httpContext, _tenantContextServiceMock.Object);

        // Assert
        _tenantContextServiceMock.Verify(x => x.SetContext(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never());
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidTenantIdFormat_SkipsContextSetting()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("tenant_id", "invalid-uuid"),
            new Claim("sub", userId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };

        // Act
        await _middleware.InvokeAsync(httpContext, _tenantContextServiceMock.Object);

        // Assert
        _tenantContextServiceMock.Verify(x => x.SetContext(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never());
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidUserIdFormat_SkipsContextSetting()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("sub", "invalid-uuid"),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };

        // Act
        await _middleware.InvokeAsync(httpContext, _tenantContextServiceMock.Object);

        // Assert
        _tenantContextServiceMock.Verify(x => x.SetContext(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never());
    }

    [Fact]
    public async Task InvokeAsync_WithValidClaims_LogsDebugMessage()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("sub", userId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };

        // Act
        await _middleware.InvokeAsync(httpContext, _tenantContextServiceMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tenant context set")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidClaims_LogsWarningMessage()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("tenant_id", "invalid-uuid"),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };

        // Act
        await _middleware.InvokeAsync(httpContext, _tenantContextServiceMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to extract tenant context")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }
}
