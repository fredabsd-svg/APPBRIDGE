using Microsoft.Extensions.Caching.Memory;

namespace AppBridge.ControlPlane.Api.Idempotency;

/// <summary>See <see cref="IIdempotencyStore"/>.</summary>
public sealed class MemoryIdempotencyStore(IMemoryCache cache) : IIdempotencyStore
{
    /// <summary>PRE-07 / ADR-0012 §3 — the same window the signed .rdp itself is valid for.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    public bool TryGet(Guid tenantId, Guid idempotencyKey, out CachedIdempotentResponse? response) =>
        cache.TryGetValue(Key(tenantId, idempotencyKey), out response);

    public void Set(Guid tenantId, Guid idempotencyKey, CachedIdempotentResponse response) =>
        cache.Set(Key(tenantId, idempotencyKey), response, Window);

    private static string Key(Guid tenantId, Guid idempotencyKey) => $"{tenantId:N}:{idempotencyKey:N}";
}
