using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace WopiHost.RedisLockProvider.Tests;

/// <summary>
/// Property-level coverage for <see cref="WopiRedisLockProviderOptions"/>. The properties are
/// trivial getters/setters but are exercised via configuration binding in production, so the
/// tests round-trip a config section through Options to confirm the binding shape matches the
/// keys the AppHost / appsettings.json conventions use.
/// </summary>
public class WopiRedisLockProviderOptionsTests
{
    [Fact]
    public void SectionName_MatchesConventionalKey()
    {
        // The provider's option section sits at Wopi:LockProvider, mirroring the Azure provider.
        // Catching a drift here prevents silent breakage when the AppHost forwards
        // Wopi__LockProvider__ConnectionString as an env var.
        Assert.Equal("Wopi:LockProvider", WopiRedisLockProviderOptions.SectionName);
    }

    [Fact]
    public void KeyPrefix_DefaultsToConventionalNamespace()
    {
        // Default prefix kept stable so existing deployments don't see their lock keys move on
        // upgrade. If you change this default, document it as a breaking change.
        var options = new WopiRedisLockProviderOptions();
        Assert.Equal("wopi:lock:", options.KeyPrefix);
    }

    [Fact]
    public void ConnectionString_DefaultsToNull()
    {
        // Null means "expect an IConnectionMultiplexer to be DI-registered" — see the
        // BuildOwnedMultiplexer branch in ServiceCollectionExtensions for how the two interact.
        var options = new WopiRedisLockProviderOptions();
        Assert.Null(options.ConnectionString);
    }

    [Fact]
    public void BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Wopi:LockProvider:ConnectionString"] = "redis.example.test:6380,ssl=true",
                ["Wopi:LockProvider:KeyPrefix"] = "wopi:custom:",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddOptions<WopiRedisLockProviderOptions>()
                .Bind(config.GetSection(WopiRedisLockProviderOptions.SectionName));

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<WopiRedisLockProviderOptions>>().Value;

        Assert.Equal("redis.example.test:6380,ssl=true", options.ConnectionString);
        Assert.Equal("wopi:custom:", options.KeyPrefix);
    }
}
