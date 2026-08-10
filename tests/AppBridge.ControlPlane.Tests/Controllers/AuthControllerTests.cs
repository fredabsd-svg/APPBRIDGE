using AppBridge.ControlPlane.Api.Controllers;
using AppBridge.ControlPlane.Application.Abstractions.Authentication;
using AppBridge.ControlPlane.Application.Dtos.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace AppBridge.ControlPlane.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthenticationService> _authenticationServiceMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        _authenticationServiceMock = new Mock<IAuthenticationService>();
        _loggerMock = new Mock<ILogger<AuthController>>();
        _sut = new AuthController(_authenticationServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200Ok()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new LoginRequestDto
        {
            TenantId = tenantId.ToString(),
            UserIdentifier = "user@domain.com",
            Password = "password123",
        };

        var tokenDto = new AuthTokenDto
        {
            AccessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
            ExpiresIn = 3600,
        };

        _authenticationServiceMock
            .Setup(x => x.AuthenticateAsync(request.UserIdentifier, request.Password, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenDto);

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(tokenDto);
    }

    [Fact]
    public async Task Login_WithInvalidTenantId_Returns400BadRequest()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            TenantId = "not-a-guid",
            UserIdentifier = "user@domain.com",
            Password = "password123",
        };

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Login_WithMissingTenantId_Returns400BadRequest()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            TenantId = string.Empty,
            UserIdentifier = "user@domain.com",
            Password = "password123",
        };

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_WithMissingUserIdentifier_Returns400BadRequest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new LoginRequestDto
        {
            TenantId = tenantId.ToString(),
            UserIdentifier = string.Empty,
            Password = "password123",
        };

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_WithMissingPassword_Returns400BadRequest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new LoginRequestDto
        {
            TenantId = tenantId.ToString(),
            UserIdentifier = "user@domain.com",
            Password = string.Empty,
        };

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401Unauthorized()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new LoginRequestDto
        {
            TenantId = tenantId.ToString(),
            UserIdentifier = "user@domain.com",
            Password = "wrongpassword",
        };

        _authenticationServiceMock
            .Setup(x => x.AuthenticateAsync(request.UserIdentifier, request.Password, tenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid credentials"));

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        var unauthorizedResult = result as UnauthorizedObjectResult;
        unauthorizedResult!.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Login_WithUnexpectedError_Returns500InternalServerError()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new LoginRequestDto
        {
            TenantId = tenantId.ToString(),
            UserIdentifier = "user@domain.com",
            Password = "password123",
        };

        _authenticationServiceMock
            .Setup(x => x.AuthenticateAsync(request.UserIdentifier, request.Password, tenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var errorResult = result as ObjectResult;
        errorResult!.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task Login_WithValidCredentials_LogsInformationMessage()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new LoginRequestDto
        {
            TenantId = tenantId.ToString(),
            UserIdentifier = "user@domain.com",
            Password = "password123",
        };

        var tokenDto = new AuthTokenDto { AccessToken = "token", ExpiresIn = 3600 };

        _authenticationServiceMock
            .Setup(x => x.AuthenticateAsync(request.UserIdentifier, request.Password, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenDto);

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("logged in successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_LogsErrorMessage()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new LoginRequestDto
        {
            TenantId = tenantId.ToString(),
            UserIdentifier = "user@domain.com",
            Password = "wrongpassword",
        };

        _authenticationServiceMock
            .Setup(x => x.AuthenticateAsync(request.UserIdentifier, request.Password, tenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Authentication error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Login_CallsAuthenticationServiceWithCorrectParameters()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new LoginRequestDto
        {
            TenantId = tenantId.ToString(),
            UserIdentifier = "user@domain.com",
            Password = "password123",
        };

        var tokenDto = new AuthTokenDto { AccessToken = "token", ExpiresIn = 3600 };

        _authenticationServiceMock
            .Setup(x => x.AuthenticateAsync(request.UserIdentifier, request.Password, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenDto);

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        _authenticationServiceMock.Verify(
            x => x.AuthenticateAsync(request.UserIdentifier, request.Password, tenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
