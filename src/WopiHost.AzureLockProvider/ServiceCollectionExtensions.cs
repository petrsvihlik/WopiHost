using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;

namespace WopiHost.AzureLockProvider;

/// <summary>
/// Provides extension methods that register <see cref="WopiAzureLockProvider"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="WopiAzureLockProvider"/> as the singleton <see cref="IWopiLockProvider"/>.
    /// </summary>
    /// <remarks>
    /// Binds <see cref="WopiAzureLockProviderOptions"/> from the <c>Wopi:LockProvider</c> section and
    /// validates on start that <see cref="WopiAzureLockProviderOptions.ContainerName"/> and either
    /// <see cref="WopiAzureLockProviderOptions.ConnectionString"/> or
    /// <see cref="WopiAzureLockProviderOptions.ServiceUri"/> are set. Without a connection string, the
    /// <see cref="TokenCredential"/> registered in DI is used, falling back to
    /// <see cref="DefaultAzureCredential"/>. The lock container is created on first use.
    /// </remarks>
    /// <param name="services">The service collection to add the provider to.</param>
    /// <param name="configuration">The configuration root that contains the <c>Wopi:LockProvider</c> section.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">An <see cref="IWopiLockProvider"/> is already registered in <paramref name="services"/>.</exception>
    public static IServiceCollection AddAzureLockProvider(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Only one IWopiLockProvider can be in DI at a time — a host that wires two would have
        // the second registration silently win the resolve. Fail fast at composition instead.
        ThrowIfLockProviderAlreadyRegistered(services, nameof(AddAzureLockProvider));

        services
            .AddOptions<WopiAzureLockProviderOptions>()
            .Bind(configuration.GetSection(WopiAzureLockProviderOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ContainerName), "Wopi:LockProvider:ContainerName is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString) || o.ServiceUri is not null,
                "Either Wopi:LockProvider:ConnectionString or Wopi:LockProvider:ServiceUri must be set.")
            .ValidateOnStart();

        services.AddSingleton<WopiAzureLockProvider>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<WopiAzureLockProviderOptions>>().Value;
            BlobServiceClient serviceClient;
            if (!string.IsNullOrWhiteSpace(opts.ConnectionString))
            {
                serviceClient = new BlobServiceClient(opts.ConnectionString);
            }
            else
            {
                var credential = sp.GetService<TokenCredential>() ?? new DefaultAzureCredential();
                serviceClient = new BlobServiceClient(opts.ServiceUri!, credential);
            }
            var container = serviceClient.GetBlobContainerClient(opts.ContainerName);
            return new WopiAzureLockProvider(
                container,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WopiAzureLockProvider>>(),
                sp.GetService<TimeProvider>(),
                sp.GetService<IWopiLockComparer>());
        });
        services.AddSingleton<IWopiLockProvider>(sp => sp.GetRequiredService<WopiAzureLockProvider>());

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
}
