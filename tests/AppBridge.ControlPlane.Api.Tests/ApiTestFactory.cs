using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AppBridge.ControlPlane.Api.Tests;

/// <summary>
/// Program.cs requires <c>APPBRIDGE_DB_CONNECTION</c>, <c>APPBRIDGE_JWT_SIGNING_KEY</c> and (T-404)
/// <c>APPBRIDGE_ICON_STORAGE_PATH</c> to start — every test that boots the real host needs all
/// three, even ones that don't touch any directly, since <c>/v1/health</c> itself checks
/// PostgreSQL. Centralized here instead of repeated per test class. Runs under
/// <c>Development</c> so <c>DevIdentityProvider</c> (ADR-0017 §5) is registered.
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

    /// <summary>A well-known minimal valid 1x1 PNG — real bytes to serve/hash, not a decoded image any test needs to render.</summary>
    private static readonly byte[] MinimalPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private readonly string _iconStoragePath;

    public ApiTestFactory()
    {
        var dbConnectionString = Environment.GetEnvironmentVariable("APPBRIDGE_TEST_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set APPBRIDGE_TEST_DB_CONNECTION before running the Api tests (see docs/SETUP-DEV.md).");

        Environment.SetEnvironmentVariable("APPBRIDGE_DB_CONNECTION", dbConnectionString);
        Environment.SetEnvironmentVariable("APPBRIDGE_JWT_SIGNING_KEY", JwtSigningKey);

        // Self-contained rather than pointing at the repo's assets/catalog-icons — this must not
        // depend on the test process's working directory matching the repo layout.
        _iconStoragePath = Path.Combine(Path.GetTempPath(), $"appbridge-test-icons-{Guid.NewGuid()}");
        Directory.CreateDirectory(_iconStoragePath);
        File.WriteAllBytes(Path.Combine(_iconStoragePath, "dominio-contabil.png"), MinimalPng);
        File.WriteAllBytes(Path.Combine(_iconStoragePath, "alterdata.png"), MinimalPng);
        Environment.SetEnvironmentVariable("APPBRIDGE_ICON_STORAGE_PATH", _iconStoragePath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_iconStoragePath))
        {
            Directory.Delete(_iconStoragePath, recursive: true);
        }
    }
}
