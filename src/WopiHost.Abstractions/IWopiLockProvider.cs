using System.Diagnostics.CodeAnalysis;

namespace WopiHost.Abstractions;

/// <summary>
/// Represents an editing file lock provider.
/// </summary>
public interface IWopiLockProvider
{
    /// <summary>
    /// try to get an existing lock by it's identifier.
    /// </summary>
    /// <param name="fileId">the fileId to check for.</param>
    /// <param name="lockInfo">the existing lockInfo if found.</param>
    /// <returns>true if found, false otherwise</returns>
    bool TryGetLock(string fileId, [NotNullWhen(true)] out WopiLockInfo? lockInfo);

    /// <summary>
    /// extend an existing lock on fileId expiration time.
    /// </summary>
    /// <param name="fileId">the fileId to refresh the lock for.</param>
    /// <param name="lockId">include to also update the lockId.</param>
    /// <returns>true if success</returns>
    bool RefreshLock(string fileId, string? lockId = null);

    /// <summary>
    /// create a new lock.
    /// </summary>
    /// <param name="fileId">the fileId to lock.</param>
    /// <param name="lockId">the lock identifier to use.</param>
    /// <returns></returns>
    WopiLockInfo? AddLock(string fileId, string lockId);

    /// <summary>
    /// remove an existing lock from a given fileId.
    /// </summary>
    /// <param name="fileId">the lock for fileId to remove.</param>
    /// <returns>true for success</returns>
    bool RemoveLock(string fileId);
}

/// <summary>
/// Represents an editing file lock.
/// </summary>
public record WopiLockInfo
{
    /// <summary>
    /// The lock identifier
    /// </summary>
    public required string LockId { get; init; }

    /// <summary>
    /// The FileId that is locked
    /// </summary>
    public required string FileId { get; init; }

    /// <summary>
    /// When was the lock created
    /// </summary>
    public DateTimeOffset DateCreated { get; init; }

    /// <summary>
    /// Is this lock expired
    /// </summary>
    /// <remarks>WOPI locks must automatically expire after 30 minutes if not renewed by the WOPI client</remarks>
    public bool Expired => DateCreated.AddMinutes(30) < DateTime.UtcNow;
}
