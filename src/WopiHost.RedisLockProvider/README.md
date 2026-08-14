# WopiHost.RedisLockProvider

`IWopiLockProvider` implementation backed by Redis, with atomic compare-and-swap via server-side conditional commands (`SET IFEQ`/`DELIFEQ`) and TTL-driven WOPI expiry.

> **Requires Redis 8.4 or later.** The compare-and-swap paths use the conditional commands introduced in Redis 8.4; older servers reject them at runtime. Deployments on older Redis (including managed offerings that lag upstream) should stay on WopiHost.RedisLockProvider v9.x, whose CAS ran as `WATCH`-based transactions.

## When to pick this over the other lock providers

| Provider | Cross-process? | Cross-instance? | Operational complexity | When |
|---|---|---|---|---|
| `MemoryLockProvider` | ❌ | ❌ | None | Single-process development; smoke tests. |
| `WopiAzureLockProvider` | ✅ | ✅ (Azure-coordinated blob leases) | Azure Storage account | Multi-region Azure deployments; strongest exclusion. |
| **`WopiRedisLockProvider`** | ✅ | ✅ (best-effort, single Redis) | Redis container | Most real-world cases — many shops already run Redis for session state / cache and would rather not adopt Azure Blob just for WOPI locks. |

## Best-effort, not Redlock

This provider deliberately does **not** implement [Redlock](https://redis.io/docs/latest/develop/use/patterns/distributed-locks/) (lock acquired against a majority of independent Redis nodes). The reasoning:

- WOPI lock semantics are advisory — the 30-minute server-side TTL is the safety net, not the lock itself. The spec already expects that a lock can be "lost" via expiry without coordination.
- Redlock is operationally heavy (independent Redis nodes, clock-skew bounds, fencing tokens) and famously controversial. The cost isn't justified for advisory locks with a known expiry contract.
- For deployments that need stronger cross-region exclusion, prefer `WopiAzureLockProvider`.

If your Redis instance fails over to a replica with stale state mid-WOPI-session, the worst-case outcome is the same as any other lock provider after the 30-minute window expires: the editor reports a lock conflict and the user can re-acquire. No data loss.

## Atomicity

Each compare-and-swap operation (refresh, unlock-and-relock) writes back with `StringSetAsync(key, value, ttl, ValueCondition.Equal(snapshot))`, which maps onto Redis 8.4's `SET IFEQ`: the server compares the resident value against the snapshot and applies the write only on a byte-exact match — one round-trip, evaluated atomically, with no `WATCH`-style abort-and-retry window. Expired-lock eviction uses the same shape via `StringDeleteAsync(key, ValueCondition.Equal(snapshot))` (`DELIFEQ`). The conformance suite's `RefreshLockAsync_ConcurrentSwapBetweenObservationAndCAS_DoesNotRefresh` test exercises this path against this provider too — a stale caller's snapshot no longer matches the Redis-resident value when the write runs, so it is refused.

## Registration

```csharp
services.AddRedisLockProvider(builder.Configuration);
```

The runnable sample's `Sample:LockProvider` discriminator (`Memory` / `Azure` / `Redis`) dispatches to the typed extension above — see [`sample/WopiHost/ServiceCollectionExtensions.cs`](../../sample/WopiHost/ServiceCollectionExtensions.cs).

Reads `Wopi:LockProvider:ConnectionString` for the StackExchange.Redis connection string. If an `IConnectionMultiplexer` is already registered in DI (e.g. via Aspire's `builder.AddRedisClient("wopi-locks")`), that wins so a single multiplexer is reused across the process.

```jsonc
// appsettings.json
"Wopi": {
  "LockProvider": {
    "ConnectionString": "localhost:6379",
    "KeyPrefix": "wopi:lock:" // optional; default shown
  }
}
```

## Interface contract

`IWopiLockProvider` from `WopiHost.Abstractions`:

```csharp
Task<WopiLockInfo?> GetLockAsync(string fileId, CancellationToken ct = default);
Task<WopiLockInfo?> AddLockAsync(string fileId, string lockId, CancellationToken ct = default);
Task<bool>          RefreshLockAsync(string fileId, string expectedExistingLockId, CancellationToken ct = default);
Task<bool>          RemoveLockAsync(string fileId, CancellationToken ct = default);
Task<bool>          TryUnlockAndRelockAsync(string fileId, string newLockId, string expectedExistingLockId, CancellationToken ct = default);
```

Behaviour is validated against the shared `LockProviderConformanceTests` in `WopiHost.Abstractions.Testing` — the same harness `MemoryLockProvider` and `WopiAzureLockProvider` run through, so all three are bound to the same contract.
