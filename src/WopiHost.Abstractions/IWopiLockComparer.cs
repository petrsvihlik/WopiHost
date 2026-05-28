namespace WopiHost.Abstractions;

/// <summary>
/// Compares two WOPI lock ids for equivalence. Used by both <see cref="IWopiLockProvider"/>
/// implementations (in their atomic compare-and-swap) and the WOPI controller pipeline (when
/// validating a client-supplied lock against the stored one).
/// </summary>
/// <remarks>
/// <para>
/// The default implementation, <see cref="OrdinalWopiLockComparer"/>, is byte-exact ordinal
/// equality — the safe choice and what the WOPI spec implies. Replace it in DI with
/// <see cref="JsonShapedWopiLockComparer"/> (or your own implementation) only if you have
/// concrete evidence that a specific WOPI client mutates lock ids between round-trips. The
/// canonical case is Office Online Server / Microsoft 365 for the Web's JSON-format locks,
/// which historically have had extra properties added between requests; cs3org/wopiserver and
/// SenseNet both ship tolerant fallbacks for the same reason.
/// </para>
/// <para>
/// Tolerance is not free — it can produce false equivalences (treating logically distinct
/// locks as equal), which surfaces as lost updates rather than spurious 409s. Don't relax the
/// strict default speculatively.
/// </para>
/// </remarks>
public interface IWopiLockComparer
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="storedLockId"/> and
    /// <paramref name="candidateLockId"/> represent the same logical lock for the purposes
    /// of validating a WOPI request.
    /// </summary>
    /// <param name="storedLockId">the lock id currently persisted by the host.</param>
    /// <param name="candidateLockId">the lock id supplied by the WOPI client (e.g. via
    /// <c>X-WOPI-Lock</c> or <c>X-WOPI-OldLock</c>).</param>
    bool AreEqual(string? storedLockId, string? candidateLockId);
}
