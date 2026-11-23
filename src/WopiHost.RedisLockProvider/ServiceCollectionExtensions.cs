using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WopiHost.Abstractions;

namespace WopiHost.RedisLockProvider;

/// <summary>
/// Provides extension methods that register <see cref="WopiRedisLockProvider"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="WopiRedisLockProvider"/> as the <see cref="IWopiLockProvider"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads <see cref="WopiRedisLockProviderOptions"/> from the <c>Wopi:LockProvider</c>
    /// section. Connection-multiplexer resolution order:
    /// </para>
    /// <list type="number">
    /// <item><description>An <see cref="IConnectionMultiplexer"/> already registered in DI (e.g. by Aspire's
    /// <c>builder.AddRedisClient("wopi-locks")</c> or any app-level Redis-ownership wiring), so a
    /// process keeps a single multiplexer even when several subsystems share it.</description></item>
    /// <item><description>Otherwise, a <see cref="ConnectionMultiplexer"/> built from
    /// <see cref="WopiRedisLockProviderOptions.ConnectionString"/> when the provider is first resolved.
    /// Resolution throws <see cref="InvalidOperationException"/> if the connection string is empty.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="services">The service collection to add the provider to.</param>
    /// <param name="configuration">The configuration root that contains the <c>Wopi:LockProvider</c> section.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">An <see cref="IWopiLockProvider"/> is already registered in <paramref name="services"/>.</exception>
    public static IServiceCollection AddRedisLockProvider(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Only one IWopiLockProvider can be in DI at a time — a host that wires two would have
        // the second registration silently win the resolve. Fail fast at composition instead.
        ThrowIfLockProviderAlreadyRegistered(services, nameof(AddRedisLockProvider));

        services
            .AddOptions<WopiRedisLockProviderOptions>()
            .Bind(configuration.GetSection(WopiRedisLockProviderOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<WopiRedisLockProvider>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<WopiRedisLockProviderOptions>>().Value;
            var multiplexer = sp.GetService<IConnectionMultiplexer>()
                ?? BuildOwnedMultiplexer(opts);
            return new WopiRedisLockProvider(
                multiplexer,
                sp.GetRequiredService<ILogger<WopiRedisLockProvider>>(),
                opts.KeyPrefix,
                sp.GetService<TimeProvider>(),
                sp.GetService<IWopiLockComparer>());
        });
        services.AddSingleton<IWopiLockProvider>(sp => sp.GetRequiredService<WopiRedisLockProvider>());

        return services;
    }

    private static void ThrowIfLockProviderAlreadyRegistered(IServiceCollection services, string thisExtensionName)
    {
        if (services.Any(d => d.ServiceType == typeof(IWopiLockProvider)))
        {
            throw new InvalidOperationException(
                $"An {nameof(IWopiLockProvider)} is already registered. {thisExtensionName} cannot " +
                "coexist with another lock-provider registration — pick one (Memory / Azure / Redis).");
        }
    }

    private static ConnectionMultiplexer BuildOwnedMultiplexer(WopiRedisLockProviderOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.ConnectionString))
        {
            throw new InvalidOperationException(
                $"{WopiRedisLockProviderOptions.SectionName}:{nameof(WopiRedisLockProviderOptions.ConnectionString)} is required " +
                "when no IConnectionMultiplexer is registered in DI.");
        }
        return ConnectionMultiplexer.Connect(opts.ConnectionString);
    }
}
