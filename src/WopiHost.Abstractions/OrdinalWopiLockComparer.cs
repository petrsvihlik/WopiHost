namespace WopiHost.Abstractions;

/// <summary>
/// Provides the default <see cref="IWopiLockComparer"/>: exact ordinal string equality, per the
/// WOPI spec's treatment of lock ids as opaque identifiers.
/// </summary>
public sealed class OrdinalWopiLockComparer : IWopiLockComparer
{
    /// <summary>
    /// Gets the shared instance, suitable as a default in providers and DI.
    /// </summary>
    public static OrdinalWopiLockComparer Instance { get; } = new();

    /// <inheritdoc />
    public bool AreEqual(string? storedLockId, string? candidateLockId)
        => string.Equals(storedLockId, candidateLockId, StringComparison.Ordinal);
}
