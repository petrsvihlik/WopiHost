using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// DI extensions to register <see cref="WopiFileSystemProvider"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="WopiFileSystemProvider"/> as both <see cref="IWopiStorageProvider"/>
    /// and <see cref="IWopiWritableStorageProvider"/>, together with the singleton
    /// <see cref="InMemoryFileIds"/> map the provider uses for path↔id round-tripping.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="WopiFileSystemProviderOptions"/> from the <c>Wopi:StorageProvider</c>
    /// configuration section. <c>RootPath</c> is required.
    /// </remarks>
    public static IServiceCollection AddFileSystemStorageProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<WopiFileSystemProviderOptions>()
            .Bind(configuration.GetSection(WopiFileSystemProviderOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.RootPath), "Wopi:StorageProvider:RootPath is required.")
            .ValidateOnStart();

        services.TryAddSingleton<InMemoryFileIds>();
        // The provider is a stateless wrapper: after construction it holds only immutable fields,
        // depends solely on singletons (InMemoryFileIds, IHostEnvironment, IConfiguration, ILogger)
        // — so no scoped captive dependency — and routes all mutable state through the thread-safe
        // InMemoryFileIds.
        services.AddSingleton<WopiFileSystemProvider>();
        services.AddSingleton<IWopiStorageProvider>(sp => sp.GetRequiredService<WopiFileSystemProvider>());
        services.AddSingleton<IWopiWritableStorageProvider>(sp => sp.GetRequiredService<WopiFileSystemProvider>());

        return services;
    }
}
