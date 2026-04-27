using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Models;
using WopiHost.Core.Security;
using WopiHost.Core.Security.Authentication;
using WopiHost.Core.Security.Authorization;

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

    [Fact]
    public void AddWopi_Registers_Default_Security_Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddWopi();
        var provider = services.BuildServiceProvider();

        Assert.IsType<JwtAccessTokenService>(provider.GetRequiredService<IWopiAccessTokenService>());
        Assert.IsType<DefaultWopiPermissionProvider>(provider.GetRequiredService<IWopiPermissionProvider>());
    }

    [Fact]
    public void AddWopi_Defaults_Are_Overridable_Via_DI()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddWopi();
        services.AddSingleton<IWopiPermissionProvider, CustomPermissionProvider>();

        var provider = services.BuildServiceProvider();

        // Last registration wins for the resolved instance.
        Assert.IsType<CustomPermissionProvider>(provider.GetRequiredService<IWopiPermissionProvider>());
    }

    [Fact]
    public void AddWopi_With_Configure_Action_Applies_Options()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddWopi(o =>
        {
            o.UseCobalt = true;
            o.DefaultFilePermissions = WopiFilePermissions.ReadOnly;
        });

        var provider = services.BuildServiceProvider();
        var hostOptions = provider.GetRequiredService<IOptions<WopiHostOptions>>().Value;

        Assert.True(hostOptions.UseCobalt);
        Assert.Equal(WopiFilePermissions.ReadOnly, hostOptions.DefaultFilePermissions);
    }

    [Fact]
    public void ConfigureWopiSecurity_Binds_Options()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWopi();

        services.ConfigureWopiSecurity(o =>
        {
            o.SigningKey = JwtAccessTokenService.DeriveHmacKey("k");
            o.Issuer = "https://issuer";
            o.Audience = "aud";
            o.DefaultTokenLifetime = TimeSpan.FromMinutes(1);
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<WopiSecurityOptions>>().Value;

        Assert.NotNull(options.SigningKey);
        Assert.Equal("https://issuer", options.Issuer);
        Assert.Equal("aud", options.Audience);
        Assert.Equal(TimeSpan.FromMinutes(1), options.DefaultTokenLifetime);
    }

    [Fact]
    public void AddWopi_Throws_For_Null_Services() =>
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddWopi());

    [Fact]
    public void AddWopi_With_ConfigureAction_Throws_For_Null_Action() =>
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddWopi(null!));

    [Fact]
    public void ConfigureWopiSecurity_Throws_For_Null_Action() =>
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().ConfigureWopiSecurity(null!));

    private sealed class CustomPermissionProvider : IWopiPermissionProvider
    {
        public Task<WopiFilePermissions> GetFilePermissionsAsync(System.Security.Claims.ClaimsPrincipal user, IWopiFile file, CancellationToken cancellationToken = default)
            => Task.FromResult(WopiFilePermissions.None);
        public Task<WopiContainerPermissions> GetContainerPermissionsAsync(System.Security.Claims.ClaimsPrincipal user, IWopiFolder container, CancellationToken cancellationToken = default)
            => Task.FromResult(WopiContainerPermissions.None);
    }
}
