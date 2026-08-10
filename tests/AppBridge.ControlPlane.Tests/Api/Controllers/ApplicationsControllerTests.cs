using AppBridge.ControlPlane.Api.Controllers;
using AppBridge.ControlPlane.Application.Abstractions.Applications;
using AppBridge.ControlPlane.Application.Abstractions.Context;
using AppBridge.ControlPlane.Application.Dtos.Applications;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AppBridge.ControlPlane.Tests.Api.Controllers;

public class ApplicationsControllerTests
{
    private readonly Mock<IApplicationService> _applicationServiceMock;
    private readonly Mock<ITenantContextService> _tenantContextServiceMock;
    private readonly Mock<ILogger<ApplicationsController>> _loggerMock;
    private readonly ApplicationsController _sut;

    private readonly Guid _testTenantId = Guid.NewGuid();
    private readonly Guid _testApplicationId = Guid.NewGuid();
    private readonly Guid _testUserId = Guid.NewGuid();

    public ApplicationsControllerTests()
    {
        _applicationServiceMock = new Mock<IApplicationService>();
        _tenantContextServiceMock = new Mock<ITenantContextService>();
        _loggerMock = new Mock<ILogger<ApplicationsController>>();
        _sut = new ApplicationsController(_applicationServiceMock.Object, _tenantContextServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ListApplications_WithValidContext_ReturnsOkWithApplications()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.UserId).Returns(_testUserId);
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        var applications = new[]
        {
            new ApplicationDto { Id = Guid.NewGuid(), Identifier = "app1", DisplayName = "App 1", RemoteAppName = "App1", IsPublished = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new ApplicationDto { Id = Guid.NewGuid(), Identifier = "app2", DisplayName = "App 2", RemoteAppName = "App2", IsPublished = false, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
        };

        _applicationServiceMock
            .Setup(x => x.ListUserApplicationsAsync(_testTenantId, _testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(applications);

        // Act
        var result = await _sut.ListApplications(CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(applications);
    }

    [Fact]
    public async Task ListApplications_WithoutUserId_ReturnsUnauthorized()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.UserId).Returns((Guid?)null);
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        // Act
        var result = await _sut.ListApplications(CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ListApplications_WithoutTenantId_ReturnsUnauthorized()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.UserId).Returns(_testUserId);
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns((Guid?)null);

        // Act
        var result = await _sut.ListApplications(CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetApplication_WithValidApplication_ReturnsOkWithApplication()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        var application = new ApplicationDto
        {
            Id = _testApplicationId,
            Identifier = "test-app",
            DisplayName = "Test App",
            RemoteAppName = "TestApp",
            IsPublished = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _applicationServiceMock
            .Setup(x => x.GetApplicationAsync(_testTenantId, _testApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(application);

        // Act
        var result = await _sut.GetApplication(_testApplicationId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(application);
    }

    [Fact]
    public async Task GetApplication_WithNonExistentApplication_ReturnsNotFound()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        _applicationServiceMock
            .Setup(x => x.GetApplicationAsync(_testTenantId, _testApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationDto?)null);

        // Act
        var result = await _sut.GetApplication(_testApplicationId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetApplication_WithoutTenantId_ReturnsUnauthorized()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns((Guid?)null);

        // Act
        var result = await _sut.GetApplication(_testApplicationId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreateApplication_WithValidData_ReturnsCreatedAtAction()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        var createDto = new CreateApplicationDto
        {
            Identifier = "new-app",
            DisplayName = "New App",
            RemoteAppName = "NewApp",
            Description = "Test description",
        };

        var createdApplication = new ApplicationDto
        {
            Id = _testApplicationId,
            Identifier = createDto.Identifier,
            DisplayName = createDto.DisplayName,
            RemoteAppName = createDto.RemoteAppName,
            Description = createDto.Description,
            IsPublished = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _applicationServiceMock
            .Setup(x => x.CreateApplicationAsync(_testTenantId, createDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdApplication);

        // Act
        var result = await _sut.CreateApplication(createDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult!.ActionName.Should().Be(nameof(ApplicationsController.GetApplication));
        createdResult.RouteValues!["applicationId"].Should().Be(_testApplicationId);
        createdResult.Value.Should().BeEquivalentTo(createdApplication);
    }

    [Fact]
    public async Task CreateApplication_WithMissingIdentifier_ReturnsBadRequest()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        var createDto = new CreateApplicationDto
        {
            Identifier = "",
            DisplayName = "New App",
            RemoteAppName = "NewApp",
        };

        // Act
        var result = await _sut.CreateApplication(createDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateApplication_WithoutTenantId_ReturnsUnauthorized()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns((Guid?)null);

        var createDto = new CreateApplicationDto
        {
            Identifier = "new-app",
            DisplayName = "New App",
            RemoteAppName = "NewApp",
        };

        // Act
        var result = await _sut.CreateApplication(createDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdateApplication_WithValidApplication_ReturnsOkWithUpdatedApplication()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        var updateDto = new UpdateApplicationDto
        {
            DisplayName = "Updated App",
            Description = "Updated description",
        };

        var updatedApplication = new ApplicationDto
        {
            Id = _testApplicationId,
            Identifier = "test-app",
            DisplayName = updateDto.DisplayName,
            RemoteAppName = "TestApp",
            Description = updateDto.Description,
            IsPublished = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _applicationServiceMock
            .Setup(x => x.UpdateApplicationAsync(_testTenantId, _testApplicationId, updateDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedApplication);

        // Act
        var result = await _sut.UpdateApplication(_testApplicationId, updateDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(updatedApplication);
    }

    [Fact]
    public async Task UpdateApplication_WithMissingDisplayName_ReturnsBadRequest()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        var updateDto = new UpdateApplicationDto
        {
            DisplayName = "",
        };

        // Act
        var result = await _sut.UpdateApplication(_testApplicationId, updateDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateApplication_WithNonExistentApplication_ReturnsNotFound()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        var updateDto = new UpdateApplicationDto
        {
            DisplayName = "Updated App",
        };

        _applicationServiceMock
            .Setup(x => x.UpdateApplicationAsync(_testTenantId, _testApplicationId, updateDto, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _sut.UpdateApplication(_testApplicationId, updateDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateApplication_WithoutTenantId_ReturnsUnauthorized()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns((Guid?)null);

        var updateDto = new UpdateApplicationDto
        {
            DisplayName = "Updated App",
        };

        // Act
        var result = await _sut.UpdateApplication(_testApplicationId, updateDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task PublishApplication_WithValidApplication_ReturnsOkWithPublishedApplication()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        var publishDto = new PublishApplicationDto { IsPublished = true };

        var publishedApplication = new ApplicationDto
        {
            Id = _testApplicationId,
            Identifier = "test-app",
            DisplayName = "Test App",
            RemoteAppName = "TestApp",
            IsPublished = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _applicationServiceMock
            .Setup(x => x.PublishApplicationAsync(_testTenantId, _testApplicationId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(publishedApplication);

        // Act
        var result = await _sut.PublishApplication(_testApplicationId, publishDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(publishedApplication);
    }

    [Fact]
    public async Task PublishApplication_WithNonExistentApplication_ReturnsNotFound()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        var publishDto = new PublishApplicationDto { IsPublished = true };

        _applicationServiceMock
            .Setup(x => x.PublishApplicationAsync(_testTenantId, _testApplicationId, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _sut.PublishApplication(_testApplicationId, publishDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task PublishApplication_WithoutTenantId_ReturnsUnauthorized()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns((Guid?)null);

        var publishDto = new PublishApplicationDto { IsPublished = true };

        // Act
        var result = await _sut.PublishApplication(_testApplicationId, publishDto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeleteApplication_WithValidApplication_ReturnsNoContent()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        _applicationServiceMock
            .Setup(x => x.DeleteApplicationAsync(_testTenantId, _testApplicationId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.DeleteApplication(_testApplicationId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteApplication_WithNonExistentApplication_ReturnsNotFound()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns(_testTenantId);

        _applicationServiceMock
            .Setup(x => x.DeleteApplicationAsync(_testTenantId, _testApplicationId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _sut.DeleteApplication(_testApplicationId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteApplication_WithoutTenantId_ReturnsUnauthorized()
    {
        // Arrange
        _tenantContextServiceMock.Setup(x => x.TenantId).Returns((Guid?)null);

        // Act
        var result = await _sut.DeleteApplication(_testApplicationId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }
}
