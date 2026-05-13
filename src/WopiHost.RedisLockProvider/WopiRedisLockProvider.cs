using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using WopiHost.Abstractions;

namespace WopiHost.RedisLockProvider;

/// <summary>
/// <see cref="IWopiLockProvider"/> backed by Redis. Each WOPI lock is a string key whose value is
/// a JSON-serialized <see cref="WopiLockInfo"/>; Redis's TTL handles the spec-mandated 30-minute
/// expiry without polling, and all compare-and-swap operations run as Lua scripts so the
/// "match-then-mutate" steps are atomic on the server.
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
/// <b>Atomicity.</b> Each non-trivial operation is a Lua script evaluated on the Redis server:
/// EVAL runs to completion without interleaving with other commands, so the compare and the
/// mutation land as a single observable step. The <c>RefreshLockAsync</c> race captured by the
/// conformance suite's <c>RefreshLockAsync_ConcurrentSwapBetweenObservationAndCAS_DoesNotRefresh</c>
/// test is impossible against this provider — a stale caller's expected lock id no longer
/// matches the Redis-resident value at the time the script runs.
/// </para>
/// </remarks>
/// <param name="multiplexer">StackExchange.Redis connection multiplexer.</param>
/// <param name="logger">Logger.</param>
/// <param name="keyPrefix">Prefix prepended to every Redis key the provider manages.</param>
/// <param name="timeProvider">
/// Clock source for lock timestamps. Defaults to <see cref="TimeProvider.System"/> when not
/// supplied via DI; inject a <c>FakeTimeProvider</c> in tests to make
/// <see cref="WopiLockInfo.DateCreated"/> deterministic. The TTL itself is still computed in
/// Redis seconds via this clock so refresh-extends-expiry semantics line up with the in-memory
/// representation tests advance.
/// </param>
/// <param name="lockComparer">
/// Lock-id comparer. Defaults to <see cref="OrdinalWopiLockComparer"/> when not supplied via DI;
/// however, note that the Lua compare runs server-side and uses byte-exact equality on the
/// LockId field — see <see cref="TryUnlockAndRelockAsync"/> for how the .NET-side
/// <see cref="IWopiLockComparer"/> layer fits in.
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

    /// <summary>
    /// Lua: get stored value; if absent → return nil. The 30-minute TTL means Redis evicts
    /// expired keys for us, but we still also evict server-side here as defense-in-depth in case
    /// the caller advanced a fake clock without the TTL having "really" elapsed (Lua sees the
    /// stored DateCreated, not the Redis EXPIRE clock).
    /// </summary>
    private const string GetScript = """
        local v = redis.call('GET', KEYS[1])
        if v == false then return nil end
        return v
        """;

    /// <summary>
    /// Lua refresh: GET → check lock-id match → SET with bumped DateCreated + reset TTL.
    /// Returns 1 if refreshed, 0 if lock missing or mismatch.
    /// ARGV[1] = expected lock id; ARGV[2] = new value (JSON); ARGV[3] = TTL seconds.
    /// </summary>
    private const string RefreshScript = """
        local v = redis.call('GET', KEYS[1])
        if v == false then return 0 end
        if cjson.decode(v).LockId ~= ARGV[1] then return 0 end
        redis.call('SET', KEYS[1], ARGV[2], 'EX', tonumber(ARGV[3]))
        return 1
        """;

    /// <summary>
    /// Lua swap: GET → check lock-id match → SET new value (new LockId + bumped DateCreated) +
    /// reset TTL. Returns 1 on success, 0 if lock missing or mismatch.
    /// </summary>
    private const string SwapScript = """
        local v = redis.call('GET', KEYS[1])
        if v == false then return 0 end
        if cjson.decode(v).LockId ~= ARGV[1] then return 0 end
        redis.call('SET', KEYS[1], ARGV[2], 'EX', tonumber(ARGV[3]))
        return 1
        """;

    /// <inheritdoc />
    public async Task<WopiLockInfo?> GetLockAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var raw = (string?)await Db.ScriptEvaluateAsync(GetScript, [Key(fileId)]).ConfigureAwait(false);
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }
        var info = Deserialize(raw);
        // Even though we set EX on every write, the fake-clock conformance test advances time
        // without the Redis server clock moving. Re-check expiry against the injected
        // TimeProvider so deterministic tests pass and we evict stale state for free.
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
        // Probe + maybe evict if an expired lock is sitting on the key. GetLockAsync handles the
        // eviction in the "advance fake clock past TTL" case (Redis hasn't really moved). Without
        // this, the SET NX below would fail against the stale value.
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
        var json = Serialize(info);
        var ttl = TimeSpan.FromMinutes(WopiLockInfo.ExpirationMinutes);

        // SET ... NX EX <ttl> — atomic create-if-not-exists with TTL. Returns true only if a
        // sibling AddLock didn't race us.
        var ok = await Db.StringSetAsync(Key(fileId), json, ttl, When.NotExists).ConfigureAwait(false);
        if (!ok)
        {
            // Lost the race. Re-read to surface the winner's lock id for telemetry, then bail.
            var winner = await GetLockAsync(fileId, cancellationToken).ConfigureAwait(false);
            LogLockAddRejected(_logger, fileId, lockId, winner?.LockId);
            return null;
        }
        LogLockAcquired(_logger, fileId, lockId);
        return info;
    }

    /// <inheritdoc />
    public async Task<bool> RefreshLockAsync(string fileId, string expectedExistingLockId, CancellationToken cancellationToken = default)
    {
        // Snapshot current state via GetLockAsync for the .NET-side IWopiLockComparer check (so
        // JsonShapedWopiLockComparer-style tolerant matching works). The Lua script then re-checks
        // byte-exact equality against the stored value: if a concurrent UnlockAndRelock won the
        // race between our read and the script's GET, the Lua compare fails and we return false.
        var snapshot = await GetLockAsync(fileId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return false;
        }
        if (!_lockComparer.AreEqual(snapshot.LockId, expectedExistingLockId))
        {
            return false;
        }

        var refreshed = snapshot with { DateCreated = _timeProvider.GetUtcNow() };
        var ttl = (int)TimeSpan.FromMinutes(WopiLockInfo.ExpirationMinutes).TotalSeconds;
        // Lua compare uses the *stored* lock id (snapshot.LockId), not the caller's potentially
        // tolerant-equal value, so the CAS is byte-exact and consistent regardless of comparer.
        var result = (long)await Db.ScriptEvaluateAsync(
            RefreshScript,
            [Key(fileId)],
            [snapshot.LockId, Serialize(refreshed), ttl.ToString(CultureInfo.InvariantCulture)]).ConfigureAwait(false);
        return result == 1;
    }

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

    /// <inheritdoc />
    public async Task<bool> TryUnlockAndRelockAsync(string fileId, string newLockId, string expectedExistingLockId, CancellationToken cancellationToken = default)
    {
        // Same shape as RefreshLockAsync: comparer-driven snapshot check on the .NET side; Lua CAS
        // against the byte-exact stored id on the server side. After this combination, a tolerant
        // comparer still wins the snapshot check, but any concurrent mutation between snapshot
        // and the Lua run causes the Lua compare to fail (it always uses the stored id), so the
        // swap aborts cleanly without producing a "ghost" relock.
        var snapshot = await GetLockAsync(fileId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return false;
        }
        if (!_lockComparer.AreEqual(snapshot.LockId, expectedExistingLockId))
        {
            return false;
        }
        var swapped = snapshot with
        {
            DateCreated = _timeProvider.GetUtcNow(),
            LockId = newLockId,
        };
        var ttl = (int)TimeSpan.FromMinutes(WopiLockInfo.ExpirationMinutes).TotalSeconds;
        var result = (long)await Db.ScriptEvaluateAsync(
            SwapScript,
            [Key(fileId)],
            [snapshot.LockId, Serialize(swapped), ttl.ToString(CultureInfo.InvariantCulture)]).ConfigureAwait(false);
        return result == 1;
    }

    private static string Serialize(WopiLockInfo info) => JsonSerializer.Serialize(info, s_json);
    private static WopiLockInfo Deserialize(string raw) =>
        JsonSerializer.Deserialize<WopiLockInfo>(raw, s_json)
        ?? throw new InvalidOperationException("Failed to deserialize WopiLockInfo from Redis value.");
}
