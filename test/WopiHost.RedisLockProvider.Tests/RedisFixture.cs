using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace WopiHost.RedisLockProvider.Tests;

/// <summary>
/// xUnit collection fixture that boots a Redis 7 container once per test run and exposes a shared
/// <see cref="ConnectionString"/>. Each test creates its own <see cref="IConnectionMultiplexer"/>
/// via <see cref="CreateMultiplexerAsync"/> and uses a GUID-suffixed key prefix so cases don't
/// step on each other.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private RedisContainer? _container;

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Redis container not started.");

    public async ValueTask InitializeAsync()
    {
        _container = new RedisBuilder("redis:7-alpine").Build();
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public async Task<IConnectionMultiplexer> CreateMultiplexerAsync()
        => await ConnectionMultiplexer.ConnectAsync(ConnectionString);
}

[CollectionDefinition(Name)]
public sealed class RedisCollection : ICollectionFixture<RedisFixture>
{
    public const string Name = "Redis";
}
