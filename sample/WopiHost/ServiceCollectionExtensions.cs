using System.Runtime.Loader;
using WopiHost.Abstractions;
using WopiHost.AzureLockProvider;
using WopiHost.AzureStorageProvider;
using WopiHost.FileSystemProvider;
using WopiHost.MemoryLockProvider;
using WopiHost.RedisLockProvider;

namespace WopiHost;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Sample-local discriminators for which storage / lock provider to wire up. Lives in the
    /// sample (not in WopiHost.Core's public options surface) because choosing between bundled
    /// providers is composition-root concern, not a contract the library should expose.
    /// </summary>
    /// <remarks>
    /// Real hosts reference one provider package and call its typed extension directly
    /// (e.g. <c>services.AddFileSystemStorageProvider(cfg)</c>). The enum-based switch exists here
    /// so the AppHost flag flow and Aspire orchestration can flip providers at runtime without
    /// recompiling the sample.
    /// </remarks>
    public enum SampleStorageProvider
    {
        FileSystem,
        Azure,
    }

    /// <inheritdoc cref="SampleStorageProvider"/>
    public enum SampleLockProvider
    {
        Memory,
        Azure,
        Redis,
    }

    /// <summary>
    /// Sample-only helper that dispatches to the chosen provider's typed registration extension.
    /// Each branch is a one-liner so adding a new provider means adding an enum value and a
    /// case — no reflection, no string-name probing, no fallback path.
    /// </summary>
    public static void AddSampleStorageProvider(
        this IServiceCollection services,
        IConfiguration configuration,
        SampleStorageProvider provider)
    {
        switch (provider)
        {
            case SampleStorageProvider.FileSystem:
                services.AddFileSystemStorageProvider(configuration);
                return;
            case SampleStorageProvider.Azure:
                services.AddAzureStorageProvider(configuration);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown sample storage provider.");
        }
    }

    /// <inheritdoc cref="AddSampleStorageProvider"/>
    public static void AddSampleLockProvider(
        this IServiceCollection services,
        IConfiguration configuration,
        SampleLockProvider provider)
    {
        switch (provider)
        {
            case SampleLockProvider.Memory:
                services.AddMemoryLockProvider();
                return;
            case SampleLockProvider.Azure:
                services.AddAzureLockProvider(configuration);
                return;
            case SampleLockProvider.Redis:
                services.AddRedisLockProvider(configuration);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown sample lock provider.");
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
}
