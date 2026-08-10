using AppBridge.ControlPlane.Infrastructure.Services.Authentication;
using FluentAssertions;

namespace AppBridge.ControlPlane.Tests.Services.Authentication;

public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _sut;

    public BcryptPasswordHasherTests()
    {
        _sut = new BcryptPasswordHasher();
    }

    [Fact]
    public void HashPassword_WithValidPassword_ReturnsHashedPassword()
    {
        // Arrange
        var password = "SecurePassword123!";

        // Act
        var hash = _sut.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe(password);
        hash.Should().StartWith("$2");
    }

    [Fact]
    public void HashPassword_SamePasswordDifferentHashes()
    {
        // Arrange
        var password = "SecurePassword123!";

        // Act
        var hash1 = _sut.HashPassword(password);
        var hash2 = _sut.HashPassword(password);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "SecurePassword123!";
        var hash = _sut.HashPassword(password);

        // Act
        var result = _sut.VerifyPassword(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var password = "SecurePassword123!";
        var wrongPassword = "WrongPassword123!";
        var hash = _sut.HashPassword(password);

        // Act
        var result = _sut.VerifyPassword(wrongPassword, hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_WithInvalidHash_ReturnsFalse()
    {
        // Arrange
        var password = "SecurePassword123!";
        var invalidHash = "not-a-valid-hash";

        // Act
        var result = _sut.VerifyPassword(password, invalidHash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_WithEmptyPassword_ReturnsFalse()
    {
        // Arrange
        var password = "";
        var hash = _sut.HashPassword("SomePassword123!");

        // Act
        var result = _sut.VerifyPassword(password, hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_WithCaseSensitivity_ReturnsFalse()
    {
        // Arrange
        var password = "SecurePassword123!";
        var wrongCase = "securepassword123!";
        var hash = _sut.HashPassword(password);

        // Act
        var result = _sut.VerifyPassword(wrongCase, hash);

        // Assert
        result.Should().BeFalse();
    }
}
