using AppBridge.ControlPlane.Infrastructure.Services.Context;
using FluentAssertions;

namespace AppBridge.ControlPlane.Tests.Services.Context;

public class TenantContextServiceTests
{
    private readonly TenantContextService _sut;

    public TenantContextServiceTests()
    {
        _sut = new TenantContextService();
    }

    [Fact]
    public void TenantId_WhenNotSet_ReturnsNull()
    {
        // Act
        var tenantId = _sut.TenantId;

        // Assert
        tenantId.Should().BeNull();
    }

    [Fact]
    public void UserId_WhenNotSet_ReturnsNull()
    {
        // Act
        var userId = _sut.UserId;

        // Assert
        userId.Should().BeNull();
    }

    [Fact]
    public void SetContext_WithValidIds_SetsContextProperties()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        _sut.SetContext(tenantId, userId);

        // Assert
        _sut.TenantId.Should().Be(tenantId);
        _sut.UserId.Should().Be(userId);
    }

    [Fact]
    public void SetContext_CanBeCalledMultipleTimes()
    {
        // Arrange
        var tenantId1 = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        // Act
        _sut.SetContext(tenantId1, userId1);
        _sut.SetContext(tenantId2, userId2);

        // Assert
        _sut.TenantId.Should().Be(tenantId2);
        _sut.UserId.Should().Be(userId2);
    }
}
