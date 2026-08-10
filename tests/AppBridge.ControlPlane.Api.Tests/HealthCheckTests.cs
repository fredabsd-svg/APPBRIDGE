using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AppBridge.ControlPlane.Api.Tests;

// T-201 — health check acceptance criterion: "/health responde".
public sealed class HealthCheckTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_endpoint_returns_200_and_healthy_status()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"healthy\"", body);
    }

    [Fact]
    public async Task Health_response_does_not_leak_internal_details()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/health");
        var body = await response.Content.ReadAsStringAsync();

        // RNF-043: no host name, file path or exception text in a response any client can read.
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, body);
    }
}
