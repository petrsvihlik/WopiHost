using FakeItEasy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.RedisLockProvider.Tests;

/// <summary>
/// Unit tests for <see cref="ServiceCollectionExtensions.AddRedisLockProvider"/>. The
/// conformance suite bypasses DI (the test factory constructs the provider directly), so the
/// registration code path needs its own coverage. These tests don't need a real Redis instance
/// — they verify the registration shape, options binding, and multiplexer-resolution priority.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddRedisLockProvider_NullServices_Throws()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<ArgumentNullException>(
            () => ServiceCollectionExtensions.AddRedisLockProvider(null!, config));
    }

    [Fact]
    public void AddRedisLockProvider_NullConfiguration_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(
            () => services.AddRedisLockProvider(null!));
    }

    [Fact]
    public void AddRedisLockProvider_RegistersIWopiLockProviderAsSingleton()
    {
        var services = NewServicesWith(new()
        {
            ["Wopi:LockProvider:ConnectionString"] = "localhost:6379",
        });

        services.AddRedisLockProvider(BuildConfig(("Wopi:LockProvider:ConnectionString", "localhost:6379")));

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IWopiLockProvider));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddRedisLockProvider_PrefersInjectedMultiplexer_OverConnectionString()
    {
        // Resolution priority: an IConnectionMultiplexer already registered in DI wins over the
        // ConnectionString option. This is how Aspire's builder.AddRedisClient("wopi-locks")
        // plays with the provider — the multiplexer comes from the host, the provider just
        // borrows it.
        var fakeMultiplexer = A.Fake<IConnectionMultiplexer>();
        var services = NewServicesWith();
        services.AddSingleton(fakeMultiplexer);

        services.AddRedisLockProvider(BuildConfig(("Wopi:LockProvider:KeyPrefix", "test:")));

        using var sp = services.BuildServiceProvider();
        // Instantiating the provider must NOT throw "ConnectionString is required" because the
        // DI-resolved multiplexer takes precedence.
        var provider = sp.GetRequiredService<IWopiLockProvider>();
        Assert.NotNull(provider);
    }

    [Fact]
    public void AddRedisLockProvider_WhenLockProviderAlreadyRegistered_Throws()
    {
        // A host that wires two IWopiLockProviders would have the second registration silently
        // win the resolve — the guard fails fast at composition instead.
        var services = NewServicesWith();
        services.AddSingleton<IWopiLockProvider>(A.Fake<IWopiLockProvider>());

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddRedisLockProvider(BuildConfig(("Wopi:LockProvider:ConnectionString", "localhost:6379"))));
        Assert.Contains(nameof(IWopiLockProvider), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddRedisLockProvider_NoMultiplexerAndNoConnectionString_ThrowsOnResolve()
    {
        // No multiplexer in DI AND no ConnectionString option → constructing the provider has to
        // fail with a useful message, not a generic NullReferenceException. The validation lives
        // in BuildOwnedMultiplexer; ValidateOnStart() catches it at resolution time.
        var services = NewServicesWith();
        services.AddRedisLockProvider(BuildConfig());

        using var sp = services.BuildServiceProvider();
        var ex = Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IWopiLockProvider>());
        Assert.Contains("ConnectionString", ex.Message, StringComparison.Ordinal);
    }

    private static IConfiguration BuildConfig(params (string Key, string Value)[] pairs)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (k, v) in pairs)
        {
            dict[k] = v;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static ServiceCollection NewServicesWith(Dictionary<string, string?>? _ = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>));
        return services;
    }
}
