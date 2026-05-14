using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WopiHost.Abstractions;

namespace WopiHost.RedisLockProvider;

/// <summary>
/// DI extensions to register <see cref="WopiRedisLockProvider"/>.
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
    /// <item>An <see cref="IConnectionMultiplexer"/> already registered in DI (e.g. by Aspire's
    /// <c>builder.AddRedisClient("wopi-locks")</c> or any app-level Redis-ownership wiring) —
    /// this wins so users keep a single multiplexer per process even when multiple subsystems
    /// share it.</item>
    /// <item>Otherwise, build a <see cref="ConnectionMultiplexer"/> from
    /// <see cref="WopiRedisLockProviderOptions.ConnectionString"/>.</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddRedisLockProvider(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

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
