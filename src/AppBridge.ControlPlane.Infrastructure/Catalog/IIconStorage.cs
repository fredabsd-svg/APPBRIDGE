namespace AppBridge.ControlPlane.Infrastructure.Catalog;

/// <summary>
/// PD-03 (API.md §11): the icon binary lives outside the database, referenced by
/// <c>Application.IconRef</c> — a coluna deliberately holds a reference, not the bytes (backup and
/// replication cost, and HTTP already caches this content type well). This interface is the one
/// place that reference gets resolved to actual bytes, so <c>GET /v1/applications/{id}/icon</c>
/// (T-404) doesn't know or care where the file physically lives.
/// </summary>
public interface IIconStorage
{
    /// <summary>Null when <paramref name="iconRef"/> doesn't resolve to a readable file — the caller decides what that means (typically 404).</summary>
    Task<IconFile?> ReadAsync(string iconRef, CancellationToken cancellationToken = default);
}

public sealed record IconFile(byte[] Content, string ContentType);
