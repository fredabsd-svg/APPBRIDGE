using AppBridge.ControlPlane.Api.Models;
using FluentAssertions;

namespace AppBridge.ControlPlane.Tests.Api;

public class ErrorResponseTests
{
    [Fact]
    public void ErrorResponse_WithAllProperties_SerializesCorrectly()
    {
        // Arrange
        var validationErrors = new Dictionary<string, string[]>
        {
            { "Email", new[] { "Invalid email format" } },
            { "Password", new[] { "Password must be at least 8 characters" } },
        };

        var response = new ErrorResponse
        {
            Code = "VALIDATION_ERROR",
            Message = "One or more validation errors occurred.",
            TraceId = "test-trace-123",
            ValidationErrors = validationErrors,
        };

        // Act & Assert
        response.Code.Should().Be("VALIDATION_ERROR");
        response.Message.Should().Be("One or more validation errors occurred.");
        response.TraceId.Should().Be("test-trace-123");
        response.ValidationErrors.Should().HaveCount(2);
        response.ValidationErrors!["Email"].Should().Contain("Invalid email format");
    }

    [Fact]
    public void ErrorResponse_MinimalProperties_CreatesValidObject()
    {
        // Arrange & Act
        var response = new ErrorResponse
        {
            Code = "INTERNAL_ERROR",
            Message = "An unexpected error occurred.",
        };

        // Assert
        response.Code.Should().Be("INTERNAL_ERROR");
        response.Message.Should().Be("An unexpected error occurred.");
        response.TraceId.Should().BeNull();
        response.ValidationErrors.Should().BeNull();
    }

    [Fact]
    public void ProblemDetails_WithAllProperties_SerializesCorrectly()
    {
        // Arrange
        var extensions = new Dictionary<string, object>
        {
            { "field", "email" },
            { "code", "INVALID_FORMAT" },
        };

        var details = new ProblemDetails
        {
            Type = "https://api.appbridge.io/errors/validation",
            Title = "Validation Failed",
            Status = 400,
            Detail = "The email field has an invalid format.",
            Instance = "/api/auth/login",
            Extensions = extensions,
        };

        // Act & Assert
        details.Type.Should().Be("https://api.appbridge.io/errors/validation");
        details.Title.Should().Be("Validation Failed");
        details.Status.Should().Be(400);
        details.Detail.Should().Be("The email field has an invalid format.");
        details.Instance.Should().Be("/api/auth/login");
        details.Extensions.Should().HaveCount(2);
    }
}
