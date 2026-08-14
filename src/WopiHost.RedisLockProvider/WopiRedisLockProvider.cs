using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using WopiHost.Abstractions;

namespace WopiHost.RedisLockProvider;

/// <summary>
/// <see cref="IWopiLockProvider"/> backed by Redis. Each WOPI lock is a string key whose value is
/// a JSON-serialized <see cref="WopiLockInfo"/>; Redis's TTL handles the spec-mandated 30-minute
/// expiry without polling, and compare-and-swap operations run as single conditional commands
/// (<c>SET IFEQ</c> / <c>DELIFEQ</c> via StackExchange.Redis's <c>ValueCondition.Equal</c>,
/// requires Redis 8.4+) so the "match-then-mutate" steps are atomic with respect to other clients.
/// </summary>
/// <remarks>
/// <para>
/// <b>Best-effort, single-instance design.</b> This provider deliberately does NOT implement
/// Redlock (lock acquired against a majority of independent Redis nodes). WOPI lock semantics
/// are advisory — the 30-minute server-side TTL is the safety net, not the lock itself — so the
/// operational cost of Redlock isn't justified. For deployments that need stronger
/// cross-region exclusion, prefer <c>WopiAzureLockProvider</c> (Azure Blob leases give true
/// coordinated exclusion through Azure's distributed lease infrastructure).
/// </para>
/// <para>
/// <b>Atomicity.</b> Refresh and Unlock-and-relock write back with
/// <c>StringSetAsync(key, value, ttl, ValueCondition.Equal(snapshot))</c> — Redis's
/// <c>SET IFEQ</c> compares the resident value against the snapshot server-side and applies the
/// write only on a byte-exact match, in one round-trip with no abort-and-retry window. A stale
/// caller's snapshot no longer matches the resident value, so the write is refused and the call
/// returns false. These conditional commands require <b>Redis 8.4 or later</b>; the
/// <c>RedisMinServerVersion</c> property in the project file declares that floor to the
/// StackExchange.Redis analyzers.
/// </para>
/// </remarks>
/// <param name="multiplexer">StackExchange.Redis connection multiplexer.</param>
/// <param name="logger">Logger.</param>
/// <param name="keyPrefix">Prefix prepended to every Redis key the provider manages.</param>
/// <param name="timeProvider">
/// Clock source for lock timestamps. Defaults to <see cref="TimeProvider.System"/> when not
/// supplied via DI; inject a <c>FakeTimeProvider</c> in tests to make
/// <see cref="WopiLockInfo.DateCreated"/> deterministic. The TTL itself is still computed via
/// this clock so refresh-extends-expiry semantics line up with the in-memory representation
/// tests advance.
/// </param>
/// <param name="lockComparer">
/// Lock-id comparer. Defaults to <see cref="OrdinalWopiLockComparer"/> when not supplied via DI.
/// The .NET-side comparer drives the snapshot match (so <see cref="JsonShapedWopiLockComparer"/>
/// and friends can absorb known client-side lock-id mutations); the Redis-side conditional write
/// always compares the full stored value byte-for-byte, so any concurrent mutation refuses the
/// CAS regardless of comparer choice.
/// </param>
public sealed partial class WopiRedisLockProvider(
    IConnectionMultiplexer multiplexer,
    ILogger<WopiRedisLockProvider> logger,
    string keyPrefix = "wopi:lock:",
    TimeProvider? timeProvider = null,
    IWopiLockComparer? lockComparer = null) : IWopiLockProvider
{
    private readonly IConnectionMultiplexer _multiplexer = multiplexer ?? throw new ArgumentNullException(nameof(multiplexer));
    private readonly ILogger<WopiRedisLockProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string _keyPrefix = keyPrefix ?? throw new ArgumentNullException(nameof(keyPrefix));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly IWopiLockComparer _lockComparer = lockComparer ?? OrdinalWopiLockComparer.Instance;

    private IDatabase Db => _multiplexer.GetDatabase();

    private RedisKey Key(string fileId) => _keyPrefix + fileId;

    private static readonly JsonSerializerOptions s_json = new() { IncludeFields = false };

    /// <inheritdoc />
    public async Task<WopiLockInfo?> GetLockAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var raw = await Db.StringGetAsync(Key(fileId)).ConfigureAwait(false);
        if (!raw.HasValue)
        {
            return null;
        }
        var info = Deserialize(raw!);
        // Even though every write sets EX, a fake-clock test can advance the injected
        // TimeProvider without the Redis server clock moving. Re-checking expiry against the
        // .NET-side clock keeps deterministic tests passing and evicts stale state for free.
        if (info.IsExpiredAt(_timeProvider.GetUtcNow()))
        {
            _ = await Db.KeyDeleteAsync(Key(fileId)).ConfigureAwait(false);
            LogLockExpired(_logger, fileId, info.LockId);
            return null;
        }
        return info;
    }

    /// <inheritdoc />
    public async Task<WopiLockInfo?> AddLockAsync(string fileId, string lockId, CancellationToken cancellationToken = default)
    {
        // Probe and evict any expired lock sitting on the key. GetLockAsync handles eviction in
        // the "fake clock advanced past TTL" case where the Redis server clock hasn't really
        // moved; without it the SET NX below would fail against the stale value.
        var existing = await GetLockAsync(fileId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            LogLockAddRejected(_logger, fileId, lockId, existing.LockId);
            return null;
        }

        var info = new WopiLockInfo
        {
            FileId = fileId,
            LockId = lockId,
            DateCreated = _timeProvider.GetUtcNow(),
        };
        var ttl = TimeSpan.FromMinutes(WopiLockInfo.ExpirationMinutes);

        // SET ... NX EX <ttl> — atomic create-if-not-exists with TTL. Returns true only if no
        // sibling AddLock raced ahead.
        var ok = await Db.StringSetAsync(Key(fileId), Serialize(info), ttl, When.NotExists).ConfigureAwait(false);
        if (!ok)
        {
            // Lost the race. Re-read to surface the winner's lock id for telemetry.
            var winner = await GetLockAsync(fileId, cancellationToken).ConfigureAwait(false);
            LogLockAddRejected(_logger, fileId, lockId, winner?.LockId);
            return null;
        }
        LogLockAcquired(_logger, fileId, lockId);
        return info;
    }

    /// <inheritdoc />
    public Task<bool> RefreshLockAsync(string fileId, string expectedExistingLockId, CancellationToken cancellationToken = default)
        => TryAtomicCasAsync(fileId, expectedExistingLockId,
            snapshot => snapshot with { DateCreated = _timeProvider.GetUtcNow() }, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryUnlockAndRelockAsync(string fileId, string newLockId, string expectedExistingLockId, CancellationToken cancellationToken = default)
        => TryAtomicCasAsync(fileId, expectedExistingLockId,
            snapshot => snapshot with { DateCreated = _timeProvider.GetUtcNow(), LockId = newLockId }, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> RemoveLockAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var removed = await Db.KeyDeleteAsync(Key(fileId)).ConfigureAwait(false);
        if (removed)
        {
            LogLockRemoved(_logger, fileId);
        }
        return removed;
    }

    /// <summary>
    /// Shared "GET snapshot → validate caller's lock-id → conditional write-back" flow that
    /// <see cref="RefreshLockAsync"/> and <see cref="TryUnlockAndRelockAsync"/> both need. They
    /// differ only in <em>how</em> the snapshot is transformed before being written back; the
    /// load, expected-id check, and CAS scaffolding is identical.
    /// </summary>
    /// <remarks>
    /// The write's <c>ValueCondition.Equal(raw)</c> compares the <em>byte-exact</em> resident
    /// value against the snapshot just read, atomically on the server (<c>SET IFEQ</c>,
    /// Redis 8.4+). If anything mutated the value between the read and the write (a sibling
    /// refresh, swap, remove, or expiry), the condition fails and the write is refused. The
    /// .NET-side <see cref="IWopiLockComparer"/> is consulted only for the caller's
    /// <paramref name="expectedExistingLockId"/> against the snapshot's <c>LockId</c>, so
    /// tolerant comparers (e.g. <see cref="JsonShapedWopiLockComparer"/>) still work without
    /// weakening the CAS.
    /// </remarks>
    private async Task<bool> TryAtomicCasAsync(
        string fileId,
        string expectedExistingLockId,
        Func<WopiLockInfo, WopiLockInfo> mutate,
        CancellationToken cancellationToken)
    {
        // StackExchange.Redis APIs don't accept a CancellationToken; the caller's token is
        // honoured at the entrance and the in-flight Redis round-trips are assumed short.
        cancellationToken.ThrowIfCancellationRequested();
        var key = Key(fileId);
        var raw = await Db.StringGetAsync(key).ConfigureAwait(false);
        if (!raw.HasValue)
        {
            return false;
        }
        var snapshot = Deserialize(raw!);
        if (snapshot.IsExpiredAt(_timeProvider.GetUtcNow()))
        {
            // Proactively evict the stale record rather than waiting for Redis's TTL to reap it,
            // so this CAS path cleans up on read like GetLockAsync and the Azure/Memory providers.
            // (Under a fake clock the Redis server clock never moves, so the server-side EX would
            // not evict at all.) The delete is guarded by the same value-equality condition the
            // mutation path uses, so a sibling that refreshed or recreated the lock in between is
            // never clobbered: the condition fails and the delete is skipped.
            _ = await Db.StringDeleteAsync(key, ValueCondition.Equal(raw)).ConfigureAwait(false);
            LogLockExpired(_logger, fileId, snapshot.LockId);
            return false;
        }
        if (!_lockComparer.AreEqual(snapshot.LockId, expectedExistingLockId))
        {
            return false;
        }

        var updated = mutate(snapshot);
        var ttl = TimeSpan.FromMinutes(WopiLockInfo.ExpirationMinutes);
        return await Db.StringSetAsync(key, Serialize(updated), ttl, ValueCondition.Equal(raw)).ConfigureAwait(false);
    }

    private static string Serialize(WopiLockInfo info) => JsonSerializer.Serialize(info, s_json);
    private static WopiLockInfo Deserialize(string raw) =>
        JsonSerializer.Deserialize<WopiLockInfo>(raw, s_json)
        ?? throw new InvalidOperationException("Failed to deserialize WopiLockInfo from Redis value.");
}
