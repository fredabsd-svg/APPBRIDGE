namespace AppBridge.ControlPlane.Services;

public sealed class CatalogIconStore
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    public const long MaximumIconSizeBytes = 1024 * 1024;

    private readonly string _rootPath;
    private readonly StringComparison _pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public CatalogIconStore(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
    }

    public async Task<byte[]?> ReadPngAsync(string iconRef, CancellationToken cancellationToken)
    {
        var path = ResolvePath(iconRef);
        if (path is null || !File.Exists(path) || HasReparsePoint(path))
        {
            return null;
        }

        var file = new FileInfo(path);
        if (file.Length < PngSignature.Length || file.Length > MaximumIconSizeBytes)
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(path, cancellationToken);
        if (content.Length < PngSignature.Length
            || content.Length > MaximumIconSizeBytes
            || !PngSignature.SequenceEqual(content.Take(PngSignature.Length)))
        {
            return null;
        }

        return content;
    }

    private string? ResolvePath(string? iconRef)
    {
        if (string.IsNullOrWhiteSpace(iconRef)
            || iconRef.Length > 512
            || Path.IsPathRooted(iconRef)
            || iconRef.Contains('\\')
            || iconRef.Contains(':')
            || iconRef.Any(char.IsControl)
            || !Path.GetExtension(iconRef).Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var segments = iconRef.Split('/');
        if (segments.Any(segment => segment is "" or "." or ".."
            || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(_rootPath, Path.Combine(segments)));
        var relativePath = Path.GetRelativePath(_rootPath, path);
        if (Path.IsPathRooted(relativePath)
            || relativePath.Equals("..", _pathComparison)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", _pathComparison))
        {
            return null;
        }

        return path;
    }

    private bool HasReparsePoint(string path)
    {
        var relativePath = Path.GetRelativePath(_rootPath, path);
        var currentPath = _rootPath;
        foreach (var segment in relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            if (File.Exists(currentPath) || Directory.Exists(currentPath))
            {
                if ((File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
