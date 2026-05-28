using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions;
using WopiHost.Abstractions.Testing;

namespace WopiHost.MemoryLockProvider.Tests;

/// <summary>
/// Runs the shared <see cref="LockProviderConformanceTests"/> against <see cref="MemoryLockProvider"/>.
/// </summary>
/// <remarks>
/// xUnit discovers the inherited [Fact] tests automatically. Provider-specific tests
/// (reflection-driven static-state probes, JSON-shaped comparer behavior under direct
/// construction, etc.) stay in <c>MemoryLockProviderTests</c>.
/// </remarks>
public sealed class MemoryLockProviderConformanceTests : LockProviderConformanceTests
{
    /// <inheritdoc />
    protected override ILockProviderTestFactory Factory { get; } = new MemoryFactory();

    private sealed class MemoryFactory : ILockProviderTestFactory
    {
        public Task<IWopiLockProvider> CreateAsync(TimeProvider timeProvider, IWopiLockComparer? lockComparer = null)
            => Task.FromResult<IWopiLockProvider>(
                new MemoryLockProvider(NullLogger<MemoryLockProvider>.Instance, timeProvider, lockComparer));
    }
}
