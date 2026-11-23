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
    /// Specifies which bundled storage or lock provider the sample registers.
    /// </summary>
    /// <remarks>
    /// Choosing between bundled providers is a composition-root concern, so this discriminator
    /// lives in the sample rather than in WopiHost.Core's public options. Real hosts reference one
    /// provider package and call its typed extension directly (e.g. <c>services.AddFileSystemStorageProvider(cfg)</c>). The enum-based switch exists here
    /// so the AppHost flag flow and Aspire orchestration can flip providers at runtime without
    /// recompiling the sample.
    /// </remarks>
    public enum SampleStorageProvider
    {
        /// <summary>The local file-system provider.</summary>
        FileSystem,

        /// <summary>The Azure Blob Storage provider.</summary>
        Azure,
    }

    /// <inheritdoc cref="SampleStorageProvider"/>
    public enum SampleLockProvider
    {
        /// <summary>The in-process memory lock provider.</summary>
        Memory,

        /// <summary>The Azure blob-lease lock provider.</summary>
        Azure,

        /// <summary>The Redis lock provider.</summary>
        Redis,
    }

    /// <summary>
    /// Registers the chosen provider by dispatching to its typed registration extension.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">The configuration the provider binds its options from.</param>
    /// <param name="provider">The provider to register.</param>
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
        var assemblyPath = Path.Join(AppContext.BaseDirectory, "WopiHost.Cobalt.dll");
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
