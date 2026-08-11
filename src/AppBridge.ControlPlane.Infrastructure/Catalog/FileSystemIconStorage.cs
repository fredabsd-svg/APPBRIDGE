namespace AppBridge.ControlPlane.Infrastructure.Catalog;

/// <summary>See <see cref="IIconStorage"/>. PD-03's "sistema de arquivos" branch — the only one built for MVP-0; an object-storage implementation is a later swap behind the same interface, not a rewrite.</summary>
public sealed class FileSystemIconStorage(IconStorageOptions options) : IIconStorage
{
    public async Task<IconFile?> ReadAsync(string iconRef, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(iconRef);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(path, cancellationToken);
        return new IconFile(content, "image/png");
    }

    /// <summary>
    /// <paramref name="iconRef"/> is admin-set today (only via <c>CatalogSeeder</c>'s fixed dataset,
    /// T-401) rather than end-user input, but resolving it under <see cref="IconStorageOptions.RootPath"/>
    /// and rejecting anything that escapes that root costs two lines and closes off path traversal
    /// (<c>../../etc/passwd</c>-style values) before it's ever a real attack surface, not after.
    /// </summary>
    private string? ResolvePath(string iconRef)
    {
        // Trailing separator on the root before the prefix check — otherwise a sibling directory
        // that merely starts with the same characters (root "/a/b" vs. an escape into "/a/bc/evil")
        // would wrongly pass.
        var root = Path.GetFullPath(options.RootPath) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root, iconRef));
        return candidate.StartsWith(root, StringComparison.Ordinal) ? candidate : null;
    }
}

/// <summary><see cref="RootPath"/> comes from <c>APPBRIDGE_ICON_STORAGE_PATH</c> (docs/SETUP-DEV.md §3) — read once at startup by <c>Program.cs</c>.</summary>
public sealed class IconStorageOptions
{
    public required string RootPath { get; init; }
}
