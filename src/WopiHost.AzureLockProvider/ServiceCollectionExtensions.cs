using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;

namespace WopiHost.AzureLockProvider;

/// <summary>
/// DI extensions to register <see cref="WopiAzureLockProvider"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="WopiAzureLockProvider"/> as the <see cref="IWopiLockProvider"/> in the
    /// container. Reads <see cref="WopiAzureLockProviderOptions"/> from the <c>Wopi:LockProvider</c>
    /// configuration section. The lock container is created on first use.
    /// </summary>
    public static IServiceCollection AddAzureLockProvider(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<WopiAzureLockProviderOptions>()
            .Bind(configuration.GetSection(WopiConfigurationSections.LOCK_OPTIONS))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ContainerName), "Wopi:LockProvider:ContainerName is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString) || !string.IsNullOrWhiteSpace(o.ServiceUri),
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
                serviceClient = new BlobServiceClient(new Uri(opts.ServiceUri!), credential);
            }
            var container = serviceClient.GetBlobContainerClient(opts.ContainerName);
            return new WopiAzureLockProvider(container, sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WopiAzureLockProvider>>());
        });
        services.AddSingleton<IWopiLockProvider>(sp => sp.GetRequiredService<WopiAzureLockProvider>());

        return services;
    }
}
