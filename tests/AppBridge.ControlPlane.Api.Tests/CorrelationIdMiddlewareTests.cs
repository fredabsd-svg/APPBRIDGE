using Xunit;

namespace AppBridge.ControlPlane.Api.Tests;

// T-201 — RNF-039: a launch (or any request) must be traceable end to end via correlation id.
public sealed class CorrelationIdMiddlewareTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private const string HeaderName = "X-Correlation-Id";

    [Fact]
    public async Task Server_generates_a_correlation_id_when_the_client_sends_none()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/health");

        Assert.True(response.Headers.TryGetValues(HeaderName, out var values));
        Assert.True(Guid.TryParse(values!.Single(), out _));
    }

    [Fact]
    public async Task Server_echoes_back_the_client_supplied_correlation_id()
    {
        var client = factory.CreateClient();
        var sent = Guid.NewGuid().ToString();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/health");
        request.Headers.Add(HeaderName, sent);
        var response = await client.SendAsync(request);

        Assert.Equal(sent, response.Headers.GetValues(HeaderName).Single());
    }

    [Fact]
    public async Task Malformed_client_correlation_id_is_replaced_rather_than_trusted_verbatim()
    {
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/health");
        request.Headers.Add(HeaderName, "not-a-uuid");
        var response = await client.SendAsync(request);

        var received = response.Headers.GetValues(HeaderName).Single();
        Assert.True(Guid.TryParse(received, out _));
        Assert.NotEqual("not-a-uuid", received);
    }
}
