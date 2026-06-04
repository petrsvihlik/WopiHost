using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider.Tests;

public class ServiceCollectionExtensionsTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("FsProviderDiTest_");

    public void Dispose()
    {
        _tempDir.Refresh();
        if (_tempDir.Exists) _tempDir.Delete(recursive: true);
        GC.SuppressFinalize(this);
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private IServiceCollection BuildServices(out IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment { ContentRootPath = _tempDir.FullName });
        config = BuildConfig(new()
        {
            ["Wopi:StorageProvider:RootPath"] = _tempDir.FullName,
        });
        // The provider constructor resolves IConfiguration from DI (separate from the options
        // binding wired up inside AddFileSystemStorageProvider), so it must be registered too.
        services.AddSingleton(config);
        return services;
    }

    [Fact]
    public void AddFileSystemStorageProvider_RegistersProviderAndInterfacesAsSingleton()
    {
        // The FS provider must be a singleton, matching AddAzureStorageProvider.
        // Guards against scoped/singleton drift between the two providers.
        var services = BuildServices(out var config);

        services.AddFileSystemStorageProvider(config);

        foreach (var serviceType in new[]
                 {
                     typeof(WopiFileSystemProvider),
                     typeof(IWopiStorageProvider),
                     typeof(IWopiWritableStorageProvider),
                 })
        {
            var descriptor = Assert.Single(services, d => d.ServiceType == serviceType);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }
    }

    [Fact]
    public void AddFileSystemStorageProvider_BothInterfaces_ResolveSameInstanceAcrossScopes()
    {
        var services = BuildServices(out var config);
        services.AddFileSystemStorageProvider(config);

        using var sp = services.BuildServiceProvider();
        using var scope1 = sp.CreateScope();
        using var scope2 = sp.CreateScope();

        var storage = scope1.ServiceProvider.GetRequiredService<IWopiStorageProvider>();
        var writable = scope1.ServiceProvider.GetRequiredService<IWopiWritableStorageProvider>();
        var storageOtherScope = scope2.ServiceProvider.GetRequiredService<IWopiStorageProvider>();

        Assert.IsType<WopiFileSystemProvider>(storage);
        // Both interfaces resolve to the one provider instance...
        Assert.Same(storage, writable);
        // ...and that instance is shared across scopes (singleton semantics, not scoped).
        Assert.Same(storage, storageOtherScope);
    }

    [Fact]
    public void AddFileSystemStorageProvider_MissingRootPath_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var config = BuildConfig([]);

        services.AddFileSystemStorageProvider(config);

        using var sp = services.BuildServiceProvider();
        var ex = Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(
            () => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WopiFileSystemProviderOptions>>().Value);
        Assert.Contains("RootPath", ex.Message);
    }

    [Fact]
    public void AddFileSystemStorageProvider_NullArgs_Throw()
    {
        var services = new ServiceCollection();
        var config = BuildConfig([]);

        Assert.Throws<ArgumentNullException>(
            () => ServiceCollectionExtensions.AddFileSystemStorageProvider(null!, config));
        Assert.Throws<ArgumentNullException>(
            () => services.AddFileSystemStorageProvider(null!));
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
