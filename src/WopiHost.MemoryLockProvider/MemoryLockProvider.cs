using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;

namespace WopiHost.MemoryLockProvider;

/// <summary>
/// In-memory implementation of <see cref="IWopiLockProvider"/>.
/// </summary>
/// <remarks>
/// State lives on an instance-scoped <see cref="ConcurrentDictionary{TKey,TValue}"/>; locks
/// survive the lifetime of this provider instance but not multi-instance deployments or restarts.
/// Operations are inherently synchronous and are wrapped in <see cref="Task.FromResult{T}"/> to
/// satisfy the async contract.
/// <para>
/// The dictionary is an instance field, so two provider instances in the same process have
/// independent stores. For processes that genuinely need a shared dictionary across providers,
/// register a single <see cref="MemoryLockProvider"/> as <c>Singleton</c>, as
/// <see cref="ServiceCollectionExtensions.AddMemoryLockProvider"/> does.
/// </para>
/// </remarks>
public partial class MemoryLockProvider : IWopiLockProvider
{
    /// <summary>Per-instance lock store keyed by fileId.</summary>
    private readonly ConcurrentDictionary<string, WopiLockInfo> _locks = new();

    private readonly ILogger<MemoryLockProvider> _logger;
    private readonly IWopiLockComparer _lockComparer;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="MemoryLockProvider"/> class.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="timeProvider">The clock source, or <see langword="null"/> to use <see cref="TimeProvider.System"/>.</param>
    /// <param name="lockComparer">The lock-id comparer, or <see langword="null"/> to use <see cref="OrdinalWopiLockComparer"/>.</param>
    /// <remarks>
    /// When <paramref name="timeProvider"/> or <paramref name="lockComparer"/> is <see langword="null"/>,
    /// the provider falls back to the default implementation, and each fallback emits one
    /// Information-level log line on construction. The log line signals the fallback so a custom
    /// <see cref="IWopiLockComparer"/> (e.g. <c>JsonShapedWopiLockComparer</c>) that was registered
    /// but bypassed by constructing the provider without DI does not silently revert to ordinal
    /// comparison unnoticed.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
    public MemoryLockProvider(
        ILogger<MemoryLockProvider> logger,
        TimeProvider? timeProvider = null,
        IWopiLockComparer? lockComparer = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lockComparer = lockComparer ?? OrdinalWopiLockComparer.Instance;

        if (timeProvider is null) LogTimeProviderFallback(_logger);
        if (lockComparer is null) LogLockComparerFallback(_logger);
    }

