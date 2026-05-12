using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;

namespace WopiHost.MemoryLockProvider;

/// <summary>
/// In-memory implementation of <see cref="IWopiLockProvider"/>.
/// </summary>
/// <remarks>
/// State lives in a static <see cref="ConcurrentDictionary{TKey,TValue}"/>; locks survive
/// the lifetime of the process but not multi-instance deployments or restarts. Operations are
/// inherently synchronous and are wrapped in <see cref="Task.FromResult{T}"/> to satisfy the async contract.
/// </remarks>
public partial class MemoryLockProvider : IWopiLockProvider
{
    /// <summary>
    /// keyed with fileId
    /// </summary>
    private static readonly ConcurrentDictionary<string, WopiLockInfo> s_locks = [];

    private readonly ILogger<MemoryLockProvider> _logger;
    private readonly IWopiLockComparer _lockComparer;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="timeProvider">
    /// Clock source for lock timestamps and expiry. Defaults to <see cref="TimeProvider.System"/>
    /// when not supplied via DI; inject a <c>FakeTimeProvider</c> (or any custom
    /// <see cref="TimeProvider"/>) in tests to make expiry deterministic.
    /// </param>
    /// <param name="lockComparer">
    /// Lock-id comparer. Defaults to <see cref="OrdinalWopiLockComparer"/> when not supplied
    /// via DI; replace with a custom comparer (e.g. <see cref="JsonShapedWopiLockComparer"/>)
    /// to absorb known WOPI-client lock-id mutations.
    /// </param>
    public MemoryLockProvider(
        ILogger<MemoryLockProvider> logger,
        TimeProvider? timeProvider = null,
        IWopiLockComparer? lockComparer = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lockComparer = lockComparer ?? OrdinalWopiLockComparer.Instance;
    }

    /// <inheritdoc />
    public Task<WopiLockInfo?> GetLockAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (s_locks.TryGetValue(fileId, out var lockInfo))
        {
            if (lockInfo.IsExpiredAt(_timeProvider.GetUtcNow()))
            {
                _ = s_locks.TryRemove(fileId, out _);
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
        var lockInfo = new WopiLockInfo
        {
            FileId = fileId,
            LockId = lockId,
            DateCreated = _timeProvider.GetUtcNow(),
        };
        if (s_locks.TryAdd(fileId, lockInfo))
        {
            LogLockAcquired(_logger, fileId, lockId);
            return Task.FromResult<WopiLockInfo?>(lockInfo);
        }

        var existingLockId = s_locks.TryGetValue(fileId, out var existing) ? existing.LockId : null;
        LogLockAddRejected(_logger, fileId, lockId, existingLockId);
        return Task.FromResult<WopiLockInfo?>(null);
    }

    /// <inheritdoc />
    public async Task<bool> RefreshLockAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var existing = await GetLockAsync(fileId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }
        var updated = existing with
        {
            DateCreated = _timeProvider.GetUtcNow(),
        };
        return s_locks.TryUpdate(fileId, updated, existing);
    }

    /// <inheritdoc />
    public Task<bool> RemoveLockAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (s_locks.TryRemove(fileId, out var removed))
        {
            LogLockRemoved(_logger, fileId, removed.LockId);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<bool> TryUnlockAndRelockAsync(string fileId, string newLockId, string expectedExistingLockId, CancellationToken cancellationToken = default)
    {
        if (!s_locks.TryGetValue(fileId, out var existing))
        {
            return Task.FromResult(false);
        }
        if (existing.IsExpiredAt(_timeProvider.GetUtcNow()))
        {
            // Best-effort eviction; behave as if no lock exists.
            _ = s_locks.TryRemove(fileId, out _);
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
        // equals the snapshot we read. Any concurrent mutation (refresh, swap, removal) makes
        // the comparand stale and the swap returns false.
        return Task.FromResult(s_locks.TryUpdate(fileId, updated, existing));
    }
}
