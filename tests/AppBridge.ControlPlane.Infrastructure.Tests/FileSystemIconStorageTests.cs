using AppBridge.ControlPlane.Infrastructure.Catalog;
using Xunit;

namespace AppBridge.ControlPlane.Infrastructure.Tests;

/// <summary>
/// T-404 (PD-03): <see cref="FileSystemIconStorage"/> resolves <c>Application.IconRef</c> to bytes
/// under a configured root. No PostgreSQL needed here — this is pure filesystem behavior, unlike
/// the rest of this project's Infrastructure tests.
/// </summary>
public sealed class FileSystemIconStorageTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"appbridge-icon-storage-tests-{Guid.NewGuid()}");

    public FileSystemIconStorageTests()
    {
        Directory.CreateDirectory(_rootPath);
    }

    public void Dispose() => Directory.Delete(_rootPath, recursive: true);

    private FileSystemIconStorage NewStorage() => new(new IconStorageOptions { RootPath = _rootPath });

    [Fact]
    public async Task ReadAsync_returns_the_bytes_of_a_file_under_the_root()
    {
        var content = new byte[] { 1, 2, 3, 4 };
        await File.WriteAllBytesAsync(Path.Combine(_rootPath, "app.png"), content);

        var icon = await NewStorage().ReadAsync("app.png");

        Assert.NotNull(icon);
        Assert.Equal(content, icon!.Content);
        Assert.Equal("image/png", icon.ContentType);
    }

    [Fact]
    public async Task ReadAsync_returns_null_when_the_file_does_not_exist()
    {
        Assert.Null(await NewStorage().ReadAsync("does-not-exist.png"));
    }

    [Fact]
    public async Task ReadAsync_refuses_to_escape_the_root_via_path_traversal()
    {
        var secretOutsideRoot = Path.Combine(Path.GetTempPath(), $"appbridge-icon-storage-secret-{Guid.NewGuid()}.txt");
        await File.WriteAllTextAsync(secretOutsideRoot, "not an icon");
        try
        {
            var traversal = Path.Combine("..", Path.GetFileName(secretOutsideRoot));
            Assert.Null(await NewStorage().ReadAsync(traversal));
        }
        finally
        {
            File.Delete(secretOutsideRoot);
        }
    }

    [Fact]
    public async Task ReadAsync_refuses_an_absolute_path_that_points_outside_the_root()
    {
        var secretOutsideRoot = Path.Combine(Path.GetTempPath(), $"appbridge-icon-storage-secret-{Guid.NewGuid()}.txt");
        await File.WriteAllTextAsync(secretOutsideRoot, "not an icon");
        try
        {
            Assert.Null(await NewStorage().ReadAsync(secretOutsideRoot));
        }
        finally
        {
            File.Delete(secretOutsideRoot);
        }
    }
}
