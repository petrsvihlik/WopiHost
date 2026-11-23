namespace WopiHost.Abstractions.Testing;

/// <summary>
/// Creates a fresh <see cref="IWopiLockProvider"/> for each test of
/// <see cref="LockProviderConformanceTests"/>, wired to the seams the suite drives: a
/// <see cref="TimeProvider"/> (deterministic expiry without sleeping) and an optional
/// <see cref="IWopiLockComparer"/> (tolerant comparers such as <see cref="JsonShapedWopiLockComparer"/>).
/// </summary>
/// <remarks>
/// Each call must return a provider that is logically <i>independent</i> of any earlier one:
/// fresh storage and no leftover state from previous tests. Concrete factories achieve this however
/// suits their backend (a new instance for the in-memory provider, a unique blob container for Azure,
/// a GUID-suffixed key prefix for Redis). The suite additionally uses GUID-suffixed file ids, so
/// backends that share state across instances do not interfere across tests.
/// </remarks>
public interface ILockProviderTestFactory
{
    /// <summary>Creates a fresh provider under test.</summary>
    /// <param name="timeProvider">The clock the provider uses for lock timestamps and expiry.</param>
    /// <param name="lockComparer">The lock-id comparer, or <see langword="null"/> to use the provider's default.</param>
    /// <returns>The provider under test.</returns>
    Task<IWopiLockProvider> CreateAsync(TimeProvider timeProvider, IWopiLockComparer? lockComparer = null);
}
