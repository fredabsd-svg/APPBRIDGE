using AppBridge.ControlPlane.Core.Configuration;
using AppBridge.ControlPlane.Infrastructure.Services.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.IdentityModel.Tokens.Jwt;

namespace AppBridge.ControlPlane.Tests.Services.Authentication;

public class JwtTokenServiceTests
{
    private readonly JwtOptions _jwtOptions;
    private readonly Mock<ILogger<JwtTokenService>> _loggerMock;
    private readonly JwtTokenService _sut;

    public JwtTokenServiceTests()
    {
        _jwtOptions = new JwtOptions
        {
            SigningKey = "this-is-a-very-long-secret-key-for-testing-purposes-with-at-least-32-bytes!",
            Issuer = "https://appbridge.local",
            Audience = "appbridge-api",
            ExpiryMinutes = 60,
        };

        _loggerMock = new Mock<ILogger<JwtTokenService>>();

        var jwtOptionsMonitor = Options.Create(_jwtOptions);
        _sut = new JwtTokenService(jwtOptionsMonitor, _loggerMock.Object);
    }

    [Fact]
    public void GenerateToken_WithValidParameters_ReturnsSignedToken()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var userIdentifier = "user@domain.com";
        var tenantId = Guid.NewGuid();

        // Act
        var token = _sut.GenerateToken(userId, userIdentifier, tenantId);

        // Assert
        token.Should().NotBeNullOrEmpty();
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();
    }

    [Fact]
    public void GenerateToken_WithValidParameters_TokenContainsExpectedClaims()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var userIdentifier = "user@domain.com";
        var tenantId = Guid.NewGuid();

        // Act
        var token = _sut.GenerateToken(userId, userIdentifier, tenantId);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == userId);
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.UniqueName && c.Value == userIdentifier);
        jwtToken.Claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == tenantId.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Iat);
    }

    [Fact]
    public void GenerateToken_WithValidParameters_TokenHasCorrectExpiry()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var userIdentifier = "user@domain.com";
        var tenantId = Guid.NewGuid();
        var beforeGeneration = DateTime.UtcNow;

        // Act
        var token = _sut.GenerateToken(userId, userIdentifier, tenantId);
        var afterGeneration = DateTime.UtcNow;

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var expiryTime = jwtToken.ValidTo;

        var expectedMinExpiry = beforeGeneration.AddMinutes(_jwtOptions.ExpiryMinutes);
        var expectedMaxExpiry = afterGeneration.AddMinutes(_jwtOptions.ExpiryMinutes);

        expiryTime.Should().BeOnOrAfter(expectedMinExpiry.AddSeconds(-1)); // Allow 1 second tolerance
        expiryTime.Should().BeOnOrBefore(expectedMaxExpiry.AddSeconds(1));
    }

    [Fact]
    public void GenerateToken_WithValidParameters_TokenHasCorrectIssuer()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var userIdentifier = "user@domain.com";
        var tenantId = Guid.NewGuid();

        // Act
        var token = _sut.GenerateToken(userId, userIdentifier, tenantId);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Issuer.Should().Be(_jwtOptions.Issuer);
        jwtToken.Audiences.Should().Contain(_jwtOptions.Audience);
    }

    [Fact]
    public void ValidateToken_WithValidToken_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var userIdentifier = "user@domain.com";
        var tenantId = Guid.NewGuid();
        var token = _sut.GenerateToken(userId, userIdentifier, tenantId);

        // Act
        var isValid = _sut.ValidateToken(token);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateToken_WithMalformedToken_ReturnsFalse()
    {
        // Arrange
        var malformedToken = "this-is-not-a-jwt-token";

        // Act
        var isValid = _sut.ValidateToken(malformedToken);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateToken_WithTokenSignedWithDifferentKey_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var userIdentifier = "user@domain.com";
        var tenantId = Guid.NewGuid();
        var token = _sut.GenerateToken(userId, userIdentifier, tenantId);

        // Create a new service with different signing key
        var differentJwtOptions = new JwtOptions
        {
            SigningKey = "a-completely-different-secret-key-for-testing-with-32-bytes!",
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            ExpiryMinutes = _jwtOptions.ExpiryMinutes,
        };

        var differentOptionsMonitor = Options.Create(differentJwtOptions);
        var differentService = new JwtTokenService(differentOptionsMonitor, _loggerMock.Object);

        // Act
        var isValid = differentService.ValidateToken(token);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateToken_WithEmptyString_ReturnsFalse()
    {
        // Arrange
        var emptyToken = string.Empty;

        // Act
        var isValid = _sut.ValidateToken(emptyToken);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateToken_WithNullString_ReturnsFalse()
    {
        // Arrange
        string? nullToken = null;

        // Act
        var isValid = _sut.ValidateToken(nullToken!);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void GenerateToken_LogsInformationMessage()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var userIdentifier = "user@domain.com";
        var tenantId = Guid.NewGuid();

        // Act
        var token = _sut.GenerateToken(userId, userIdentifier, tenantId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("JWT token generated")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ValidateToken_WithInvalidToken_LogsWarningMessage()
    {
        // Arrange
        var invalidToken = "invalid.token.here";

        // Act
        var isValid = _sut.ValidateToken(invalidToken);

        // Assert
        isValid.Should().BeFalse();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Token validation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ValidateToken_WithTokenFromDifferentIssuer_ReturnsFalse()
    {
        // Arrange
        var handler = new JwtSecurityTokenHandler();
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "https://different-issuer.com",
            audience: _jwtOptions.Audience,
            claims: new[]
            {
                new System.Security.Claims.Claim(JwtRegisteredClaimNames.Sub, "userId"),
                new System.Security.Claims.Claim("tenant_id", Guid.NewGuid().ToString()),
            },
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: creds);

        var differentIssuerToken = handler.WriteToken(token);

        // Act
        var isValid = _sut.ValidateToken(differentIssuerToken);

        // Assert
        isValid.Should().BeFalse();
    }
}
