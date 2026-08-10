using AppBridge.ControlPlane.Core.Entities;
using AppBridge.ControlPlane.Infrastructure.Persistence;
using AppBridge.ControlPlane.Infrastructure.Services.Authorization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AppBridge.ControlPlane.Tests.Services.Authorization;

public class AuthorizationServiceTests : IAsyncLifetime
{
    private readonly AppBridgeDbContext _dbContext;
    private readonly Mock<ILogger<AuthorizationService>> _loggerMock;
    private readonly AuthorizationService _sut;

    private readonly Guid _testTenantId = Guid.NewGuid();
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _testApplicationId = Guid.NewGuid();

    public AuthorizationServiceTests()
    {
        var dbContextOptions = new DbContextOptionsBuilder<AppBridgeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppBridgeDbContext(dbContextOptions);
        _loggerMock = new Mock<ILogger<AuthorizationService>>();
        _sut = new AuthorizationService(_dbContext, _loggerMock.Object);
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
    public async Task UserHasApplicationAccessAsync_WithValidPermission_ReturnsTrue()
    {
        // Arrange
        var permission = new ApplicationUserPermission
        {
            Id = Guid.NewGuid(),
            TenantId = _testTenantId,
            UserId = _testUserId,
            ApplicationId = _testApplicationId,
            GrantedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.ApplicationUserPermissions.Add(permission);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.UserHasApplicationAccessAsync(_testUserId, _testTenantId, _testApplicationId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserHasApplicationAccessAsync_WithoutPermission_ReturnsFalse()
    {
        // Act
        var result = await _sut.UserHasApplicationAccessAsync(_testUserId, _testTenantId, _testApplicationId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UserHasApplicationAccessAsync_WithRevokedPermission_ReturnsFalse()
    {
        // Arrange
        var permission = new ApplicationUserPermission
        {
            Id = Guid.NewGuid(),
            TenantId = _testTenantId,
            UserId = _testUserId,
            ApplicationId = _testApplicationId,
            GrantedAt = DateTimeOffset.UtcNow,
            RevokedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.ApplicationUserPermissions.Add(permission);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.UserHasApplicationAccessAsync(_testUserId, _testTenantId, _testApplicationId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UserHasApplicationAccessAsync_WithExpiredPermission_ReturnsFalse()
    {
        // Arrange
        var permission = new ApplicationUserPermission
        {
            Id = Guid.NewGuid(),
            TenantId = _testTenantId,
            UserId = _testUserId,
            ApplicationId = _testApplicationId,
            GrantedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.ApplicationUserPermissions.Add(permission);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.UserHasApplicationAccessAsync(_testUserId, _testTenantId, _testApplicationId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UserHasApplicationAccessAsync_WithFutureExpiration_ReturnsTrue()
    {
        // Arrange
        var permission = new ApplicationUserPermission
        {
            Id = Guid.NewGuid(),
            TenantId = _testTenantId,
            UserId = _testUserId,
            ApplicationId = _testApplicationId,
            GrantedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.ApplicationUserPermissions.Add(permission);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.UserHasApplicationAccessAsync(_testUserId, _testTenantId, _testApplicationId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserHasApplicationAccessAsync_WithWrongTenant_ReturnsFalse()
    {
        // Arrange
        var otherTenantId = Guid.NewGuid();
        var permission = new ApplicationUserPermission
        {
            Id = Guid.NewGuid(),
            TenantId = _testTenantId,
            UserId = _testUserId,
            ApplicationId = _testApplicationId,
            GrantedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.ApplicationUserPermissions.Add(permission);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.UserHasApplicationAccessAsync(_testUserId, otherTenantId, _testApplicationId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ApplicationIsPublishedAsync_WithPublishedApplication_ReturnsTrue()
    {
        // Arrange
        var application = new Application
        {
            Id = _testApplicationId,
            TenantId = _testTenantId,
            Identifier = "test-app",
            DisplayName = "Test Application",
            RemoteAppName = "TestApp",
            IsPublished = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Applications.Add(application);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ApplicationIsPublishedAsync(_testApplicationId, _testTenantId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ApplicationIsPublishedAsync_WithUnpublishedApplication_ReturnsFalse()
    {
        // Arrange
        var application = new Application
        {
            Id = _testApplicationId,
            TenantId = _testTenantId,
            Identifier = "test-app",
            DisplayName = "Test Application",
            RemoteAppName = "TestApp",
            IsPublished = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Applications.Add(application);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ApplicationIsPublishedAsync(_testApplicationId, _testTenantId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ApplicationIsPublishedAsync_WithNonExistentApplication_ReturnsFalse()
    {
        // Act
        var result = await _sut.ApplicationIsPublishedAsync(Guid.NewGuid(), _testTenantId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserApplicationsAsync_WithMultiplePermissions_ReturnsAllApplicationIds()
    {
        // Arrange
        var app1 = Guid.NewGuid();
        var app2 = Guid.NewGuid();
        var app3 = Guid.NewGuid();

        var perms = new[]
        {
            new ApplicationUserPermission
            {
                Id = Guid.NewGuid(),
                TenantId = _testTenantId,
                UserId = _testUserId,
                ApplicationId = app1,
                GrantedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
            new ApplicationUserPermission
            {
                Id = Guid.NewGuid(),
                TenantId = _testTenantId,
                UserId = _testUserId,
                ApplicationId = app2,
                GrantedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
            new ApplicationUserPermission
            {
                Id = Guid.NewGuid(),
                TenantId = _testTenantId,
                UserId = _testUserId,
                ApplicationId = app3,
                GrantedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        };

        _dbContext.ApplicationUserPermissions.AddRange(perms);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetUserApplicationsAsync(_testUserId, _testTenantId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(app1);
        result.Should().Contain(app2);
        result.Should().NotContain(app3);
    }

    [Fact]
    public async Task GetUserApplicationsAsync_WithNoPermissions_ReturnsEmpty()
    {
        // Act
        var result = await _sut.GetUserApplicationsAsync(_testUserId, _testTenantId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserApplicationsAsync_ExcludesRevokedPermissions()
    {
        // Arrange
        var app1 = Guid.NewGuid();
        var app2 = Guid.NewGuid();

        var perms = new[]
        {
            new ApplicationUserPermission
            {
                Id = Guid.NewGuid(),
                TenantId = _testTenantId,
                UserId = _testUserId,
                ApplicationId = app1,
                GrantedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
            new ApplicationUserPermission
            {
                Id = Guid.NewGuid(),
                TenantId = _testTenantId,
                UserId = _testUserId,
                ApplicationId = app2,
                GrantedAt = DateTimeOffset.UtcNow,
                RevokedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        };

        _dbContext.ApplicationUserPermissions.AddRange(perms);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetUserApplicationsAsync(_testUserId, _testTenantId);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(app1);
    }

    [Fact]
    public async Task UserHasApplicationAccessAsync_WithValidPermission_LogsInformationMessage()
    {
        // Arrange
        var permission = new ApplicationUserPermission
        {
            Id = Guid.NewGuid(),
            TenantId = _testTenantId,
            UserId = _testUserId,
            ApplicationId = _testApplicationId,
            GrantedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.ApplicationUserPermissions.Add(permission);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.UserHasApplicationAccessAsync(_testUserId, _testTenantId, _testApplicationId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("granted access")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task UserHasApplicationAccessAsync_WithoutPermission_LogsWarningMessage()
    {
        // Act
        var result = await _sut.UserHasApplicationAccessAsync(_testUserId, _testTenantId, _testApplicationId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("denied access")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
