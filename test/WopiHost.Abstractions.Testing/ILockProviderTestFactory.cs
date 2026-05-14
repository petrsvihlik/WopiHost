namespace WopiHost.Abstractions.Testing;

/// <summary>
/// Factory the conformance test suite uses to obtain a fresh <see cref="IWopiLockProvider"/>
/// instance for each test, parameterized by the seams the suite needs to drive — a
/// <see cref="TimeProvider"/> (to make expiry deterministic without sleeping) and an optional
/// <see cref="IWopiLockComparer"/> (to swap in tolerant comparers like
/// <see cref="JsonShapedWopiLockComparer"/>).
/// </summary>
/// <remarks>
/// Each call must return a provider that is logically <em>independent</em> of any earlier one
/// — fresh storage, no leftover state from previous tests. Concrete providers do this however
/// makes sense for their backend (a unique blob container per call for Azure; a fresh
/// <c>ConcurrentDictionary</c> for an Aspire'd Redis db; etc.). The in-tree
/// <c>MemoryLockProvider</c> uses static state so true isolation isn't possible there; the
/// conformance suite avoids cross-test interference by using GUID-suffixed file ids.
/// </remarks>
public interface ILockProviderTestFactory
{
    /// <summary>Create a fresh provider under test.</summary>
    /// <param name="timeProvider">Clock the provider uses for lock timestamps and expiry.</param>
    /// <param name="lockComparer">Optional comparer; defaults to the provider's own default when null.</param>
    Task<IWopiLockProvider> CreateAsync(TimeProvider timeProvider, IWopiLockComparer? lockComparer = null);
}
