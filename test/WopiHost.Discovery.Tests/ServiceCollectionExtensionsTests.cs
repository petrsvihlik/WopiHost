using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery.Tests;

public class ServiceCollectionExtensionsTests
{
    private sealed class FakeOptions : IDiscoveryOptions
    {
        public Uri? ClientUrl { get; set; } = new("http://wopi.example.com");
    }

    private static ServiceCollection BuildServicesWithFakeOptions()
    {
        var services = new ServiceCollection();
        services.Configure<FakeOptions>(o => o.ClientUrl = new Uri("http://wopi.example.com"));
        return services;
    }

    [Fact]
    public void AddWopiDiscovery_RegistersDiscovererAndFileProvider()
    {
        var services = BuildServicesWithFakeOptions();

        services.AddWopiDiscovery<FakeOptions>(o => o.NetZone = NetZoneEnum.InternalHttp);

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<IDiscoverer>());
        Assert.NotNull(sp.GetService<IDiscoveryFileProvider>());
    }

    [Fact]
    public void AddWopiDiscovery_AppliesDiscoveryOptions()
    {
        var services = BuildServicesWithFakeOptions();

        services.AddWopiDiscovery<FakeOptions>(o =>
        {
            o.NetZone = NetZoneEnum.ExternalHttps;
            o.RefreshInterval = TimeSpan.FromMinutes(5);
        });

        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<DiscoveryOptions>>().Value;
        Assert.Equal(NetZoneEnum.ExternalHttps, opts.NetZone);
        Assert.Equal(TimeSpan.FromMinutes(5), opts.RefreshInterval);
    }

    [Fact]
    public void AddWopiDiscovery_HttpClient_ConfiguresBaseAddressFromOptions()
    {
        var services = BuildServicesWithFakeOptions();
        services.AddWopiDiscovery<FakeOptions>(_ => { });

        using var sp = services.BuildServiceProvider();

        // Resolving the file provider triggers the HttpClient configurator lambda
        // (lines `client.BaseAddress = wopiOptions.Value.ClientUrl;`).
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(nameof(IDiscoveryFileProvider));
        Assert.Equal(new Uri("http://wopi.example.com"), client.BaseAddress);
    }

    [Fact]
    public void AddWopiDiscovery_ReturnsSameServiceCollection()
    {
        var services = BuildServicesWithFakeOptions();

        var returned = services.AddWopiDiscovery<FakeOptions>(_ => { });

        Assert.Same(services, returned);
    }
}
