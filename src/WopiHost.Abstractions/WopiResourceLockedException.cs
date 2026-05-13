namespace WopiHost.Abstractions;

/// <summary>
/// Thrown when a write operation is attempted against a resource that is currently locked
/// according to <see cref="IWopiLockProvider"/>. Used by lock-aware storage decorators
/// (see <c>WopiLockAwareWritableStorageProvider</c> in <c>WopiHost.Core</c>) as a defense-in-depth
/// signal: the WOPI controllers already short-circuit on locks before reaching the storage
/// layer, so this exception only surfaces when a non-WOPI code path or future controller
/// regression bypasses that check.
/// </summary>
/// <param name="resourceIdentifier">Identifier of the resource the caller tried to mutate.</param>
/// <param name="lockId">The lock id currently held on the resource.</param>
public class WopiResourceLockedException(string resourceIdentifier, string lockId)
    : InvalidOperationException($"Resource '{resourceIdentifier}' is locked (lock id '{lockId}'); refusing write through lock-aware storage decorator.")
{
    /// <summary>
    /// Identifier of the resource the caller tried to mutate.
    /// </summary>
    public string ResourceIdentifier { get; } = resourceIdentifier;

    /// <summary>
    /// The lock id currently held on the resource.
    /// </summary>
    public string LockId { get; } = lockId;
}
