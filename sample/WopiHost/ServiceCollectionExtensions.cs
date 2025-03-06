using System.Runtime.Loader;
using WopiHost.Abstractions;

namespace WopiHost;

public static class ServiceCollectionExtensions
{
    public static void AddStorageProvider(this IServiceCollection services, string storageProviderAssemblyName)
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, $"{storageProviderAssemblyName}.dll");
        if (!File.Exists(assemblyPath))
        {
            throw new InvalidProgramException($"StorageProvider's Assembly {assemblyPath} not found.");
        }
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        services
            .Scan(scan => scan.FromAssemblies(assembly)
            .AddClasses(classes => classes
                .AssignableToAny(typeof(IWopiSecurityHandler), typeof(IWopiStorageProvider)))
            .AsImplementedInterfaces());
    }

    public static void AddLockProvider(this IServiceCollection services, string lockProviderAssemblyName)
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, $"{lockProviderAssemblyName}.dll");
        if (!File.Exists(assemblyPath))
        {
            throw new InvalidProgramException($"LockProvider's Assembly {assemblyPath} not found.");
        }
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        services
            .Scan(scan => scan.FromAssemblies(assembly)
            .AddClasses(classes => classes
                .AssignableTo<IWopiLockProvider>())
            .AsImplementedInterfaces());
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
}
