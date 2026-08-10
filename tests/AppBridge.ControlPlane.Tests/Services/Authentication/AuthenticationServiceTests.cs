using AppBridge.ControlPlane.Core.Configuration;
using AppBridge.ControlPlane.Core.Entities;
using AppBridge.ControlPlane.Infrastructure.Persistence;
using AppBridge.ControlPlane.Infrastructure.Services.Authentication;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AppBridge.ControlPlane.Tests.Services.Authentication;

public class AuthenticationServiceTests : IAsyncLifetime
{
    private readonly AppBridgeDbContext _dbContext;
    private readonly JwtTokenService _jwtTokenService;
    private readonly Mock<ILogger<AuthenticationService>> _authServiceLoggerMock;
    private readonly Mock<ILogger<JwtTokenService>> _jwtServiceLoggerMock;
    private readonly AuthenticationService _sut;

    private readonly Guid _testTenantId = Guid.NewGuid();
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly string _testUserIdentifier = "user@domain.com";
    private readonly string _testDisplayName = "Test User";
    private readonly string _testEmail = "user@domain.com";

    public AuthenticationServiceTests()
    {
        var jwtOptions = new JwtOptions
        {
            SigningKey = "this-is-a-very-long-secret-key-for-testing-purposes-with-at-least-32-bytes!",
            Issuer = "https://appbridge.local",
            Audience = "appbridge-api",
            ExpiryMinutes = 60,
        };

        var dbContextOptions = new DbContextOptionsBuilder<AppBridgeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppBridgeDbContext(dbContextOptions);

        _jwtServiceLoggerMock = new Mock<ILogger<JwtTokenService>>();
        var jwtOptionsMonitor = Options.Create(jwtOptions);
        _jwtTokenService = new JwtTokenService(jwtOptionsMonitor, _jwtServiceLoggerMock.Object);

        _authServiceLoggerMock = new Mock<ILogger<AuthenticationService>>();
        _sut = new AuthenticationService(_dbContext, _jwtTokenService, _authServiceLoggerMock.Object);
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.EnsureCreatedAsync();

        var tenant = new Tenant
        {
            Id = _testTenantId,
            Identifier = "test-tenant",
            Name = "Test Tenant",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidUser_ReturnsTokenDto()
    {
        // Arrange
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = _testUserIdentifier,
            DisplayName = _testDisplayName,
            Email = _testEmail,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.AuthenticateAsync(_testUserIdentifier, "password", _testTenantId);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.TokenType.Should().Be("Bearer");
        result.ExpiresIn.Should().Be(3600);
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidUser_ReturnsValidToken()
    {
        // Arrange
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = _testUserIdentifier,
            DisplayName = _testDisplayName,
            Email = _testEmail,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.AuthenticateAsync(_testUserIdentifier, "password", _testTenantId);

        // Assert
        var isValid = _jwtTokenService.ValidateToken(result.AccessToken);
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_WithUserNotFound_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var nonExistentUserIdentifier = "nonexistent@domain.com";

        // Act & Assert
        await _sut.Invoking(s => s.AuthenticateAsync(nonExistentUserIdentifier, "password", _testTenantId))
            .Should()
            .ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task AuthenticateAsync_WithInactiveUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = _testUserIdentifier,
            DisplayName = _testDisplayName,
            Email = _testEmail,
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await _sut.Invoking(s => s.AuthenticateAsync(_testUserIdentifier, "password", _testTenantId))
            .Should()
            .ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task AuthenticateAsync_WithDeletedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = _testUserIdentifier,
            DisplayName = _testDisplayName,
            Email = _testEmail,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            DeletedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await _sut.Invoking(s => s.AuthenticateAsync(_testUserIdentifier, "password", _testTenantId))
            .Should()
            .ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task AuthenticateAsync_WithUserFromDifferentTenant_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var differentTenantId = Guid.NewGuid();
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = _testUserIdentifier,
            DisplayName = _testDisplayName,
            Email = _testEmail,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await _sut.Invoking(s => s.AuthenticateAsync(_testUserIdentifier, "password", differentTenantId))
            .Should()
            .ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidUser_LogsInformationMessage()
    {
        // Arrange
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = _testUserIdentifier,
            DisplayName = _testDisplayName,
            Email = _testEmail,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.AuthenticateAsync(_testUserIdentifier, "password", _testTenantId);

        // Assert
        _authServiceLoggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("authenticated successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WithUserNotFound_LogsWarningMessage()
    {
        // Arrange
        var nonExistentUserIdentifier = "nonexistent@domain.com";

        // Act & Assert
        try
        {
            await _sut.AuthenticateAsync(nonExistentUserIdentifier, "password", _testTenantId);
        }
        catch { }

        _authServiceLoggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Authentication failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateUserAsync_WithValidUser_ReturnsTrue()
    {
        // Arrange
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = _testUserIdentifier,
            DisplayName = _testDisplayName,
            Email = _testEmail,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ValidateUserAsync(_testUserIdentifier, _testTenantId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateUserAsync_WithUserNotFound_ReturnsFalse()
    {
        // Arrange
        var nonExistentUserIdentifier = "nonexistent@domain.com";

        // Act
        var result = await _sut.ValidateUserAsync(nonExistentUserIdentifier, _testTenantId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateUserAsync_WithInactiveUser_ReturnsFalse()
    {
        // Arrange
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = _testUserIdentifier,
            DisplayName = _testDisplayName,
            Email = _testEmail,
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ValidateUserAsync(_testUserIdentifier, _testTenantId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateUserAsync_WithDeletedUser_ReturnsFalse()
    {
        // Arrange
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = _testUserIdentifier,
            DisplayName = _testDisplayName,
            Email = _testEmail,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            DeletedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ValidateUserAsync(_testUserIdentifier, _testTenantId);

        // Assert
        result.Should().BeFalse();
    }
}
