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
/// The dictionary was a static field in earlier versions, which meant two provider instances in
/// the same process saw a shared store — surprising for a per-instance-shaped class and
/// hostile to test isolation (#380 item 2.3). It's an instance field now; for processes that
/// genuinely need a shared dictionary across providers, register a single
/// <see cref="MemoryLockProvider"/> as <c>Singleton</c> (the default registration path does this
/// via <c>services.AddMemoryLockProvider()</c>).
/// </para>
/// </remarks>
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
public partial class MemoryLockProvider(
    ILogger<MemoryLockProvider> logger,
    TimeProvider? timeProvider = null,
    IWopiLockComparer? lockComparer = null) : IWopiLockProvider
{
    /// <summary>Per-instance lock store keyed by fileId.</summary>
    private readonly ConcurrentDictionary<string, WopiLockInfo> _locks = new();

    private readonly ILogger<MemoryLockProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IWopiLockComparer _lockComparer = lockComparer ?? OrdinalWopiLockComparer.Instance;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

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
        var lockInfo = new WopiLockInfo
        {
            FileId = fileId,
            LockId = lockId,
            DateCreated = _timeProvider.GetUtcNow(),
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
        // Same atomic CAS pattern as TryUnlockAndRelockAsync — without this, a concurrent
        // UnlockAndRelock between our compare and our write would silently extend the wrong lock.
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
        // equals the snapshot we read. Any concurrent mutation (refresh, swap, removal) makes
        // the comparand stale and the swap returns false.
        return Task.FromResult(_locks.TryUpdate(fileId, updated, existing));
    }
}
