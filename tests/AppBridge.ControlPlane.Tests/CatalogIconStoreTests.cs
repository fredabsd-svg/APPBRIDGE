using AppBridge.ControlPlane.Services;
using Xunit;

namespace AppBridge.ControlPlane.Tests;

public sealed class CatalogIconStoreTests
{
    [Fact]
    public async Task Reads_a_png_from_the_configured_root()
    {
        var root = CreateTemporaryDirectory();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "accounting"));
            var icon = PngSignature().Concat(new byte[] { 1, 2, 3, 4 }).ToArray();
            await File.WriteAllBytesAsync(Path.Combine(root, "accounting", "domain.png"), icon, cancellationToken);

            var result = await new CatalogIconStore(root).ReadPngAsync("accounting/domain.png", cancellationToken);

            Assert.Equal(icon, result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("../outside.png")]
    [InlineData("assets/../../outside.png")]
    [InlineData("/tmp/outside.png")]
    [InlineData("..\\outside.png")]
    [InlineData("C:\\outside.png")]
    [InlineData("icon.svg")]
    public async Task Rejects_invalid_or_escaping_icon_references(string iconRef)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var result = await new CatalogIconStore(root).ReadPngAsync(iconRef, CancellationToken.None);

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Rejects_non_png_and_oversized_files()
    {
        var root = CreateTemporaryDirectory();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(root, "not-png.png"), [0, 1, 2, 3, 4, 5, 6, 7, 8], cancellationToken);
            await File.WriteAllBytesAsync(
                Path.Combine(root, "large.png"),
                new byte[CatalogIconStore.MaximumIconSizeBytes + 1], cancellationToken);

            var store = new CatalogIconStore(root);

            Assert.Null(await store.ReadPngAsync("not-png.png", cancellationToken));
            Assert.Null(await store.ReadPngAsync("large.png", cancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Rejects_symbolic_links_that_escape_the_configured_root()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = CreateTemporaryDirectory();
        var outside = CreateTemporaryDirectory();
        try
        {
            var outsideFile = Path.Combine(outside, "outside.png");
            await File.WriteAllBytesAsync(outsideFile, PngSignature(), TestContext.Current.CancellationToken);
            File.CreateSymbolicLink(Path.Combine(root, "linked.png"), outsideFile);

            var result = await new CatalogIconStore(root).ReadPngAsync(
                "linked.png", TestContext.Current.CancellationToken);

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    private static byte[] PngSignature() => [137, 80, 78, 71, 13, 10, 26, 10];

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"appbridge-icons-{Guid.CreateVersion7():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
