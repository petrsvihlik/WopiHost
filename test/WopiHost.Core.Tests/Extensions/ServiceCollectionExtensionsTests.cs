using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WopiHost.Core.Extensions;

namespace WopiHost.Core.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWopi_RegistersIMemoryCache()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddWopi();
        var provider = services.BuildServiceProvider();

        // Assert
        var cache = provider.GetService<IMemoryCache>();
        Assert.NotNull(cache);
    }

    [Fact]
    public void AddWopi_ConfiguresLowercaseUrls()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddWopi();
        var provider = services.BuildServiceProvider();

        // Assert
        var routeOptions = provider.GetRequiredService<IOptions<RouteOptions>>().Value;
        Assert.True(routeOptions.LowercaseUrls);
    }
}
