using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AppBridge.ControlPlane.Api.Tests;

/// <summary>
/// Wiring a correlation id into a logger scope proves nothing on its own — if no line in the
/// request path actually logs, the scope never reaches output and RNF-039 is not really met.
/// These tests capture the real log lines and assert the correlation id is on them.
/// </summary>
public sealed class RequestLoggingTests
{
    [Fact]
    public async Task A_request_produces_at_least_one_log_line_carrying_its_correlation_id()
    {
        var sink = new CapturingLoggerProvider();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(sink)));
        var client = factory.CreateClient();
        var correlationId = Guid.NewGuid().ToString();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/health");
        request.Headers.Add("X-Correlation-Id", correlationId);
        await client.SendAsync(request);

        var matching = sink.Entries.Where(e => e.Scopes.Any(s => s.Contains(correlationId))).ToList();

        Assert.NotEmpty(matching);
        Assert.Contains(matching, e => e.Message.Contains("Request starting"));
        Assert.Contains(matching, e => e.Message.Contains("Request finished"));
    }

    [Fact]
    public async Task Two_concurrent_requests_do_not_cross_contaminate_correlation_ids()
    {
        var sink = new CapturingLoggerProvider();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(sink)));
        var client = factory.CreateClient();
        var idA = Guid.NewGuid().ToString();
        var idB = Guid.NewGuid().ToString();

        async Task Send(string correlationId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/health");
            request.Headers.Add("X-Correlation-Id", correlationId);
            await client.SendAsync(request);
        }

        await Task.WhenAll(Send(idA), Send(idB));

        var linesForA = sink.Entries.Where(e => e.Scopes.Any(s => s.Contains(idA))).ToList();
        var linesForB = sink.Entries.Where(e => e.Scopes.Any(s => s.Contains(idB))).ToList();

        Assert.NotEmpty(linesForA);
        Assert.NotEmpty(linesForB);
        Assert.DoesNotContain(linesForA, e => e.Scopes.Any(s => s.Contains(idB)));
        Assert.DoesNotContain(linesForB, e => e.Scopes.Any(s => s.Contains(idA)));
    }

    private sealed record LogEntry(string Message, IReadOnlyList<string> Scopes);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<LogEntry> _entries = [];
        public IReadOnlyList<LogEntry> Entries => _entries;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            private static readonly AsyncLocal<List<object?>> ScopeStack = new();

            public IDisposable BeginScope<TState>(TState state) where TState : notnull
            {
                ScopeStack.Value ??= [];
                ScopeStack.Value.Add(state);
                var index = ScopeStack.Value.Count - 1;
                return new PopOnDispose(() => ScopeStack.Value?.RemoveAt(index));
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var scopes = (ScopeStack.Value ?? [])
                    .Select(s => JsonSerializer.Serialize(s))
                    .ToList();
                lock (owner._entries)
                {
                    owner._entries.Add(new LogEntry(formatter(state, exception), scopes));
                }
            }

            private sealed class PopOnDispose(Action onDispose) : IDisposable
            {
                public void Dispose() => onDispose();
            }
        }
    }
}
