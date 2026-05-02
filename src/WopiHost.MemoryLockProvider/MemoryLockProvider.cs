using System.Collections.Concurrent;
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
public class MemoryLockProvider : IWopiLockProvider
{
    /// <summary>
    /// keyed with fileId
    /// </summary>
    private static readonly ConcurrentDictionary<string, WopiLockInfo> locks = [];

    /// <inheritdoc />
    public Task<WopiLockInfo?> GetLockAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (locks.TryGetValue(fileId, out var lockInfo))
        {
            if (lockInfo.Expired)
            {
                _ = locks.TryRemove(fileId, out _);
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
        return Task.FromResult<WopiLockInfo?>(locks.TryAdd(fileId, lockInfo) ? lockInfo : null);
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
        => Task.FromResult(locks.TryRemove(fileId, out _));
}
