namespace WopiHost.RedisLockProvider;

/// <summary>
/// Configuration for <see cref="WopiRedisLockProvider"/>.
/// </summary>
/// <remarks>
/// Bind to the <c>Wopi:LockProvider</c> configuration section. The
/// <see cref="ConnectionString"/> is forwarded to <c>StackExchange.Redis</c>'s
/// <c>ConnectionMultiplexer</c>; alternatively, register an <c>IConnectionMultiplexer</c> in DI
/// (e.g. via Aspire's <c>builder.AddRedisClient("wopi-locks")</c>) and the provider will resolve
/// it from the container instead of opening its own connection.
/// </remarks>
public class WopiRedisLockProviderOptions
{
    /// <summary>
    /// Default configuration section path this options class binds to. Use with
    /// <c>builder.Configuration.GetSection(WopiRedisLockProviderOptions.SectionName)</c>.
    /// </summary>
    public const string SectionName = "Wopi:LockProvider";

    /// <summary>
    /// StackExchange.Redis connection string (e.g. <c>localhost:6379</c>,
    /// <c>redis.example.test:6380,password=...,ssl=true</c>). Ignored if an
    /// <c>IConnectionMultiplexer</c> is already registered in DI — that takes precedence so
    /// Aspire orchestration / app-level Redis ownership keeps working.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Prefix prepended to every Redis key managed by the provider. Lets multiple WopiHost
    /// deployments share a single Redis instance without colliding. Defaults to <c>wopi:lock:</c>.
    /// </summary>
    public string KeyPrefix { get; set; } = "wopi:lock:";
}
