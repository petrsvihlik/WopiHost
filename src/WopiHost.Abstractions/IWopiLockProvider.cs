namespace WopiHost.Abstractions;

/// <summary>
/// Represents an editing file lock provider.
/// </summary>
/// <remarks>
/// All operations are asynchronous so implementations can talk to out-of-process
/// stores (Azure Blob, Redis, SQL, etc.) without blocking. The in-process
/// <c>MemoryLockProvider</c> simply wraps its synchronous logic in <see cref="Task.FromResult{T}"/>.
/// </remarks>
public interface IWopiLockProvider
{
    /// <summary>
    /// Try to get an existing lock by its identifier.
    /// </summary>
    /// <param name="fileId">the fileId to check for.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>the existing <see cref="WopiLockInfo"/> if present and not expired; <see langword="null"/> otherwise.</returns>
    Task<WopiLockInfo?> GetLockAsync(string fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extend an existing lock's expiration time.
    /// </summary>
    /// <param name="fileId">the fileId to refresh the lock for.</param>
    /// <param name="lockId">include to also update the lockId.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>true if the lock was successfully refreshed</returns>
    Task<bool> RefreshLockAsync(string fileId, string? lockId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new lock.
    /// </summary>
    /// <param name="fileId">the fileId to lock.</param>
    /// <param name="lockId">the lock identifier to use.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>the created <see cref="WopiLockInfo"/>, or <see langword="null"/> if a lock already exists for <paramref name="fileId"/>.</returns>
    Task<WopiLockInfo?> AddLockAsync(string fileId, string lockId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove an existing lock from a given fileId.
    /// </summary>
    /// <param name="fileId">the lock for fileId to remove.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>true if a lock was removed</returns>
    Task<bool> RemoveLockAsync(string fileId, CancellationToken cancellationToken = default);
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
