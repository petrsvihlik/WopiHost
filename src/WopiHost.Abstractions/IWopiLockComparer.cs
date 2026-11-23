namespace WopiHost.Abstractions;

/// <summary>
/// Compares two WOPI lock ids for equivalence. Used both by <see cref="IWopiLockProvider"/>
/// implementations (in their atomic compare-and-swap) and by the WOPI endpoints (when
/// validating a client-supplied lock against the stored one).
/// </summary>
/// <remarks>
/// <para>
/// The default implementation, <see cref="OrdinalWopiLockComparer"/>, is byte-exact ordinal
/// equality — the safe choice and what the WOPI spec implies. Replace it in DI with
/// <see cref="JsonShapedWopiLockComparer"/> (or a custom implementation) only on concrete
/// evidence that a specific WOPI client mutates lock ids between round-trips. The
/// canonical case is Office Online Server / Microsoft 365 for the Web's JSON-format locks,
/// which historically have had extra properties added between requests; cs3org/wopiserver and
/// SenseNet both ship tolerant fallbacks for the same reason.
/// </para>
/// <para>
/// Tolerance can produce false equivalences (treating logically distinct locks as equal),
/// which surface as lost updates rather than spurious <c>409</c> responses, so the strict
/// default should not be relaxed speculatively.
/// </para>
/// </remarks>
public interface IWopiLockComparer
{
    /// <summary>
    /// Determines whether <paramref name="storedLockId"/> and <paramref name="candidateLockId"/>
    /// represent the same logical lock for the purposes of validating a WOPI request.
    /// </summary>
    /// <param name="storedLockId">The lock id currently persisted by the host.</param>
    /// <param name="candidateLockId">The lock id supplied by the WOPI client (for example via
    /// <c>X-WOPI-Lock</c> or <c>X-WOPI-OldLock</c>).</param>
    /// <returns><see langword="true"/> if the two ids represent the same lock; otherwise,
    /// <see langword="false"/>.</returns>
    bool AreEqual(string? storedLockId, string? candidateLockId);
}
