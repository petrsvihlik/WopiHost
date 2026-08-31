using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace WopiHost.RedisLockProvider.Tests;

/// <summary>
/// xUnit collection fixture that boots a Redis 8 container once per test run and shares a single
/// lazily-created <see cref="IConnectionMultiplexer"/> via <see cref="GetMultiplexerAsync"/>.
/// Tests isolate their state with GUID-suffixed key prefixes, so sharing the connection is safe
/// and avoids per-test TCP/handshake churn (a multiplexer owns sockets and reader threads).
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private RedisContainer? _container;
    private IConnectionMultiplexer? _multiplexer;

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Redis container not started.");

    public async ValueTask InitializeAsync()
    {
        _container = new RedisBuilder("redis:8-alpine").Build();
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _multiplexer?.Dispose();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    // No init race: classes sharing a collection fixture run sequentially in xUnit, so the
    // first caller connects and later callers reuse the connection.
    public async Task<IConnectionMultiplexer> GetMultiplexerAsync()
        => _multiplexer ??= await ConnectionMultiplexer.ConnectAsync(ConnectionString);
}

[CollectionDefinition(Name)]
public sealed class RedisCollection : ICollectionFixture<RedisFixture>
{
    public const string Name = "Redis";
}
