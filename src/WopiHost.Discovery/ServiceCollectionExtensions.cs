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
    /// <remarks>
    /// Registers <see cref="HttpDiscoveryFileProvider"/> as a typed HTTP client and <see cref="WopiDiscoverer"/>
    /// as a singleton <see cref="IDiscoverer"/>. <see cref="DiscoveryOptions.RefreshInterval"/> is validated
    /// to be positive at startup.
    /// </remarks>
    /// <typeparam name="TOptions">The options type whose <see cref="IDiscoveryOptions.ClientUrl"/> becomes the base address of the discovery HTTP client.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureDiscoveryOptions">A delegate that configures the <see cref="DiscoveryOptions"/>.</param>
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