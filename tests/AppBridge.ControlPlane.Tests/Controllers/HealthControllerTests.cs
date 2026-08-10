using AppBridge.ControlPlane.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace AppBridge.ControlPlane.Tests.Controllers;

public class HealthControllerTests
{
    private readonly Mock<ILogger<HealthController>> _loggerMock;
    private readonly HealthController _sut;

    public HealthControllerTests()
    {
        _loggerMock = new Mock<ILogger<HealthController>>();
        _sut = new HealthController(_loggerMock.Object);
    }

    [Fact]
    public void Status_WithoutAuthentication_Returns200Ok()
    {
        // Act
        var result = _sut.Status();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(200);

        var value = okResult.Value as dynamic;
        value.status.Should().Be("healthy");
        value.timestamp.Should().NotBeNull();
    }

    [Fact]
    public void Status_ReturnsObjectWithHealthyStatus()
    {
        // Act
        var result = _sut.Status();

        // Assert
        var okResult = result as OkObjectResult;
        var value = okResult!.Value as dynamic;

        Assert.NotNull(value.status);
        Assert.NotNull(value.timestamp);
    }

    [Fact]
    public void Protected_WithoutAuthentication_ShouldBeMarkedWithAuthorizeAttribute()
    {
        // Assert
        var methodInfo = typeof(HealthController).GetMethod(nameof(HealthController.Protected));
        var authorizeAttribute = methodInfo!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false).FirstOrDefault();

        authorizeAttribute.Should().NotBeNull("Protected endpoint should have Authorize attribute");
    }

    [Fact]
    public void Protected_WithAuthentication_Returns200Ok()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new Claim("sub", userId),
            new Claim("tenant_id", tenantId),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        _sut.ControllerContext.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = principal,
        };

        // Act
        var result = _sut.Protected();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact]
    public void Protected_WithAuthentication_ReturnsUserIdAndTenantId()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new Claim("sub", userId),
            new Claim("tenant_id", tenantId),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        _sut.ControllerContext.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = principal,
        };

        // Act
        var result = _sut.Protected();

        // Assert
        var okResult = result as OkObjectResult;
        var value = okResult!.Value as dynamic;

        ((string)value.userId).Should().Be(userId);
        ((string)value.tenantId).Should().Be(tenantId);
        ((string)value.status).Should().Be("authenticated");
    }

    [Fact]
    public void Protected_WithAuthentication_LogsInformationMessage()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new Claim("sub", userId),
            new Claim("tenant_id", tenantId),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        _sut.ControllerContext.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = principal,
        };

        // Act
        var result = _sut.Protected();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Protected endpoint accessed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Status_ReturnsCurrentTimestamp()
    {
        // Arrange
        var beforeCall = DateTimeOffset.UtcNow;

        // Act
        var result = _sut.Status();

        var afterCall = DateTimeOffset.UtcNow;

        // Assert
        var okResult = result as OkObjectResult;
        var value = okResult!.Value as dynamic;
        var timestamp = (DateTimeOffset)value.timestamp;

        timestamp.Should().BeOnOrAfter(beforeCall);
        timestamp.Should().BeOnOrBefore(afterCall);
    }

    [Fact]
    public void Protected_ReturnsCurrentTimestamp()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new Claim("sub", userId),
            new Claim("tenant_id", tenantId),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        _sut.ControllerContext.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = principal,
        };

        var beforeCall = DateTimeOffset.UtcNow;

        // Act
        var result = _sut.Protected();

        var afterCall = DateTimeOffset.UtcNow;

        // Assert
        var okResult = result as OkObjectResult;
        var value = okResult!.Value as dynamic;
        var timestamp = (DateTimeOffset)value.timestamp;

        timestamp.Should().BeOnOrAfter(beforeCall);
        timestamp.Should().BeOnOrBefore(afterCall);
    }

    [Fact]
    public void Protected_WithoutSubClaim_ReturnsNullUserId()
    {
        // Arrange
        var tenantId = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new Claim("tenant_id", tenantId),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        _sut.ControllerContext.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = principal,
        };

        // Act
        var result = _sut.Protected();

        // Assert
        var okResult = result as OkObjectResult;
        var value = okResult!.Value as dynamic;

        Assert.Null(value.userId);
    }

    [Fact]
    public void Protected_WithoutTenantIdClaim_ReturnsNullTenantId()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new Claim("sub", userId),
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        _sut.ControllerContext.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = principal,
        };

        // Act
        var result = _sut.Protected();

        // Assert
        var okResult = result as OkObjectResult;
        var value = okResult!.Value as dynamic;

        Assert.Null(value.tenantId);
    }
}
