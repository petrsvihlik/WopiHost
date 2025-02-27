using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using WopiHost.Abstractions;

namespace WopiHost.MemoryLockProvider;

/// <summary>
/// In-memory implementation of a <see cref="IWopiLockProvider"/>
/// </summary>
public class MemoryLockProvider : IWopiLockProvider
{
    /// <summary>
    /// keyed with fileId
    /// </summary>
    private static readonly ConcurrentDictionary<string, WopiLockInfo> locks = [];

    /// <inheritdoc />
    public bool TryGetLock(string fileId, [NotNullWhen(true)] out WopiLockInfo? lockInfo)
    {
        if (locks.TryGetValue(fileId, out lockInfo))
        {
            if (lockInfo.Expired)
            {
                _ = RemoveLock(fileId);
                return false;
            }
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <inheritdoc />
    public WopiLockInfo? AddLock(string fileId, string lockId)
    {
        var lockInfo = new WopiLockInfo
        {
            FileId = fileId,
            LockId = lockId,
            DateCreated = DateTimeOffset.UtcNow,
        };
        if (locks.TryAdd(fileId, lockInfo))
        {
            return lockInfo;
        }
        else
        {
            return null;
        }
    }

    /// <inheritdoc />
    public bool RefreshLock(string fileId, string? lockId = null)
    {
        if (TryGetLock(fileId, out var existingLock))
        {
            var lockInfo = existingLock with
            {
                DateCreated = DateTimeOffset.UtcNow,
                LockId = lockId ?? existingLock.LockId,
            };
            return locks.TryUpdate(fileId, lockInfo, existingLock);
        }
        return false;
    }


    /// <inheritdoc />
    public bool RemoveLock(string fileId)
    {
        return locks.TryRemove(fileId, out _);
    }
}
