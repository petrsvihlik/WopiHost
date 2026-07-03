using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WopiHost.Discovery;

/// <summary>
/// Extension methods for adding WOPI discovery services to the service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds WOPI discovery services to the service collection.
    /// </summary>
    /// <typeparam name="TOptions">The type of options to use for configuration that implements IDiscoveryOptions.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureDiscoveryOptions">Action to configure the discovery options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWopiDiscovery<TOptions>(
        this IServiceCollection services,
        Action<DiscoveryOptions> configureDiscoveryOptions)
        where TOptions : class, IDiscoveryOptions
    {
        services.AddOptions<DiscoveryOptions>()
            .Configure(configureDiscoveryOptions)
            .Validate(o => o.RefreshInterval > TimeSpan.Zero, "RefreshInterval must be positive.")
            .ValidateOnStart();

        services.AddHttpClient<IDiscoveryFileProvider, HttpDiscoveryFileProvider>((sp, client) =>
        {
            var wopiOptions = sp.GetRequiredService<IOptions<TOptions>>();
            client.BaseAddress = wopiOptions.Value.ClientUrl;
        });

        services.AddSingleton<IDiscoverer, WopiDiscoverer>();

        return services;
    }
} 