namespace WopiHost.Abstractions;

/// <summary>
/// Default <see cref="IWopiLockComparer"/> — byte-exact ordinal equality. Matches the WOPI
/// spec's implicit contract that lock ids are opaque strings the host treats as identifiers.
/// </summary>
public sealed class OrdinalWopiLockComparer : IWopiLockComparer
{
    /// <summary>
    /// Shared, allocation-free singleton suitable for use as a default in providers and DI.
    /// </summary>
    public static OrdinalWopiLockComparer Instance { get; } = new();

    /// <inheritdoc />
    public bool AreEqual(string? storedLockId, string? candidateLockId)
        => string.Equals(storedLockId, candidateLockId, StringComparison.Ordinal);
}
