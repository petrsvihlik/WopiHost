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
/// DI extensions to register <see cref="WopiAzureStorageProvider"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="WopiAzureStorageProvider"/> as both <see cref="IWopiStorageProvider"/> and
    /// <see cref="IWopiWritableStorageProvider"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads <see cref="WopiAzureStorageProviderOptions"/> from the <c>Wopi:StorageProvider</c>
    /// configuration section. If <c>ConnectionString</c> is set, that wins (typical for Azurite/dev).
    /// Otherwise <c>ServiceUri</c> is required and is paired with whatever <see cref="TokenCredential"/>
    /// the caller has registered in DI; if none is registered, <see cref="DefaultAzureCredential"/> is
    /// used.
    /// </para>
    /// <para>
    /// The host can call this directly for explicit registration, or rely on
    /// <c>services.AddStorageProvider("WopiHost.AzureStorageProvider")</c> to discover and register it
    /// via the assembly-name convention used by the rest of WopiHost.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAzureStorageProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<WopiAzureStorageProviderOptions>()
            .Bind(configuration.GetSection(WopiConfigurationSections.STORAGE_OPTIONS))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ContainerName), "Wopi:StorageProvider:ContainerName is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString) || o.ServiceUri is not null,
                "Either Wopi:StorageProvider:ConnectionString or Wopi:StorageProvider:ServiceUri must be set.")
            .ValidateOnStart();

        services.TryAddSingleton<BlobIdMap>();

        services.AddSingleton(sp =>
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

        services.AddSingleton<WopiAzureStorageProvider>();
        services.AddSingleton<IWopiStorageProvider>(sp => sp.GetRequiredService<WopiAzureStorageProvider>());
        services.AddSingleton<IWopiWritableStorageProvider>(sp => sp.GetRequiredService<WopiAzureStorageProvider>());

        return services;
    }
}
