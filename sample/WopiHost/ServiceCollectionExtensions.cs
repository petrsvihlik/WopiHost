using System.Runtime.Loader;
using Microsoft.Extensions.Configuration;
using WopiHost.Abstractions;
using WopiHost.AzureLockProvider;
using WopiHost.AzureStorageProvider;
using WopiHost.FileSystemProvider;

namespace WopiHost;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the storage provider identified by <paramref name="storageProviderAssemblyName"/>.
    /// </summary>
    /// <remarks>
    /// Recognises specific assembly names so each provider's required DI dependencies are wired up
    /// correctly (Azure clients, options binding, ID maps). Falls back to a Scrutor scan for
    /// arbitrary third-party assemblies whose providers can be constructed from already-registered
    /// services.
    /// </remarks>
    public static void AddStorageProvider(
        this IServiceCollection services,
        IConfiguration configuration,
        string storageProviderAssemblyName)
    {
        switch (storageProviderAssemblyName)
        {
            case "WopiHost.FileSystemProvider":
                services.AddSingleton<InMemoryFileIds>();
                services.AddScoped<IWopiStorageProvider, WopiFileSystemProvider>();
                services.AddScoped<IWopiWritableStorageProvider, WopiFileSystemProvider>();
                return;

            case "WopiHost.AzureStorageProvider":
                services.AddAzureStorageProvider(configuration);
                return;

            default:
                ScanAssemblyAndRegister<IWopiStorageProvider>(services, storageProviderAssemblyName);
                return;
        }
    }

    /// <summary>
    /// Registers the lock provider identified by <paramref name="lockProviderAssemblyName"/>.
    /// </summary>
    public static void AddLockProvider(
        this IServiceCollection services,
        IConfiguration configuration,
        string lockProviderAssemblyName)
    {
        switch (lockProviderAssemblyName)
        {
            case "WopiHost.MemoryLockProvider":
                services.AddSingleton<IWopiLockProvider, MemoryLockProvider.MemoryLockProvider>();
                return;

            case "WopiHost.AzureLockProvider":
                services.AddAzureLockProvider(configuration);
                return;

            default:
                ScanAssemblyAndRegister<IWopiLockProvider>(services, lockProviderAssemblyName);
                return;
        }
    }

    public static void AddCobalt(this IServiceCollection services)
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "WopiHost.Cobalt.dll");
        if (!File.Exists(assemblyPath))
        {
            throw new InvalidProgramException($"Cobalt Assembly {assemblyPath} not found.");
        }
        var cobaltAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        services
            .Scan(scan => scan.FromAssemblies(cobaltAssembly)
            .AddClasses(classes => classes
                .AssignableTo<ICobaltProcessor>())
            .AsImplementedInterfaces());
    }

    private static void ScanAssemblyAndRegister<TInterface>(IServiceCollection services, string assemblyName)
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll");
        if (!File.Exists(assemblyPath))
        {
            throw new InvalidProgramException($"Provider assembly {assemblyPath} not found.");
        }
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        services
            .Scan(scan => scan.FromAssemblies(assembly)
            .AddClasses(classes => classes
                .AssignableTo<TInterface>())
            .AsImplementedInterfaces());
    }
}