    /// <inheritdoc />
    public Task<WopiLockInfo?> GetLockAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (_locks.TryGetValue(fileId, out var lockInfo))
        {
            if (lockInfo.IsExpiredAt(_timeProvider.GetUtcNow()))
            {
                _ = _locks.TryRemove(fileId, out _);
                LogLockExpired(_logger, fileId, lockInfo.LockId);
                return Task.FromResult<WopiLockInfo?>(null);
            }
            return Task.FromResult<WopiLockInfo?>(lockInfo);
        }
        return Task.FromResult<WopiLockInfo?>(null);
    }

    /// <inheritdoc />
    public Task<WopiLockInfo?> AddLockAsync(string fileId, string lockId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        // An expired-but-resident lock is contractually "no lock" (GetLockAsync evicts it), so drop a
        // stale entry before TryAdd — otherwise it would spuriously reject a takeover that the Azure
        // and Redis providers allow. The KeyValuePair overload removes only the exact stale record,
        // leaving a concurrent acquire that already replaced it intact.
        if (_locks.TryGetValue(fileId, out var stale) && stale.IsExpiredAt(now))
        {
            _ = _locks.TryRemove(new KeyValuePair<string, WopiLockInfo>(fileId, stale));
        }

        var lockInfo = new WopiLockInfo
        {
            FileId = fileId,
            LockId = lockId,
            DateCreated = now,
        };
        if (_locks.TryAdd(fileId, lockInfo))
        {
            LogLockAcquired(_logger, fileId, lockId);
            return Task.FromResult<WopiLockInfo?>(lockInfo);
        }

        var existingLockId = _locks.TryGetValue(fileId, out var existing) ? existing.LockId : null;
        LogLockAddRejected(_logger, fileId, lockId, existingLockId);
        return Task.FromResult<WopiLockInfo?>(null);
    }

    /// <inheritdoc />
    public Task<bool> RefreshLockAsync(string fileId, string expectedExistingLockId, CancellationToken cancellationToken = default)
    {
        if (!_locks.TryGetValue(fileId, out var existing))
        {
            return Task.FromResult(false);
        }
        if (existing.IsExpiredAt(_timeProvider.GetUtcNow()))
        {
            // Best-effort eviction; behave as if no lock exists.
            _ = _locks.TryRemove(fileId, out _);
            LogLockExpired(_logger, fileId, existing.LockId);
            return Task.FromResult(false);
        }
        if (!_lockComparer.AreEqual(existing.LockId, expectedExistingLockId))
        {
            return Task.FromResult(false);
        }
        var updated = existing with
        {
            DateCreated = _timeProvider.GetUtcNow(),
        };
        // Same atomic CAS pattern as TryUnlockAndRelockAsync — without it, a concurrent
        // UnlockAndRelock between the compare and the write would silently extend the wrong lock.
        return Task.FromResult(_locks.TryUpdate(fileId, updated, existing));
    }

    /// <inheritdoc />
    public Task<bool> RemoveLockAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (_locks.TryRemove(fileId, out var removed))
        {
            LogLockRemoved(_logger, fileId, removed.LockId);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<bool> TryUnlockAndRelockAsync(string fileId, string newLockId, string expectedExistingLockId, CancellationToken cancellationToken = default)
    {
        if (!_locks.TryGetValue(fileId, out var existing))
        {
            return Task.FromResult(false);
        }
        if (existing.IsExpiredAt(_timeProvider.GetUtcNow()))
        {
            // Best-effort eviction; behave as if no lock exists.
            _ = _locks.TryRemove(fileId, out _);
            LogLockExpired(_logger, fileId, existing.LockId);
            return Task.FromResult(false);
        }
        if (!_lockComparer.AreEqual(existing.LockId, expectedExistingLockId))
        {
            return Task.FromResult(false);
        }
        var updated = existing with
        {
            DateCreated = _timeProvider.GetUtcNow(),
            LockId = newLockId,
        };
        // TryUpdate is the atomic CAS: succeeds only if the dictionary's current value still
        // equals the snapshot just read. Any concurrent mutation (refresh, swap, removal) makes
        // the comparand stale and the swap returns false.
        return Task.FromResult(_locks.TryUpdate(fileId, updated, existing));
    }

    /// <summary>
    /// Writes <paramref name="lockInfo"/> directly into the per-instance lock dictionary, bypassing
    /// the acquire logic. Test-only: seeds shapes such as a stale record with a past
    /// <see cref="WopiLockInfo.DateCreated"/> to exercise eviction paths.
    /// </summary>
    /// <param name="fileId">The identifier of the file to seed a lock for.</param>
    /// <param name="lockInfo">The lock record to store as-is.</param>
    internal void SeedLockForTesting(string fileId, WopiLockInfo lockInfo)
        => _locks[fileId] = lockInfo;

    /// <summary>
    /// Determines whether <paramref name="fileId"/> currently has an entry in the per-instance
    /// dictionary, expired or not. Test-only counterpart to <see cref="SeedLockForTesting"/>.
    /// </summary>
    /// <param name="fileId">The identifier of the file to check.</param>
    /// <returns><see langword="true"/> if an entry exists for <paramref name="fileId"/>; otherwise, <see langword="false"/>.</returns>
    internal bool ContainsLockForTesting(string fileId) => _locks.ContainsKey(fileId);
}
