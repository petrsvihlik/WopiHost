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
public partial class MemoryLockProvider(ILogger<MemoryLockProvider> logger) : IWopiLockProvider
{
    /// <summary>
    /// keyed with fileId
    /// </summary>
    private static readonly ConcurrentDictionary<string, WopiLockInfo> locks = [];

    private readonly ILogger<MemoryLockProvider> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public Task<WopiLockInfo?> GetLockAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (locks.TryGetValue(fileId, out var lockInfo))
        {
            if (lockInfo.Expired)
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
            DateCreated = DateTimeOffset.UtcNow,
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
    public async Task<bool> RefreshLockAsync(string fileId, string? lockId = null, CancellationToken cancellationToken = default)
    {
        var existing = await GetLockAsync(fileId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }
        var updated = existing with
        {
            DateCreated = DateTimeOffset.UtcNow,
            LockId = lockId ?? existing.LockId,
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
}
