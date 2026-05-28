using Microsoft.Extensions.DependencyInjection;
using WopiHost.Abstractions;

namespace WopiHost.MemoryLockProvider;

/// <summary>
/// DI extensions to register <see cref="MemoryLockProvider"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="MemoryLockProvider"/> as the singleton <see cref="IWopiLockProvider"/>.
    /// </summary>
    /// <remarks>
    /// Singleton lifetime ensures every request in the process shares the same in-memory lock
    /// dictionary — anything narrower would let concurrent requests see independent stores and
    /// silently break exclusion. State does not survive process restart or extend across
    /// instances; use <c>WopiHost.RedisLockProvider</c> or <c>WopiHost.AzureLockProvider</c>
    /// for multi-instance deployments.
    /// </remarks>
    public static IServiceCollection AddMemoryLockProvider(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // Only one IWopiLockProvider can be in DI at a time — a host that wires two would have
        // the second registration silently win the resolve. Fail fast at composition instead.
        if (services.Any(d => d.ServiceType == typeof(IWopiLockProvider)))
        {
            throw new InvalidOperationException(
                $"An {nameof(IWopiLockProvider)} is already registered. {nameof(AddMemoryLockProvider)} cannot " +
                "coexist with another lock-provider registration — pick one (Memory / Azure / Redis).");
        }
        services.AddSingleton<IWopiLockProvider, MemoryLockProvider>();
        return services;
    }
}
