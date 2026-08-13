namespace AppBridge.ControlPlane.Api.Idempotency;

/// <summary>
/// PD-04 (API.md §11), resolved by T-504: where idempotency responses live during the 60 s window
/// (ADR-0012 §3, PRE-07). Answer: in-process memory, not a table or a distributed cache — the
/// dogfood/MVP-0 topology is a single Control Plane instance co-located on the session host
/// (ADR-0002), so there is no second process that would ever miss a cache another instance wrote.
/// That assumption breaks the moment the Control Plane runs as more than one instance (V2+); revisit
/// this decision — a shared store behind the same interface — when that happens, not before.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>Keyed by (tenant, idempotency key) — the same key value from two different tenants must never collide.</summary>
    bool TryGet(Guid tenantId, Guid idempotencyKey, out CachedIdempotentResponse? response);

    void Set(Guid tenantId, Guid idempotencyKey, CachedIdempotentResponse response);
}

/// <summary>
/// Exactly what was sent to the client the first time, so a replay is byte-identical rather than
/// merely equivalent — re-deriving the response (e.g. re-signing the .rdp) would work, but ADR-0012
/// §3 says "a mesma resposta", and re-signing also means a second, wasted external process call.
/// </summary>
public sealed record CachedIdempotentResponse(string RequestBodyHash, int StatusCode, string ContentType, string Body);
