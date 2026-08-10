using AppBridge.ControlPlane.Application.Dtos.Auditing;
using AppBridge.ControlPlane.Core.Entities;
using AppBridge.ControlPlane.Infrastructure.Persistence;
using AppBridge.ControlPlane.Infrastructure.Services.Auditing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AppBridge.ControlPlane.Tests.Services.Auditing;

public class AuditingServiceTests : IAsyncLifetime
{
    private readonly AppBridgeDbContext _dbContext;
    private readonly Mock<ILogger<AuditingService>> _loggerMock;
    private readonly AuditingService _sut;

    private readonly Guid _testTenantId = Guid.NewGuid();

    public AuditingServiceTests()
    {
        var dbContextOptions = new DbContextOptionsBuilder<AppBridgeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppBridgeDbContext(dbContextOptions);
        _loggerMock = new Mock<ILogger<AuditingService>>();
        _sut = new AuditingService(_dbContext, _loggerMock.Object);
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
    public async Task LogAccessAsync_WithValidAuditLog_SavesLogToDatabase()
    {
        // Arrange
        var auditLog = new AuditLogDto
        {
            TenantId = _testTenantId,
            Category = "Access",
            ActorIdentifier = "user@domain.com",
            Action = "login",
            ResourceType = "User",
            ResourceId = Guid.NewGuid().ToString(),
            Description = "User logged in",
            Result = "Success",
            SourceIp = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
        };

        // Act
        await _sut.LogAccessAsync(auditLog);

        // Assert
        var savedLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        savedLog.Should().NotBeNull();
        savedLog!.TenantId.Should().Be(_testTenantId);
        savedLog.Action.Should().Be("login");
        savedLog.Category.Should().Be(AuditCategory.Access);
        savedLog.Result.Should().Be(AuditResult.Success);
    }

    [Fact]
    public async Task LogAuthorizationAsync_WithValidAuditLog_SavesLogWithCorrectCategory()
    {
        // Arrange
        var auditLog = new AuditLogDto
        {
            TenantId = _testTenantId,
            Category = "Authorization",
            ActorIdentifier = "user@domain.com",
            Action = "access_denied",
            ResourceType = "Application",
            ResourceId = Guid.NewGuid().ToString(),
            Description = "User denied access to application",
            Result = "Denied",
        };

        // Act
        await _sut.LogAuthorizationAsync(auditLog);

        // Assert
        var savedLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        savedLog.Should().NotBeNull();
        savedLog!.Category.Should().Be(AuditCategory.Authorization);
    }

    [Fact]
    public async Task LogAdministrativeAsync_WithValidAuditLog_SavesLogWithCorrectCategory()
    {
        // Arrange
        var auditLog = new AuditLogDto
        {
            TenantId = _testTenantId,
            Category = "Administrative",
            ActorIdentifier = "admin@domain.com",
            Action = "create_user",
            ResourceType = "User",
            ResourceId = Guid.NewGuid().ToString(),
            Description = "Admin created new user",
            Result = "Success",
        };

        // Act
        await _sut.LogAdministrativeAsync(auditLog);

        // Assert
        var savedLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        savedLog.Should().NotBeNull();
        savedLog!.Category.Should().Be(AuditCategory.Administrative);
    }

    [Fact]
    public async Task LogSystemConfigurationAsync_WithValidAuditLog_SavesLogWithCorrectCategory()
    {
        // Arrange
        var auditLog = new AuditLogDto
        {
            TenantId = _testTenantId,
            Category = "SystemConfiguration",
            ActorIdentifier = "admin@domain.com",
            Action = "update_settings",
            ResourceType = "Configuration",
            ResourceId = "retention_days",
            Description = "Admin updated retention settings",
            Result = "Success",
        };

        // Act
        await _sut.LogSystemConfigurationAsync(auditLog);

        // Assert
        var savedLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        savedLog.Should().NotBeNull();
        savedLog!.Category.Should().Be(AuditCategory.SystemConfiguration);
    }

    [Fact]
    public async Task LogAccessAsync_WithFailureResult_SavesLogWithFailureDetails()
    {
        // Arrange
        var auditLog = new AuditLogDto
        {
            TenantId = _testTenantId,
            Category = "Access",
            ActorIdentifier = "user@domain.com",
            Action = "login",
            ResourceType = "User",
            ResourceId = Guid.NewGuid().ToString(),
            Description = "User failed to login",
            Result = "Failure",
            FailureReason = "Invalid credentials",
        };

        // Act
        await _sut.LogAccessAsync(auditLog);

        // Assert
        var savedLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        savedLog.Should().NotBeNull();
        savedLog!.Result.Should().Be(AuditResult.Failure);
        savedLog.FailureReason.Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task LogAccessAsync_WithActorUserId_SavesLogWithActorReference()
    {
        // Arrange
        var actorUserId = Guid.NewGuid();
        var auditLog = new AuditLogDto
        {
            TenantId = _testTenantId,
            Category = "Access",
            ActorUserId = actorUserId,
            ActorIdentifier = "user@domain.com",
            Action = "login",
            ResourceType = "User",
            ResourceId = Guid.NewGuid().ToString(),
            Description = "User logged in",
            Result = "Success",
        };

        // Act
        await _sut.LogAccessAsync(auditLog);

        // Assert
        var savedLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        savedLog.Should().NotBeNull();
        savedLog!.ActorUserId.Should().Be(actorUserId);
    }

    [Fact]
    public async Task LogAccessAsync_WithDetails_SavesJsonDetails()
    {
        // Arrange
        var details = "{ \"method\": \"POST\", \"path\": \"/api/users\" }";
        var auditLog = new AuditLogDto
        {
            TenantId = _testTenantId,
            Category = "Access",
            ActorIdentifier = "admin@domain.com",
            Action = "create_user",
            ResourceType = "User",
            ResourceId = Guid.NewGuid().ToString(),
            Description = "Admin created new user",
            Result = "Success",
            Details = details,
        };

        // Act
        await _sut.LogAccessAsync(auditLog);

        // Assert
        var savedLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        savedLog.Should().NotBeNull();
        savedLog!.Details.Should().Be(details);
    }

    [Fact]
    public async Task LogAccessAsync_WithValidLog_LogsInformationMessage()
    {
        // Arrange
        var auditLog = new AuditLogDto
        {
            TenantId = _testTenantId,
            Category = "Access",
            ActorIdentifier = "user@domain.com",
            Action = "login",
            ResourceType = "User",
            ResourceId = Guid.NewGuid().ToString(),
            Description = "User logged in",
            Result = "Success",
        };

        // Act
        await _sut.LogAccessAsync(auditLog);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Audit log recorded")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAccessAsync_WithDatabaseFailure_ThrowsInvalidOperationException()
    {
        // Arrange
        var dbContextMock = new Mock<AppBridgeDbContext>();
        dbContextMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        dbContextMock.Setup(x => x.AuditLogs).Returns(_dbContext.AuditLogs);

        var auditingService = new AuditingService(dbContextMock.Object, _loggerMock.Object);

        var auditLog = new AuditLogDto
        {
            TenantId = _testTenantId,
            Category = "Access",
            ActorIdentifier = "user@domain.com",
            Action = "login",
            ResourceType = "User",
            ResourceId = Guid.NewGuid().ToString(),
            Description = "User logged in",
            Result = "Success",
        };

        // Act & Assert
        await auditingService.Invoking(x => x.LogAccessAsync(auditLog))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Audit logging failed*");
    }

    [Fact]
    public async Task LogAccessAsync_WithDatabaseFailure_LogsErrorMessage()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<AuditingService>>();
        var dbContextMock = new Mock<AppBridgeDbContext>();
        dbContextMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        dbContextMock.Setup(x => x.AuditLogs).Returns(_dbContext.AuditLogs);

        var auditingService = new AuditingService(dbContextMock.Object, loggerMock.Object);

        var auditLog = new AuditLogDto
        {
            TenantId = _testTenantId,
            Category = "Access",
            ActorIdentifier = "user@domain.com",
            Action = "login",
            ResourceType = "User",
            ResourceId = Guid.NewGuid().ToString(),
            Description = "User logged in",
            Result = "Success",
        };

        // Act
        try
        {
            await auditingService.LogAccessAsync(auditLog);
        }
        catch { }

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to log audit event")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAccessAsync_SetsTimestampsAutomatically()
    {
        // Arrange
        var beforeLog = DateTimeOffset.UtcNow;
        var auditLog = new AuditLogDto
        {
            TenantId = _testTenantId,
            Category = "Access",
            ActorIdentifier = "user@domain.com",
            Action = "login",
            ResourceType = "User",
            ResourceId = Guid.NewGuid().ToString(),
            Description = "User logged in",
            Result = "Success",
        };

        // Act
        await _sut.LogAccessAsync(auditLog);
        var afterLog = DateTimeOffset.UtcNow;

        // Assert
        var savedLog = await _dbContext.AuditLogs.FirstOrDefaultAsync();
        savedLog.Should().NotBeNull();
        savedLog!.OccurredAt.Should().BeOnOrAfter(beforeLog);
        savedLog.OccurredAt.Should().BeOnOrBefore(afterLog);
        savedLog.LoggedAt.Should().BeOnOrAfter(beforeLog);
        savedLog.LoggedAt.Should().BeOnOrBefore(afterLog);
    }

    [Fact]
    public async Task LogAccessAsync_GeneratesUniqueId()
    {
        // Arrange
        var auditLog = new AuditLogDto
        {
            TenantId = _testTenantId,
            Category = "Access",
            ActorIdentifier = "user@domain.com",
            Action = "login",
            ResourceType = "User",
            ResourceId = Guid.NewGuid().ToString(),
            Description = "User logged in",
            Result = "Success",
        };

        var auditLog2 = new AuditLogDto
        {
            TenantId = _testTenantId,
            Category = "Access",
            ActorIdentifier = "user2@domain.com",
            Action = "login",
            ResourceType = "User",
            ResourceId = Guid.NewGuid().ToString(),
            Description = "User logged in",
            Result = "Success",
        };

        // Act
        await _sut.LogAccessAsync(auditLog);
        await _sut.LogAccessAsync(auditLog2);

        // Assert
        var logs = await _dbContext.AuditLogs.ToListAsync();
        logs.Should().HaveCount(2);
        logs[0].Id.Should().NotBe(logs[1].Id);
    }
}
