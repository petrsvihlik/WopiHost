using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.MemoryLockProvider.Tests;

/// <summary>
/// Unit tests for <see cref="ServiceCollectionExtensions.AddMemoryLockProvider"/>. The
/// conformance suite bypasses DI (it constructs the provider directly), so the registration
/// extension's behaviour — null-arg guards, lifetime, and the duplicate-registration check
/// — needs its own coverage.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMemoryLockProvider_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => ServiceCollectionExtensions.AddMemoryLockProvider(null!));
    }

    [Fact]
    public void AddMemoryLockProvider_RegistersIWopiLockProviderAsSingleton()
    {
        var services = NewServicesWithLogging();

        services.AddMemoryLockProvider();

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IWopiLockProvider));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddMemoryLockProvider_WhenLockProviderAlreadyRegistered_Throws()
    {
        // A host that wires two IWopiLockProviders would have the second registration silently
        // win the resolve — the guard fails fast at composition instead.
        var services = NewServicesWithLogging();
        services.AddSingleton<IWopiLockProvider>(new FakeLockProvider());

        var ex = Assert.Throws<InvalidOperationException>(services.AddMemoryLockProvider);
        Assert.Contains(nameof(IWopiLockProvider), ex.Message, StringComparison.Ordinal);
    }

    private static ServiceCollection NewServicesWithLogging()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return services;
    }

    private sealed class FakeLockProvider : IWopiLockProvider
    {
        public Task<WopiLockInfo?> GetLockAsync(string fileId, CancellationToken cancellationToken = default) => Task.FromResult<WopiLockInfo?>(null);
        public Task<WopiLockInfo?> AddLockAsync(string fileId, string lockId, CancellationToken cancellationToken = default) => Task.FromResult<WopiLockInfo?>(null);
        public Task<bool> RefreshLockAsync(string fileId, string expectedExistingLockId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> TryUnlockAndRelockAsync(string fileId, string newLockId, string expectedExistingLockId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> RemoveLockAsync(string fileId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
