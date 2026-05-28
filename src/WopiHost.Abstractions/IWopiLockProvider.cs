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
    /// Extend an existing lock's expiration time — but only if the stored lock id matches
    /// <paramref name="expectedExistingLockId"/>. Implements the WOPI <c>RefreshLock</c>
    /// operation, which the spec requires the host to honour only when the caller's lock id
    /// matches the one currently stored on the file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations MUST perform a true compare-and-swap (e.g. <c>ConcurrentDictionary.TryUpdate</c>
    /// against a snapshot, or an ETag-conditional metadata write on a remote store). A naive
    /// "Get + Refresh" pair is racy: a concurrent <c>UnlockAndRelock</c> between the read and the
    /// write would silently extend the wrong lock.
    /// </para>
    /// <para>
    /// The previous signature took only <c>fileId</c> and left the controller layer to compare
    /// the requested vs. stored lock id between a <see cref="GetLockAsync"/> and this call. That
    /// is the textbook check-then-act race and violated the spec's atomicity requirement; the
    /// argument now flows through to the provider so the comparison and the extension are part
    /// of a single transaction.
    /// </para>
    /// <para>
    /// Spec: <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/refreshlock"/>.
    /// </para>
    /// </remarks>
    /// <param name="fileId">the fileId to refresh the lock for.</param>
    /// <param name="expectedExistingLockId">the lock id the caller observed; the refresh aborts
    /// if the stored lock id no longer matches this value (or no lock is held).</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>true if the lock was refreshed; false if the lock was missing, expired, or the
    /// existing id no longer matches.</returns>
    Task<bool> RefreshLockAsync(string fileId, string expectedExistingLockId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Atomically replace the current lock id with a new one — but only if the existing lock id
    /// matches <paramref name="expectedExistingLockId"/>. Implements the WOPI <c>UnlockAndRelock</c>
    /// operation, which the spec requires be atomic with respect to concurrent lock modifications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations MUST perform a true compare-and-swap (e.g. <c>ConcurrentDictionary.TryUpdate</c>
    /// against a snapshot, or an ETag/lease-conditional write on a remote store). A naive
    /// "Get + Refresh" sequence is not sufficient — see the spec link below for why.
    /// </para>
    /// <para>
    /// Spec: <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/unlockandrelock"/>.
    /// </para>
    /// </remarks>
    /// <param name="fileId">the fileId whose lock is being swapped.</param>
    /// <param name="newLockId">the lock id to set.</param>
    /// <param name="expectedExistingLockId">the lock id the caller observed; the swap aborts if the
    /// stored lock id no longer matches this value.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>true if the swap completed atomically; false if the lock was missing, expired, or
    /// the existing id no longer matches.</returns>
    Task<bool> TryUnlockAndRelockAsync(string fileId, string newLockId, string expectedExistingLockId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an editing file lock.
/// </summary>
public record WopiLockInfo
{
    /// <summary>
    /// Lock auto-expiration window mandated by the WOPI specification.
    /// </summary>
    /// <remarks>
    /// Per <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/lock"/>,
    /// WOPI locks MUST automatically expire after 30 minutes if not renewed by the WOPI client.
    /// This is fixed by the spec and not a tunable.
    /// </remarks>
    public const int ExpirationMinutes = 30;

    /// <summary>
    /// Maximum lock-id length permitted by the WOPI spec when the host advertises
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
    /// Returns <see langword="true"/> if this lock is expired relative to the supplied
    /// <paramref name="now"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Callers pass an explicit clock reading — typically
    /// <see cref="TimeProvider.GetUtcNow"/> on a provider-injected <see cref="TimeProvider"/> —
    /// instead of the record reading ambient time. This keeps <see cref="WopiLockInfo"/> a
    /// pure value, makes expiry deterministic in tests (inject a <c>FakeTimeProvider</c> or
    /// equivalent), and fixes the silent <see cref="DateTime"/>/<see cref="DateTimeOffset"/>
    /// conversion that the previous ambient-clock property carried.
    /// </para>
    /// <para>
    /// See <see cref="ExpirationMinutes"/> for the spec-mandated 30-minute window.
    /// </para>
    /// </remarks>
    /// <param name="now">The current time, typically <c>timeProvider.GetUtcNow()</c>.</param>
    public bool IsExpiredAt(DateTimeOffset now) => DateCreated.AddMinutes(ExpirationMinutes) < now;
}
