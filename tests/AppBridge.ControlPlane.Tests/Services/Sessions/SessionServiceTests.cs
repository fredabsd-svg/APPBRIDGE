using AppBridge.ControlPlane.Application.Abstractions.Authorization;
using AppBridge.ControlPlane.Application.Abstractions.Sessions;
using AppBridge.ControlPlane.Application.Dtos.Sessions;
using AppBridge.ControlPlane.Core.Entities;
using AppBridge.ControlPlane.Infrastructure.Persistence;
using AppBridge.ControlPlane.Infrastructure.Services.Sessions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AppBridge.ControlPlane.Tests.Services.Sessions;

public class SessionServiceTests : IAsyncLifetime
{
    private readonly AppBridgeDbContext _dbContext;
    private readonly Mock<IAuthorizationService> _authorizationServiceMock;
    private readonly Mock<IRdpFileSigner> _rdpFileSignerMock;
    private readonly Mock<ILogger<SessionService>> _loggerMock;
    private readonly SessionService _sut;

    private readonly Guid _testTenantId = Guid.NewGuid();
    private readonly Guid _testApplicationId = Guid.NewGuid();
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _testSessionId = Guid.NewGuid();

    public SessionServiceTests()
    {
        var dbContextOptions = new DbContextOptionsBuilder<AppBridgeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppBridgeDbContext(dbContextOptions);
        _authorizationServiceMock = new Mock<IAuthorizationService>();
        _rdpFileSignerMock = new Mock<IRdpFileSigner>();
        _loggerMock = new Mock<ILogger<SessionService>>();
        _sut = new SessionService(_dbContext, _authorizationServiceMock.Object, _rdpFileSignerMock.Object, _loggerMock.Object);
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

        var user = new User
        {
            Id = _testUserId,
            TenantId = _testTenantId,
            Email = "test@example.com",
            DisplayName = "Test User",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

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

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.Applications.Add(application);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetSessionAsync_WithValidSession_ReturnsSession()
    {
        // Arrange
        var session = new Session
        {
            Id = _testSessionId,
            TenantId = _testTenantId,
            UserId = _testUserId,
            ApplicationId = _testApplicationId,
            State = SessionState.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetSessionAsync(_testTenantId, _testSessionId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(_testSessionId);
        result.State.Should().Be(SessionState.Pending);
    }

    [Fact]
    public async Task GetSessionAsync_WithNonExistentSession_ReturnsNull()
    {
        // Act
        var result = await _sut.GetSessionAsync(_testTenantId, Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListUserSessionsAsync_WithActiveSessions_ReturnsActiveSessions()
    {
        // Arrange
        var session1 = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = _testTenantId,
            UserId = _testUserId,
            ApplicationId = _testApplicationId,
            State = SessionState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var session2 = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = _testTenantId,
            UserId = _testUserId,
            ApplicationId = _testApplicationId,
            State = SessionState.Terminated,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Sessions.AddRange(session1, session2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ListUserSessionsAsync(_testTenantId, _testUserId);

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(session1.Id);
    }

    [Fact]
    public async Task LaunchSessionAsync_WithValidData_CreatesSessionAndReturnsSignedRdp()
    {
        // Arrange
        var signedRdp = new byte[] { 0x53, 0x49, 0x47, 0x4E, 0x45, 0x44 }; // "SIGNED"
        _rdpFileSignerMock.Setup(x => x.SignRdpFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(signedRdp);

        _authorizationServiceMock
            .Setup(x => x.UserHasApplicationAccessAsync(_testUserId, _testTenantId, _testApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.LaunchSessionAsync(_testTenantId, _testUserId, _testApplicationId);

        // Assert
        result.Should().NotBeNull();
        result.SessionId.Should().NotBeEmpty();
        result.RdpFile.Should().NotBeNullOrEmpty();
        result.ValiditySeconds.Should().Be(60);

        var session = await _dbContext.Sessions.FirstOrDefaultAsync(s => s.Id == result.SessionId);
        session.Should().NotBeNull();
        session!.State.Should().Be(SessionState.Pending);
        session.UserId.Should().Be(_testUserId);
    }

    [Fact]
    public async Task LaunchSessionAsync_WithoutAccess_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _authorizationServiceMock
            .Setup(x => x.UserHasApplicationAccessAsync(_testUserId, _testTenantId, _testApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.LaunchSessionAsync(_testTenantId, _testUserId, _testApplicationId)
        );
    }

    [Fact]
    public async Task LaunchSessionAsync_WithUnpublishedApp_ThrowsInvalidOperationException()
    {
        // Arrange
        var unpublishedApp = new Application
        {
            Id = Guid.NewGuid(),
            TenantId = _testTenantId,
            Identifier = "unpublished-app",
            DisplayName = "Unpublished App",
            RemoteAppName = "UnpublishedApp",
            IsPublished = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Applications.Add(unpublishedApp);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.LaunchSessionAsync(_testTenantId, _testUserId, unpublishedApp.Id)
        );
    }

    [Fact]
    public async Task LaunchSessionAsync_WithNonExistentApp_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.LaunchSessionAsync(_testTenantId, _testUserId, Guid.NewGuid())
        );
    }

    [Fact]
    public async Task UpdateSessionAsync_WithValidData_UpdatesSession()
    {
        // Arrange
        var session = new Session
        {
            Id = _testSessionId,
            TenantId = _testTenantId,
            UserId = _testUserId,
            ApplicationId = _testApplicationId,
            State = SessionState.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var updateDto = new UpdateSessionDto
        {
            State = SessionState.Active,
            SessionHostId = "HOST-123",
        };

        // Act
        var result = await _sut.UpdateSessionAsync(_testTenantId, _testSessionId, updateDto);

        // Assert
        result.State.Should().Be(SessionState.Active);
        result.SessionHostId.Should().Be("HOST-123");
    }

    [Fact]
    public async Task UpdateSessionAsync_WithNonExistentSession_ThrowsKeyNotFoundException()
    {
        // Arrange
        var updateDto = new UpdateSessionDto { State = SessionState.Active };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.UpdateSessionAsync(_testTenantId, Guid.NewGuid(), updateDto)
        );
    }

    [Fact]
    public async Task TerminateSessionAsync_WithValidSession_TerminatesSession()
    {
        // Arrange
        var session = new Session
        {
            Id = _testSessionId,
            TenantId = _testTenantId,
            UserId = _testUserId,
            ApplicationId = _testApplicationId,
            State = SessionState.Active,
            StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        };

        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.TerminateSessionAsync(_testTenantId, _testSessionId, "User requested termination");

        // Assert
        result.State.Should().Be(SessionState.Terminated);
        result.TerminationReason.Should().Be("User requested termination");
        result.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task TerminateSessionAsync_WithNonExistentSession_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.TerminateSessionAsync(_testTenantId, Guid.NewGuid())
        );
    }
}
