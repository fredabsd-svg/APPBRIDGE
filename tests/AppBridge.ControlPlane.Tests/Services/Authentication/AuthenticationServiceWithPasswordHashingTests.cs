using AppBridge.ControlPlane.Application.Abstractions.Authentication;
using AppBridge.ControlPlane.Application.Dtos.Authentication;
using AppBridge.ControlPlane.Core.Entities;
using AppBridge.ControlPlane.Infrastructure.Persistence;
using AppBridge.ControlPlane.Infrastructure.Services.Authentication;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AppBridge.ControlPlane.Tests.Services.Authentication;

public class AuthenticationServiceWithPasswordHashingTests : IAsyncLifetime
{
    private readonly AppBridgeDbContext _dbContext;
    private readonly BcryptPasswordHasher _passwordHasher;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ILogger<AuthenticationService>> _loggerMock;
    private readonly AuthenticationService _sut;

    private readonly Guid _testTenantId = Guid.NewGuid();
    private readonly Guid _testUserId = Guid.NewGuid();
    private const string TestPassword = "SecurePassword123!";

    public AuthenticationServiceWithPasswordHashingTests()
    {
        var dbContextOptions = new DbContextOptionsBuilder<AppBridgeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppBridgeDbContext(dbContextOptions);
        _passwordHasher = new BcryptPasswordHasher();
        _tokenServiceMock = new Mock<ITokenService>();
        _loggerMock = new Mock<ILogger<AuthenticationService>>();
        _sut = new AuthenticationService(_dbContext, _tokenServiceMock.Object, _passwordHasher, _loggerMock.Object);
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
    public async Task AuthenticateAsync_WithCorrectPassword_ReturnsToken()
    {
        // Arrange
        var passwordHash = _passwordHasher.HashPassword(TestPassword);
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = "testuser",
            DisplayName = "Test User",
            Email = "testuser@example.com",
            PasswordHash = passwordHash,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var expectedToken = "test-token";
        _tokenServiceMock.Setup(x => x.GenerateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns(expectedToken);

        // Act
        var result = await _sut.AuthenticateAsync("testuser", TestPassword, _testTenantId);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be(expectedToken);
        result.ExpiresIn.Should().Be(3600);
    }

    [Fact]
    public async Task AuthenticateAsync_WithIncorrectPassword_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var passwordHash = _passwordHasher.HashPassword(TestPassword);
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = "testuser",
            DisplayName = "Test User",
            Email = "testuser@example.com",
            PasswordHash = passwordHash,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await _sut.Invoking(x => x.AuthenticateAsync("testuser", "WrongPassword123!", _testTenantId))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AuthenticateAsync_WithNonExistentUser_ThrowsUnauthorizedAccessException()
    {
        // Act & Assert
        await _sut.Invoking(x => x.AuthenticateAsync("nonexistent", TestPassword, _testTenantId))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AuthenticateAsync_WithInactiveUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var passwordHash = _passwordHasher.HashPassword(TestPassword);
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = "testuser",
            DisplayName = "Test User",
            Email = "testuser@example.com",
            PasswordHash = passwordHash,
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await _sut.Invoking(x => x.AuthenticateAsync("testuser", TestPassword, _testTenantId))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AuthenticateAsync_WithDeletedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var passwordHash = _passwordHasher.HashPassword(TestPassword);
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = "testuser",
            DisplayName = "Test User",
            Email = "testuser@example.com",
            PasswordHash = passwordHash,
            IsActive = true,
            DeletedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await _sut.Invoking(x => x.AuthenticateAsync("testuser", TestPassword, _testTenantId))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AuthenticateAsync_WithCorrectPassword_UpdatesLastLoginAt()
    {
        // Arrange
        var passwordHash = _passwordHasher.HashPassword(TestPassword);
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = "testuser",
            DisplayName = "Test User",
            Email = "testuser@example.com",
            PasswordHash = passwordHash,
            IsActive = true,
            LastLoginAt = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _tokenServiceMock.Setup(x => x.GenerateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns("test-token");

        // Act
        await _sut.AuthenticateAsync("testuser", TestPassword, _testTenantId);

        // Assert
        var updatedUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == _testUserId);
        updatedUser.Should().NotBeNull();
        updatedUser!.LastLoginAt.Should().NotBeNull();
        updatedUser.LastLoginAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AuthenticateAsync_WithCrossTenantiUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var otherTenantId = Guid.NewGuid();
        var passwordHash = _passwordHasher.HashPassword(TestPassword);
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = "testuser",
            DisplayName = "Test User",
            Email = "testuser@example.com",
            PasswordHash = passwordHash,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await _sut.Invoking(x => x.AuthenticateAsync("testuser", TestPassword, otherTenantId))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AuthenticateAsync_WithCorrectPassword_LogsInformationMessage()
    {
        // Arrange
        var passwordHash = _passwordHasher.HashPassword(TestPassword);
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = "testuser",
            DisplayName = "Test User",
            Email = "testuser@example.com",
            PasswordHash = passwordHash,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _tokenServiceMock.Setup(x => x.GenerateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns("test-token");

        // Act
        await _sut.AuthenticateAsync("testuser", TestPassword, _testTenantId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("authenticated successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    [Fact]
    public async Task AuthenticateAsync_WithIncorrectPassword_LogsWarningMessage()
    {
        // Arrange
        var passwordHash = _passwordHasher.HashPassword(TestPassword);
        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Identifier = "testuser",
            DisplayName = "Test User",
            Email = "testuser@example.com",
            PasswordHash = passwordHash,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await _sut.Invoking(x => x.AuthenticateAsync("testuser", "WrongPassword123!", _testTenantId))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("invalid password")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }
}
