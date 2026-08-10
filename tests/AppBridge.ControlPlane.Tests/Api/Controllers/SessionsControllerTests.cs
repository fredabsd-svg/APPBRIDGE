using AppBridge.ControlPlane.Api.Controllers;
using AppBridge.ControlPlane.Application.Abstractions.Context;
using AppBridge.ControlPlane.Application.Abstractions.Sessions;
using AppBridge.ControlPlane.Application.Dtos.Sessions;
using AppBridge.ControlPlane.Core.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AppBridge.ControlPlane.Tests.Api.Controllers;

public class SessionsControllerTests
{
    private readonly Mock<ISessionService> _sessionServiceMock;
    private readonly Mock<ITenantContextService> _tenantContextServiceMock;
    private readonly Mock<ILogger<SessionsController>> _loggerMock;
    private readonly SessionsController _sut;

    private readonly Guid _testTenantId = Guid.NewGuid();
    private readonly Guid _testApplicationId = Guid.NewGuid();
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _testSessionId = Guid.NewGuid();

    public SessionsControllerTests()
    {
        _sessionServiceMock = new Mock<ISessionService>();
        _tenantContextServiceMock = new Mock<ITenantContextService>();
        _loggerMock = new Mock<ILogger<SessionsController>>();
        _sut = new SessionsController(_sessionServiceMock.Object, _tenantContextServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ListUserSessions_WithValidContext_ReturnsOkWithSessions()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.UserId).Returns(_testUserId);
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        var sessions = new[]
        {
            new SessionDto { Id = Guid.NewGuid(), ApplicationId = _testApplicationId, UserId = _testUserId, State = SessionState.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new SessionDto { Id = Guid.NewGuid(), ApplicationId = _testApplicationId, UserId = _testUserId, State = SessionState.Pending, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
        };

        _sessionServiceMock
            .Setup(x => x.ListUserSessionsAsync(_testTenantId, _testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _sut.ListUserSessions(CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(sessions);
    }

    [Fact]
    public async Task ListUserSessions_WithoutUserId_ReturnsUnauthorized()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.UserId).Returns((Guid?)null);
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        // Act
        var result = await _sut.ListUserSessions(CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ListUserSessions_WithoutTenantId_ReturnsUnauthorized()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.UserId).Returns(_testUserId);
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns((Guid?)null);

        // Act
        var result = await _sut.ListUserSessions(CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetSession_WithValidSession_ReturnsOkWithSession()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        var session = new SessionDto
        {
            Id = _testSessionId,
            ApplicationId = _testApplicationId,
            UserId = _testUserId,
            State = SessionState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _sessionServiceMock
            .Setup(x => x.GetSessionAsync(_testTenantId, _testSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _sut.GetSession(_testSessionId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(session);
    }

    [Fact]
    public async Task GetSession_WithNonExistentSession_ReturnsNotFound()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        _sessionServiceMock
            .Setup(x => x.GetSessionAsync(_testTenantId, _testSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionDto?)null);

        // Act
        var result = await _sut.GetSession(_testSessionId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetSession_WithoutTenantId_ReturnsUnauthorized()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns((Guid?)null);

        // Act
        var result = await _sut.GetSession(_testSessionId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task LaunchSession_WithValidData_ReturnsCreatedAtAction()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);
        _tenantContextServiceMock.Setup(x => x.UserId).Returns(_testUserId);

        var createDto = new CreateSessionDto { ApplicationId = _testApplicationId };

        var launchResponse = new LaunchSessionResponseDto
        {
            SessionId = _testSessionId,
            RdpFile = "base64encodedrdp",
            ValiditySeconds = 60,
        };

        _sessionServiceMock
            .Setup(x => x.LaunchSessionAsync(_testTenantId, _testUserId, _testApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(launchResponse);

        // Act
        var result = await _sut.LaunchSession(createDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult!.ActionName.Should().Be(nameof(SessionsController.GetSession));
        createdResult.RouteValues!["sessionId"].Should().Be(_testSessionId);
        createdResult.Value.Should().BeEquivalentTo(launchResponse);
    }

    [Fact]
    public async Task LaunchSession_WithEmptyApplicationId_ReturnsBadRequest()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);
        _tenantContextServiceMock.Setup(x => x.UserId).Returns(_testUserId);

        var createDto = new CreateSessionDto { ApplicationId = Guid.Empty };

        // Act
        var result = await _sut.LaunchSession(createDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task LaunchSession_WithNonExistentApplication_ReturnsNotFound()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);
        _tenantContextServiceMock.Setup(x => x.UserId).Returns(_testUserId);

        var createDto = new CreateSessionDto { ApplicationId = _testApplicationId };

        _sessionServiceMock
            .Setup(x => x.LaunchSessionAsync(_testTenantId, _testUserId, _testApplicationId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _sut.LaunchSession(createDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task LaunchSession_WithoutAccess_ReturnsForbid()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);
        _tenantContextServiceMock.Setup(x => x.UserId).Returns(_testUserId);

        var createDto = new CreateSessionDto { ApplicationId = _testApplicationId };

        _sessionServiceMock
            .Setup(x => x.LaunchSessionAsync(_testTenantId, _testUserId, _testApplicationId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        // Act
        var result = await _sut.LaunchSession(createDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task LaunchSession_WithUnpublishedApp_ReturnsBadRequest()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);
        _tenantContextServiceMock.Setup(x => x.UserId).Returns(_testUserId);

        var createDto = new CreateSessionDto { ApplicationId = _testApplicationId };

        _sessionServiceMock
            .Setup(x => x.LaunchSessionAsync(_testTenantId, _testUserId, _testApplicationId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Application is not published"));

        // Act
        var result = await _sut.LaunchSession(createDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task LaunchSession_WithoutTenantId_ReturnsUnauthorized()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns((Guid?)null);

        var createDto = new CreateSessionDto { ApplicationId = _testApplicationId };

        // Act
        var result = await _sut.LaunchSession(createDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task LaunchSession_WithoutUserId_ReturnsUnauthorized()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);
        _tenantContextServiceMock.Setup(x => x.UserId).Returns((Guid?)null);

        var createDto = new CreateSessionDto { ApplicationId = _testApplicationId };

        // Act
        var result = await _sut.LaunchSession(createDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdateSession_WithValidData_ReturnsOkWithUpdatedSession()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        var updateDto = new UpdateSessionDto { State = SessionState.Active };

        var updatedSession = new SessionDto
        {
            Id = _testSessionId,
            ApplicationId = _testApplicationId,
            UserId = _testUserId,
            State = SessionState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _sessionServiceMock
            .Setup(x => x.UpdateSessionAsync(_testTenantId, _testSessionId, updateDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedSession);

        // Act
        var result = await _sut.UpdateSession(_testSessionId, updateDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(updatedSession);
    }

    [Fact]
    public async Task UpdateSession_WithNonExistentSession_ReturnsNotFound()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        var updateDto = new UpdateSessionDto { State = SessionState.Active };

        _sessionServiceMock
            .Setup(x => x.UpdateSessionAsync(_testTenantId, _testSessionId, updateDto, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _sut.UpdateSession(_testSessionId, updateDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateSession_WithoutTenantId_ReturnsUnauthorized()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns((Guid?)null);

        var updateDto = new UpdateSessionDto { State = SessionState.Active };

        // Act
        var result = await _sut.UpdateSession(_testSessionId, updateDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task TerminateSession_WithValidSession_ReturnsNoContent()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        _sessionServiceMock
            .Setup(x => x.TerminateSessionAsync(_testTenantId, _testSessionId, null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.TerminateSession(_testSessionId, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task TerminateSession_WithReason_PassesReasonToService()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        _sessionServiceMock
            .Setup(x => x.TerminateSessionAsync(_testTenantId, _testSessionId, "User logged off", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.TerminateSession(_testSessionId, "User logged off", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _sessionServiceMock.Verify(x => x.TerminateSessionAsync(_testTenantId, _testSessionId, "User logged off", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TerminateSession_WithNonExistentSession_ReturnsNotFound()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        _sessionServiceMock
            .Setup(x => x.TerminateSessionAsync(_testTenantId, _testSessionId, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _sut.TerminateSession(_testSessionId, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task TerminateSession_WithoutTenantId_ReturnsUnauthorized()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns((Guid?)null);

        // Act
        var result = await _sut.TerminateSession(_testSessionId, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }
}
