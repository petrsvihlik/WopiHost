using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Provides extension methods that register <see cref="WopiAzureStorageProvider"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="WopiAzureStorageProvider"/> as both <see cref="IWopiStorageProvider"/> and
    /// <see cref="IWopiWritableStorageProvider"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Binds <see cref="WopiAzureStorageProviderOptions"/> from the <c>Wopi:StorageProvider</c>
    /// configuration section and validates on start that
    /// <see cref="WopiAzureStorageProviderOptions.ContainerName"/> and either
    /// <see cref="WopiAzureStorageProviderOptions.ConnectionString"/> or
    /// <see cref="WopiAzureStorageProviderOptions.ServiceUri"/> are set. A connection string wins
    /// (typical for Azurite/dev); otherwise the service URI is paired with the
    /// <see cref="TokenCredential"/> registered in DI, falling back to <see cref="DefaultAzureCredential"/>.
    /// </para>
    /// <para>
    /// All registrations use <c>TryAdd</c>, so a provider or <see cref="BlobContainerClient"/> the host
    /// registered first wins and a repeat call is a no-op.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to add the provider to.</param>
    /// <param name="configuration">The configuration root that contains the <c>Wopi:StorageProvider</c> section.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddAzureStorageProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<WopiAzureStorageProviderOptions>()
            .Bind(configuration.GetSection(WopiAzureStorageProviderOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ContainerName), "Wopi:StorageProvider:ContainerName is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString) || o.ServiceUri is not null,
                "Either Wopi:StorageProvider:ConnectionString or Wopi:StorageProvider:ServiceUri must be set.")
            .ValidateOnStart();

        services.TryAddSingleton<BlobIdMap>();

        services.TryAddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<WopiAzureStorageProviderOptions>>().Value;
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
            return serviceClient.GetBlobContainerClient(opts.ContainerName);
        });

        // TryAdd: a host registering its own provider (or BlobContainerClient) first wins, and a
        // repeat call no-ops instead of double-registering.
        services.TryAddSingleton<WopiAzureStorageProvider>();
        services.TryAddSingleton<IWopiStorageProvider>(sp => sp.GetRequiredService<WopiAzureStorageProvider>());
        services.TryAddSingleton<IWopiWritableStorageProvider>(sp => sp.GetRequiredService<WopiAzureStorageProvider>());

        return services;
    }
}
