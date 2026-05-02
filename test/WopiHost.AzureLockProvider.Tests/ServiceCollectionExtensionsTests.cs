using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.AzureLockProvider.Tests;

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
    public void AddAzureLockProvider_WithConnectionString_RegistersLockProvider()
    {
        var services = new ServiceCollection();
        AddNullLogging(services);
        var config = BuildConfig(new()
        {
            ["Wopi:LockProvider:ConnectionString"] = "UseDevelopmentStorage=true",
            ["Wopi:LockProvider:ContainerName"] = "wopi-locks",
        });

        services.AddAzureLockProvider(config);

        using var sp = services.BuildServiceProvider();
        var lockProvider = sp.GetRequiredService<IWopiLockProvider>();
        Assert.IsType<WopiAzureLockProvider>(lockProvider);
    }

    [Fact]
    public void AddAzureLockProvider_WithServiceUriAndCredential_RegistersLockProvider()
    {
        var services = new ServiceCollection();
        AddNullLogging(services);
        services.AddSingleton<TokenCredential>(new FakeTokenCredential());
        var config = BuildConfig(new()
        {
            ["Wopi:LockProvider:ServiceUri"] = "https://acct.blob.core.windows.net",
            ["Wopi:LockProvider:ContainerName"] = "wopi-locks",
        });

        services.AddAzureLockProvider(config);

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<IWopiLockProvider>());
    }

    [Fact]
    public void AddAzureLockProvider_WithServiceUriAndNoCredential_FallsBackToDefaultCredential()
    {
        var services = new ServiceCollection();
        AddNullLogging(services);
        var config = BuildConfig(new()
        {
            ["Wopi:LockProvider:ServiceUri"] = "https://acct.blob.core.windows.net",
            ["Wopi:LockProvider:ContainerName"] = "wopi-locks",
        });

        services.AddAzureLockProvider(config);

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<IWopiLockProvider>());
    }

    [Fact]
    public void AddAzureLockProvider_MissingContainerName_FailsValidation()
    {
        var services = new ServiceCollection();
        AddNullLogging(services);
        var config = BuildConfig(new()
        {
            ["Wopi:LockProvider:ConnectionString"] = "UseDevelopmentStorage=true",
            ["Wopi:LockProvider:ContainerName"] = "",
        });

        services.AddAzureLockProvider(config);

        using var sp = services.BuildServiceProvider();
        var ex = Assert.Throws<OptionsValidationException>(
            () => sp.GetRequiredService<IOptions<WopiAzureLockProviderOptions>>().Value);
        Assert.Contains("ContainerName", ex.Message);
    }

    [Fact]
    public void AddAzureLockProvider_MissingConnectionStringAndServiceUri_FailsValidation()
    {
        var services = new ServiceCollection();
        AddNullLogging(services);
        var config = BuildConfig(new()
        {
            ["Wopi:LockProvider:ContainerName"] = "wopi-locks",
        });

        services.AddAzureLockProvider(config);

        using var sp = services.BuildServiceProvider();
        var ex = Assert.Throws<OptionsValidationException>(
            () => sp.GetRequiredService<IOptions<WopiAzureLockProviderOptions>>().Value);
        Assert.Contains("ConnectionString", ex.Message);
    }

    [Fact]
    public void AddAzureLockProvider_NullArgs_Throw()
    {
        var services = new ServiceCollection();
        var config = BuildConfig(new());

        Assert.Throws<ArgumentNullException>(
            () => ServiceCollectionExtensions.AddAzureLockProvider(null!, config));
        Assert.Throws<ArgumentNullException>(
            () => services.AddAzureLockProvider(null!));
    }

    private sealed class FakeTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new("fake", DateTimeOffset.UtcNow.AddHours(1));
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new(new AccessToken("fake", DateTimeOffset.UtcNow.AddHours(1)));
    }
}
