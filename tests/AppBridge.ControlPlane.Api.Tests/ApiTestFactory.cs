using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AppBridge.ControlPlane.Api.Tests;

/// <summary>
/// Program.cs requires <c>APPBRIDGE_DB_CONNECTION</c> and <c>APPBRIDGE_JWT_SIGNING_KEY</c> to start
/// (T-301) — every test that boots the real host needs both, even ones that don't touch either
/// directly, since <c>/v1/health</c> itself now checks PostgreSQL. Centralized here instead of
/// repeated per test class. Runs under <c>Development</c> so <c>DevIdentityProvider</c> (ADR-0017
/// §5) is registered.
///
/// Sets real process environment variables, not a <c>ConfigureAppConfiguration</c> overlay:
/// <c>WebApplication.CreateBuilder(args)</c> reads environment variables as one of its own default
/// sources, synchronously, at the top of <c>Program.cs</c> — before <c>WebApplicationFactory</c>'s
/// configuration hooks can run (those apply at the <c>Build()</c> interception boundary, which is
/// too late for Program.cs's own "value or throw" checks right after <c>CreateBuilder</c> returns).
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    public const string JwtSigningKey = "test-only-jwt-signing-key-at-least-32-bytes-long";

    public ApiTestFactory()
    {
        var dbConnectionString = Environment.GetEnvironmentVariable("APPBRIDGE_TEST_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set APPBRIDGE_TEST_DB_CONNECTION before running the Api tests (see docs/SETUP-DEV.md).");

        Environment.SetEnvironmentVariable("APPBRIDGE_DB_CONNECTION", dbConnectionString);
        Environment.SetEnvironmentVariable("APPBRIDGE_JWT_SIGNING_KEY", JwtSigningKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");
}
