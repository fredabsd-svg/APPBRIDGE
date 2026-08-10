using AppBridge.ControlPlane.Application.Abstractions.Authorization;
using AppBridge.ControlPlane.Infrastructure.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace AppBridge.ControlPlane.Tests.Authorization;

public class ApplicationAccessAuthorizationHandlerTests
{
    private readonly Mock<IAuthorizationService> _authorizationServiceMock;
    private readonly Mock<ILogger<ApplicationAccessAuthorizationHandler>> _loggerMock;
    private readonly ApplicationAccessAuthorizationHandler _sut;

    public ApplicationAccessAuthorizationHandlerTests()
    {
        _authorizationServiceMock = new Mock<IAuthorizationService>();
        _loggerMock = new Mock<ILogger<ApplicationAccessAuthorizationHandler>>();
        _sut = new ApplicationAccessAuthorizationHandler(_authorizationServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task HandleRequirementAsync_WithValidAccessAndClaims_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId.ToString()),
            new Claim("tenant_id", tenantId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.GetRouteData().Values.Add("applicationId", applicationId.ToString());

        var authContext = new AuthorizationHandlerContext(
            new[] { new ApplicationAccessRequirement() },
            principal,
            httpContext);

        _authorizationServiceMock
            .Setup(x => x.UserHasApplicationAccessAsync(userId, tenantId, applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.HandleRequirementAsync(authContext, new ApplicationAccessRequirement());

        // Assert
        authContext.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithoutAccess_Fails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId.ToString()),
            new Claim("tenant_id", tenantId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.GetRouteData().Values.Add("applicationId", applicationId.ToString());

        var authContext = new AuthorizationHandlerContext(
            new[] { new ApplicationAccessRequirement() },
            principal,
            httpContext);

        _authorizationServiceMock
            .Setup(x => x.UserHasApplicationAccessAsync(userId, tenantId, applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _sut.HandleRequirementAsync(authContext, new ApplicationAccessRequirement());

        // Assert
        authContext.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithMissingUserIdClaim_Fails()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("tenant_id", tenantId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.GetRouteData().Values.Add("applicationId", applicationId.ToString());

        var authContext = new AuthorizationHandlerContext(
            new[] { new ApplicationAccessRequirement() },
            principal,
            httpContext);

        // Act
        await _sut.HandleRequirementAsync(authContext, new ApplicationAccessRequirement());

        // Assert
        authContext.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithMissingTenantIdClaim_Fails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.GetRouteData().Values.Add("applicationId", applicationId.ToString());

        var authContext = new AuthorizationHandlerContext(
            new[] { new ApplicationAccessRequirement() },
            principal,
            httpContext);

        // Act
        await _sut.HandleRequirementAsync(authContext, new ApplicationAccessRequirement());

        // Assert
        authContext.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithMissingApplicationIdInRoute_Fails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId.ToString()),
            new Claim("tenant_id", tenantId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };

        var authContext = new AuthorizationHandlerContext(
            new[] { new ApplicationAccessRequirement() },
            principal,
            httpContext);

        // Act
        await _sut.HandleRequirementAsync(authContext, new ApplicationAccessRequirement());

        // Assert
        authContext.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithInvalidUserIdFormat_Fails()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", "invalid-uuid"),
            new Claim("tenant_id", tenantId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.GetRouteData().Values.Add("applicationId", applicationId.ToString());

        var authContext = new AuthorizationHandlerContext(
            new[] { new ApplicationAccessRequirement() },
            principal,
            httpContext);

        // Act
        await _sut.HandleRequirementAsync(authContext, new ApplicationAccessRequirement());

        // Assert
        authContext.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithInvalidTenantIdFormat_Fails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId.ToString()),
            new Claim("tenant_id", "invalid-uuid"),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.GetRouteData().Values.Add("applicationId", applicationId.ToString());

        var authContext = new AuthorizationHandlerContext(
            new[] { new ApplicationAccessRequirement() },
            principal,
            httpContext);

        // Act
        await _sut.HandleRequirementAsync(authContext, new ApplicationAccessRequirement());

        // Assert
        authContext.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithInvalidApplicationIdFormat_Fails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId.ToString()),
            new Claim("tenant_id", tenantId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.GetRouteData().Values.Add("applicationId", "invalid-uuid");

        var authContext = new AuthorizationHandlerContext(
            new[] { new ApplicationAccessRequirement() },
            principal,
            httpContext);

        // Act
        await _sut.HandleRequirementAsync(authContext, new ApplicationAccessRequirement());

        // Assert
        authContext.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithValidClaims_CallsAuthorizationService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId.ToString()),
            new Claim("tenant_id", tenantId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.GetRouteData().Values.Add("applicationId", applicationId.ToString());

        var authContext = new AuthorizationHandlerContext(
            new[] { new ApplicationAccessRequirement() },
            principal,
            httpContext);

        _authorizationServiceMock
            .Setup(x => x.UserHasApplicationAccessAsync(userId, tenantId, applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.HandleRequirementAsync(authContext, new ApplicationAccessRequirement());

        // Assert
        _authorizationServiceMock.Verify(
            x => x.UserHasApplicationAccessAsync(userId, tenantId, applicationId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleRequirementAsync_WithFailedAuthorization_LogsWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new Claim("sub", userId.ToString()),
            new Claim("tenant_id", tenantId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.GetRouteData().Values.Add("applicationId", applicationId.ToString());

        var authContext = new AuthorizationHandlerContext(
            new[] { new ApplicationAccessRequirement() },
            principal,
            httpContext);

        _authorizationServiceMock
            .Setup(x => x.UserHasApplicationAccessAsync(userId, tenantId, applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _sut.HandleRequirementAsync(authContext, new ApplicationAccessRequirement());

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Authorization failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
