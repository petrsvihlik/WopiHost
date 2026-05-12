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
    private static readonly ConcurrentDictionary<string, WopiLockInfo> locks = [];

    private readonly ILogger<MemoryLockProvider> logger;
    private readonly IWopiLockComparer lockComparer;
    private readonly TimeProvider timeProvider;

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
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.lockComparer = lockComparer ?? OrdinalWopiLockComparer.Instance;
    }

    /// <inheritdoc />
    public Task<WopiLockInfo?> GetLockAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (locks.TryGetValue(fileId, out var lockInfo))
        {
            if (lockInfo.IsExpiredAt(timeProvider.GetUtcNow()))
            {
                _ = locks.TryRemove(fileId, out _);
                LogLockExpired(logger, fileId, lockInfo.LockId);
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
            DateCreated = timeProvider.GetUtcNow(),
        };
        if (locks.TryAdd(fileId, lockInfo))
        {
            LogLockAcquired(logger, fileId, lockId);
            return Task.FromResult<WopiLockInfo?>(lockInfo);
        }

        var existingLockId = locks.TryGetValue(fileId, out var existing) ? existing.LockId : null;
        LogLockAddRejected(logger, fileId, lockId, existingLockId);
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
            DateCreated = timeProvider.GetUtcNow(),
        };
        return locks.TryUpdate(fileId, updated, existing);
    }

    /// <inheritdoc />
    public Task<bool> RemoveLockAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (locks.TryRemove(fileId, out var removed))
        {
            LogLockRemoved(logger, fileId, removed.LockId);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<bool> TryUnlockAndRelockAsync(string fileId, string newLockId, string expectedExistingLockId, CancellationToken cancellationToken = default)
    {
        if (!locks.TryGetValue(fileId, out var existing))
        {
            return Task.FromResult(false);
        }
        if (existing.IsExpiredAt(timeProvider.GetUtcNow()))
        {
            // Best-effort eviction; behave as if no lock exists.
            _ = locks.TryRemove(fileId, out _);
            LogLockExpired(logger, fileId, existing.LockId);
            return Task.FromResult(false);
        }
        if (!lockComparer.AreEqual(existing.LockId, expectedExistingLockId))
        {
            return Task.FromResult(false);
        }
        var updated = existing with
        {
            DateCreated = timeProvider.GetUtcNow(),
            LockId = newLockId,
        };
        // TryUpdate is the atomic CAS: succeeds only if the dictionary's current value still
        // equals the snapshot we read. Any concurrent mutation (refresh, swap, removal) makes
        // the comparand stale and the swap returns false.
        return Task.FromResult(locks.TryUpdate(fileId, updated, existing));
    }
}
