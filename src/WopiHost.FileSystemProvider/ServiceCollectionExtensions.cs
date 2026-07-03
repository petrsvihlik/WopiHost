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
        // TryAdd: a host registering its own provider first wins, and a repeat call no-ops
        // instead of double-registering. Singleton is safe — the provider routes all mutable
        // state through the thread-safe InMemoryFileIds; its change watcher is disposed with
        // the container (the provider is IDisposable).
        services.TryAddSingleton<WopiFileSystemProvider>();
        services.TryAddSingleton<IWopiStorageProvider>(sp => sp.GetRequiredService<WopiFileSystemProvider>());
        services.TryAddSingleton<IWopiWritableStorageProvider>(sp => sp.GetRequiredService<WopiFileSystemProvider>());

        return services;
    }
}
