using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.CellBridge.Tests;

public class ServiceCollectionExtensionsTests
{
    private static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
            .Build();

    [Fact]
    public void AddCellBridgeProcessor_RegistersTheProcessorAsICobaltProcessor()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddCellBridgeProcessor(Configuration());

        using var provider = services.BuildServiceProvider();
        Assert.IsType<CellBridgeProcessor>(provider.GetRequiredService<ICobaltProcessor>());
    }

    [Fact]
    public void AddCellBridgeProcessor_BindsOptionsFromTheWopiCellBridgeSection()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddCellBridgeProcessor(Configuration(
            ("Wopi:CellBridge:SerializationProfile", "Current"),
            ("Wopi:CellBridge:WebOrigin", "https://wopi.example.test")));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CellBridgeProcessorOptions>>().Value;
        Assert.Equal(global::CellBridge.FssHttpB.FsshttpbSerializationProfile.Current, options.SerializationProfile);
        Assert.Equal("https://wopi.example.test", options.WebOrigin);
    }

    [Fact]
    public void AddCellBridgeProcessor_SecondBackend_Throws()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICobaltProcessor>(_ => throw new InvalidOperationException("never resolved"));

        Assert.Throws<InvalidOperationException>(() => services.AddCellBridgeProcessor(Configuration()));
    }
}
