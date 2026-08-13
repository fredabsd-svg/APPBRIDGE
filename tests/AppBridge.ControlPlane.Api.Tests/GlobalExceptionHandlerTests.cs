using System.Text.Json;
using AppBridge.ControlPlane.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AppBridge.ControlPlane.Api.Tests;

/// <summary>
/// T-505: <see cref="GlobalExceptionHandler"/> is exercised directly against a
/// <see cref="DefaultHttpContext"/> rather than through a real HTTP round trip, because nothing in
/// the Api project currently throws an ordinary, unanticipated exception for an end-to-end test to
/// provoke — <see cref="LaunchEndpointTests"/> covers the one exception type minimal API itself
/// throws (<c>BadHttpRequestException</c>, for a malformed body) through the real pipeline. This
/// file proves the handler's own mapping and RNF-043 guarantee in isolation: any other exception
/// becomes 500 <c>INTERNAL_ERROR</c>, and the response body never contains the exception's own
/// message or type name.
/// </summary>
public sealed class GlobalExceptionHandlerTests
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task An_unanticipated_exception_becomes_500_INTERNAL_ERROR_without_leaking_its_message()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var context = NewContext("/v1/applications", correlationId: "018f-test-correlation");
        var secretException = new InvalidOperationException("connection string: Host=db-prod-01.internal;Password=hunter2");

        var handled = await handler.TryHandleAsync(context, secretException, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        var problem = await ReadBodyAsync(context);
        Assert.Equal("INTERNAL_ERROR", problem.GetProperty("appbridgeCode").GetString());
        Assert.Equal("018f-test-correlation", problem.GetProperty("correlationId").GetString());

        var rawBody = problem.GetRawText();
        Assert.DoesNotContain("hunter2", rawBody);
        Assert.DoesNotContain("db-prod-01", rawBody);
        Assert.DoesNotContain("InvalidOperationException", rawBody);
    }

    [Fact]
    public async Task A_BadHttpRequestException_becomes_400_MALFORMED_REQUEST()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var context = NewContext("/v1/auth/session", correlationId: "018f-test-correlation-2");

        var handled = await handler.TryHandleAsync(context, new BadHttpRequestException("bad body"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var problem = await ReadBodyAsync(context);
        Assert.Equal("MALFORMED_REQUEST", problem.GetProperty("appbridgeCode").GetString());
    }

    [Fact]
    public async Task Missing_correlation_id_on_the_context_does_not_throw_and_still_produces_one()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext { RequestServices = null! };
        context.Request.Path = "/v1/applications";
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context, new Exception("boom"), CancellationToken.None);

        Assert.True(handled);
        var problem = await ReadBodyAsync(context);
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("correlationId").GetString()));
    }

    private static DefaultHttpContext NewContext(string path, string correlationId)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Items[CorrelationIdMiddleware.HeaderName] = correlationId;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body, ReadOptions);
    }
}
