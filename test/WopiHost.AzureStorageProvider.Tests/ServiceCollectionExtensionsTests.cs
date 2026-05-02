using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.AzureStorageProvider.Tests;

public class ServiceCollectionExtensionsTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static void AddNullLogging(IServiceCollection services)
    {
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
    }

    [Fact]
    public void AddAzureStorageProvider_WithConnectionString_RegistersBothInterfaces()
    {
        var services = new ServiceCollection();
        AddNullLogging(services);
        var config = BuildConfig(new()
        {
            ["Wopi:StorageProvider:ConnectionString"] = "UseDevelopmentStorage=true",
            ["Wopi:StorageProvider:ContainerName"] = "wopi-files",
        });

        services.AddAzureStorageProvider(config);

        using var sp = services.BuildServiceProvider();
        var storage = sp.GetRequiredService<IWopiStorageProvider>();
        var writable = sp.GetRequiredService<IWopiWritableStorageProvider>();
        Assert.Same(storage, writable);
        Assert.IsType<WopiAzureStorageProvider>(storage);
        // Container client and id map should be resolvable singletons.
        Assert.NotNull(sp.GetRequiredService<BlobContainerClient>());
        Assert.NotNull(sp.GetRequiredService<BlobIdMap>());
    }

    [Fact]
    public void AddAzureStorageProvider_WithServiceUriAndCredential_RegistersProvider()
    {
        var services = new ServiceCollection();
        AddNullLogging(services);
        services.AddSingleton<TokenCredential>(new FakeTokenCredential());
        var config = BuildConfig(new()
        {
            ["Wopi:StorageProvider:ServiceUri"] = "https://acct.blob.core.windows.net",
            ["Wopi:StorageProvider:ContainerName"] = "wopi-files",
        });

        services.AddAzureStorageProvider(config);

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<IWopiStorageProvider>());
    }

    [Fact]
    public void AddAzureStorageProvider_WithServiceUriAndNoCredential_FallsBackToDefaultCredential()
    {
        // No TokenCredential registered → ServiceCollectionExtensions must build a DefaultAzureCredential.
        // We can't easily assert "is DefaultAzureCredential" since it's encapsulated in the BlobServiceClient,
        // but resolution must not throw.
        var services = new ServiceCollection();
        AddNullLogging(services);
        var config = BuildConfig(new()
        {
            ["Wopi:StorageProvider:ServiceUri"] = "https://acct.blob.core.windows.net",
            ["Wopi:StorageProvider:ContainerName"] = "wopi-files",
        });

        services.AddAzureStorageProvider(config);

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<BlobContainerClient>());
    }

    [Fact]
    public void AddAzureStorageProvider_MissingContainerName_FailsValidation()
    {
        var services = new ServiceCollection();
        AddNullLogging(services);
        var config = BuildConfig(new()
        {
            ["Wopi:StorageProvider:ConnectionString"] = "UseDevelopmentStorage=true",
        });

        services.AddAzureStorageProvider(config);

        using var sp = services.BuildServiceProvider();
        var ex = Assert.Throws<OptionsValidationException>(
            () => sp.GetRequiredService<IOptions<WopiAzureStorageProviderOptions>>().Value);
        Assert.Contains("ContainerName", ex.Message);
    }

    [Fact]
    public void AddAzureStorageProvider_MissingConnectionStringAndServiceUri_FailsValidation()
    {
        var services = new ServiceCollection();
        AddNullLogging(services);
        var config = BuildConfig(new()
        {
            ["Wopi:StorageProvider:ContainerName"] = "wopi-files",
        });

        services.AddAzureStorageProvider(config);

        using var sp = services.BuildServiceProvider();
        var ex = Assert.Throws<OptionsValidationException>(
            () => sp.GetRequiredService<IOptions<WopiAzureStorageProviderOptions>>().Value);
        Assert.Contains("ConnectionString", ex.Message);
    }

    [Fact]
    public void AddAzureStorageProvider_NullArgs_Throw()
    {
        var services = new ServiceCollection();
        var config = BuildConfig(new());

        Assert.Throws<ArgumentNullException>(
            () => ServiceCollectionExtensions.AddAzureStorageProvider(null!, config));
        Assert.Throws<ArgumentNullException>(
            () => services.AddAzureStorageProvider(null!));
    }

    private sealed class FakeTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new("fake", DateTimeOffset.UtcNow.AddHours(1));
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new(new AccessToken("fake", DateTimeOffset.UtcNow.AddHours(1)));
    }
}
