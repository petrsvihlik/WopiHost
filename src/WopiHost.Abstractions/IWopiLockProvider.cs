namespace WopiHost.Abstractions;

/// <summary>
/// Provides storage for WOPI file locks.
/// </summary>
/// <remarks>
/// All operations are asynchronous so implementations can talk to out-of-process stores
/// (Azure Blob, Redis, SQL) without blocking; in-process implementations can return completed
/// tasks.
/// </remarks>
public interface IWopiLockProvider
{
    /// <summary>
    /// Gets the current lock on a file.
    /// </summary>
    /// <param name="fileId">The identifier of the file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The existing <see cref="WopiLockInfo"/> if present and not expired; otherwise, <see langword="null"/>.</returns>
    Task<WopiLockInfo?> GetLockAsync(string fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extends an existing lock's expiration time, but only if the stored lock id matches
    /// <paramref name="expectedExistingLockId"/>. Implements the WOPI <c>RefreshLock</c> operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations MUST perform a true compare-and-swap (e.g. <c>ConcurrentDictionary.TryUpdate</c>
    /// against a snapshot, or an ETag-conditional metadata write on a remote store). A naive
    /// <see cref="GetLockAsync"/> followed by a refresh is racy: a concurrent <c>UnlockAndRelock</c>
    /// between the read and the write would silently extend the wrong lock. The lock-id
    /// comparison and the extension must happen in a single atomic step.
    /// </para>
    /// <para>
    /// Spec: <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/refreshlock"/>.
    /// </para>
    /// </remarks>
    /// <param name="fileId">The identifier of the file whose lock to refresh.</param>
    /// <param name="expectedExistingLockId">The lock id the caller observed. The refresh aborts
    /// if no lock is held or the stored lock id no longer matches this value.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the lock was refreshed; <see langword="false"/> if the
    /// lock was missing, expired, or the existing id no longer matches.</returns>
    Task<bool> RefreshLockAsync(string fileId, string expectedExistingLockId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new lock on a file.
    /// </summary>
    /// <param name="fileId">The identifier of the file to lock.</param>
    /// <param name="lockId">The lock id to store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The created <see cref="WopiLockInfo"/>, or <see langword="null"/> if a lock already exists for <paramref name="fileId"/>.</returns>
    Task<WopiLockInfo?> AddLockAsync(string fileId, string lockId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the lock from a file.
    /// </summary>
    /// <param name="fileId">The identifier of the file whose lock to remove.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if a lock was removed; <see langword="false"/> if none was held.</returns>
    Task<bool> RemoveLockAsync(string fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to atomically replace the current lock id with a new one, but only if the existing
    /// lock id matches <paramref name="expectedExistingLockId"/>. Implements the WOPI
    /// <c>UnlockAndRelock</c> operation, which the spec requires to be atomic with respect to
    /// concurrent lock modifications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations MUST perform a true compare-and-swap (e.g. <c>ConcurrentDictionary.TryUpdate</c>
    /// against a snapshot, or an ETag/lease-conditional write on a remote store); a
    /// <see cref="GetLockAsync"/>-then-write sequence is not sufficient.
    /// </para>
    /// <para>
    /// Spec: <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/unlockandrelock"/>.
    /// </para>
    /// </remarks>
    /// <param name="fileId">The identifier of the file whose lock to swap.</param>
    /// <param name="newLockId">The lock id to set.</param>
    /// <param name="expectedExistingLockId">The lock id the caller observed. The swap aborts if
    /// the stored lock id no longer matches this value.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the swap completed; <see langword="false"/> if the lock
    /// was missing, expired, or the existing id no longer matches.</returns>
    Task<bool> TryUnlockAndRelockAsync(string fileId, string newLockId, string expectedExistingLockId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a WOPI lock held on a file.
/// </summary>
public record WopiLockInfo
{
    /// <summary>
    /// The lock auto-expiration window, in minutes, mandated by the WOPI specification.
    /// </summary>
    /// <remarks>
    /// Per <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/lock"/>,
    /// WOPI locks MUST automatically expire after 30 minutes if not renewed by the WOPI client.
    /// This is fixed by the spec and not a tunable.
    /// </remarks>
    public const int ExpirationMinutes = 30;

    /// <summary>
    /// The maximum lock-id length permitted by the WOPI spec when the host advertises
    /// <c>SupportsExtendedLockLength=true</c>.
    /// </summary>
    /// <remarks>
    /// Per the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/concepts#lock">Lock concepts</see>
    /// page: <i>"Contain a lock ID of maximum length 1024 ASCII characters."</i> WopiHost advertises
    /// <c>SupportsExtendedLockLength</c> in <c>CheckFileInfo</c>, so this is the contract limit
    /// upstream WOPI clients are entitled to assume.
    /// </remarks>
    public const int MaxLockIdLength = 1024;

    /// <summary>
    /// Gets the lock id supplied by the WOPI client.
    /// </summary>
    public required string LockId { get; init; }

    /// <summary>
    /// Gets the identifier of the locked file.
    /// </summary>
    public required string FileId { get; init; }

    /// <summary>
    /// Gets the time the lock was created or last refreshed.
    /// </summary>
    public DateTimeOffset DateCreated { get; init; }

    /// <summary>
    /// Determines whether this lock has expired as of <paramref name="now"/>.
    /// </summary>
    /// <remarks>
    /// The lock expires <see cref="ExpirationMinutes"/> minutes after <see cref="DateCreated"/>.
    /// Callers pass an explicit clock reading (typically <see cref="TimeProvider.GetUtcNow"/> on
    /// an injected <see cref="TimeProvider"/>) so expiry is deterministic under a fake clock.
    /// </remarks>
    /// <param name="now">The current time, typically <c>timeProvider.GetUtcNow()</c>.</param>
    /// <returns><see langword="true"/> if the lock has expired; otherwise, <see langword="false"/>.</returns>
    public bool IsExpiredAt(DateTimeOffset now) => DateCreated.AddMinutes(ExpirationMinutes) < now;
}
