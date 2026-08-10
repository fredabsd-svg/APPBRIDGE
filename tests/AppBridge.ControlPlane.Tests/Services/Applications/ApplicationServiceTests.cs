using AppBridge.ControlPlane.Application.Abstractions.Applications;
using AppBridge.ControlPlane.Application.Abstractions.Authorization;
using AppBridge.ControlPlane.Application.Dtos.Applications;
using AppBridge.ControlPlane.Core.Entities;
using AppBridge.ControlPlane.Infrastructure.Persistence;
using AppBridge.ControlPlane.Infrastructure.Services.Applications;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AppBridge.ControlPlane.Tests.Services.Applications;

public class ApplicationServiceTests : IAsyncLifetime
{
    private readonly AppBridgeDbContext _dbContext;
    private readonly Mock<IAuthorizationService> _authorizationServiceMock;
    private readonly Mock<ILogger<ApplicationService>> _loggerMock;
    private readonly ApplicationService _sut;

    private readonly Guid _testTenantId = Guid.NewGuid();
    private readonly Guid _testApplicationId = Guid.NewGuid();
    private readonly Guid _testUserId = Guid.NewGuid();

    public ApplicationServiceTests()
    {
        var dbContextOptions = new DbContextOptionsBuilder<AppBridgeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppBridgeDbContext(dbContextOptions);
        _authorizationServiceMock = new Mock<IAuthorizationService>();
        _loggerMock = new Mock<ILogger<ApplicationService>>();
        _sut = new ApplicationService(_dbContext, _authorizationServiceMock.Object, _loggerMock.Object);
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
    public async Task GetApplicationAsync_WithValidApplication_ReturnsApplication()
    {
        // Arrange
        var application = new Application
        {
            Id = _testApplicationId,
            TenantId = _testTenantId,
            Identifier = "test-app",
            DisplayName = "Test App",
            RemoteAppName = "TestApp",
            IsPublished = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Applications.Add(application);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetApplicationAsync(_testTenantId, _testApplicationId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(_testApplicationId);
        result.DisplayName.Should().Be("Test App");
        result.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task GetApplicationAsync_WithNonExistentApplication_ReturnsNull()
    {
        // Act
        var result = await _sut.GetApplicationAsync(_testTenantId, Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetApplicationAsync_WithDeletedApplication_ReturnsNull()
    {
        // Arrange
        var application = new Application
        {
            Id = _testApplicationId,
            TenantId = _testTenantId,
            Identifier = "test-app",
            DisplayName = "Test App",
            RemoteAppName = "TestApp",
            DeletedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Applications.Add(application);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetApplicationAsync(_testTenantId, _testApplicationId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListUserApplicationsAsync_WithUserPermissions_ReturnsAccessibleApplications()
    {
        // Arrange
        var app1 = Guid.NewGuid();
        var app2 = Guid.NewGuid();

        var applications = new[]
        {
            new Application
            {
                Id = app1,
                TenantId = _testTenantId,
                Identifier = "app1",
                DisplayName = "App 1",
                RemoteAppName = "App1",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
            new Application
            {
                Id = app2,
                TenantId = _testTenantId,
                Identifier = "app2",
                DisplayName = "App 2",
                RemoteAppName = "App2",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
        };

        _dbContext.Applications.AddRange(applications);
        await _dbContext.SaveChangesAsync();

        _authorizationServiceMock
            .Setup(x => x.GetUserApplicationsAsync(_testUserId, _testTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { app1, app2 });

        // Act
        var result = await _sut.ListUserApplicationsAsync(_testTenantId, _testUserId);

        // Assert
        result.Should().HaveCount(2);
        result.Select(a => a.Id).Should().Contain(new[] { app1, app2 });
    }

    [Fact]
    public async Task ListUserApplicationsAsync_WithNoPermissions_ReturnsEmpty()
    {
        // Arrange
        _authorizationServiceMock
            .Setup(x => x.GetUserApplicationsAsync(_testUserId, _testTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Guid>());

        // Act
        var result = await _sut.ListUserApplicationsAsync(_testTenantId, _testUserId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListApplicationsAsync_ReturnsAllApplications()
    {
        // Arrange
        var applications = new[]
        {
            new Application
            {
                Id = Guid.NewGuid(),
                TenantId = _testTenantId,
                Identifier = "app1",
                DisplayName = "App 1",
                RemoteAppName = "App1",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
            new Application
            {
                Id = Guid.NewGuid(),
                TenantId = _testTenantId,
                Identifier = "app2",
                DisplayName = "App 2",
                RemoteAppName = "App2",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
        };

        _dbContext.Applications.AddRange(applications);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ListApplicationsAsync(_testTenantId);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateApplicationAsync_WithValidData_CreatesApplication()
    {
        // Arrange
        var dto = new CreateApplicationDto
        {
            Identifier = "new-app",
            DisplayName = "New Application",
            RemoteAppName = "NewApp",
            Description = "Test description",
        };

        // Act
        var result = await _sut.CreateApplicationAsync(_testTenantId, dto);

        // Assert
        result.Should().NotBeNull();
        result.Identifier.Should().Be("new-app");
        result.DisplayName.Should().Be("New Application");
        result.IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task PublishApplicationAsync_WithValidApplication_PublishesApplication()
    {
        // Arrange
        var application = new Application
        {
            Id = _testApplicationId,
            TenantId = _testTenantId,
            Identifier = "test-app",
            DisplayName = "Test App",
            RemoteAppName = "TestApp",
            IsPublished = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Applications.Add(application);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.PublishApplicationAsync(_testTenantId, _testApplicationId, true);

        // Assert
        result.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteApplicationAsync_WithValidApplication_SoftDeletesApplication()
    {
        // Arrange
        var application = new Application
        {
            Id = _testApplicationId,
            TenantId = _testTenantId,
            Identifier = "test-app",
            DisplayName = "Test App",
            RemoteAppName = "TestApp",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Applications.Add(application);
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.DeleteApplicationAsync(_testTenantId, _testApplicationId);

        // Assert
        var deletedApp = await _dbContext.Applications.FirstOrDefaultAsync(a => a.Id == _testApplicationId);
        deletedApp.Should().NotBeNull();
        deletedApp!.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateApplicationAsync_WithValidApplication_UpdatesApplication()
    {
        // Arrange
        var application = new Application
        {
            Id = _testApplicationId,
            TenantId = _testTenantId,
            Identifier = "test-app",
            DisplayName = "Test App",
            RemoteAppName = "TestApp",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Applications.Add(application);
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateApplicationDto
        {
            DisplayName = "Updated App",
            Description = "Updated description",
        };

        // Act
        var result = await _sut.UpdateApplicationAsync(_testTenantId, _testApplicationId, dto);

        // Assert
        result.DisplayName.Should().Be("Updated App");
        result.Description.Should().Be("Updated description");
    }
}
