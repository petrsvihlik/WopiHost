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
/// register a single <see cref="MemoryLockProvider"/> as <c>Singleton</c> (the default
/// registration path does this via <c>services.AddMemoryLockProvider()</c>).
/// </para>
/// </remarks>
public partial class MemoryLockProvider : IWopiLockProvider
{
    /// <summary>Per-instance lock store keyed by fileId.</summary>
    private readonly ConcurrentDictionary<string, WopiLockInfo> _locks = new();

    private readonly ILogger<MemoryLockProvider> _logger;
    private readonly IWopiLockComparer _lockComparer;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates a new instance of <see cref="MemoryLockProvider"/>.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="timeProvider">Clock source. Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="lockComparer">Lock-id comparer. Defaults to <see cref="OrdinalWopiLockComparer"/>.</param>
    /// <remarks>
    /// When <paramref name="timeProvider"/> or <paramref name="lockComparer"/> is <see langword="null"/>,
    /// the provider falls back to the default implementation and emits a single Information-level
    /// log line on construction. The log line signals the fallback so a custom
    /// <see cref="IWopiLockComparer"/> (e.g. <c>JsonShapedWopiLockComparer</c>) that was registered
    /// but bypassed by constructing the provider without DI does not silently revert to ordinal
    /// comparison unnoticed.
    /// </remarks>
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
    /// Test-only seed: writes <paramref name="lockInfo"/> directly into the per-instance lock
    /// dictionary. Lets <c>MemoryLockProviderTests</c> exercise eviction paths that depend on
    /// shape (e.g. a stale record with a past <see cref="WopiLockInfo.DateCreated"/>) without
    /// reflecting on the private dictionary field. Conformance-style behavior is driven through
    /// the public <see cref="IWopiLockProvider"/> surface — this hook is exclusively for the
    /// provider's own impl-specific tests.
    /// </summary>
    internal void SeedLockForTesting(string fileId, WopiLockInfo lockInfo)
        => _locks[fileId] = lockInfo;

    /// <summary>
    /// Test-only probe: returns whether <paramref name="fileId"/> currently has an entry in the
    /// per-instance dictionary. Paired with <see cref="SeedLockForTesting"/> to assert eviction
    /// behavior without reaching into private state via reflection.
    /// </summary>
    internal bool ContainsLockForTesting(string fileId) => _locks.ContainsKey(fileId);
}
