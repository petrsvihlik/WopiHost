using Microsoft.Extensions.DependencyInjection;

namespace WopiHost.Abstractions;

/// <summary>
/// Startup-time guard for the <c>Wopi:LockProvider</c> configuration section. Every lock-provider
/// package that binds the section (currently <c>WopiHost.AzureLockProvider</c> and
/// <c>WopiHost.RedisLockProvider</c>) calls <see cref="ClaimSection"/> from its
/// <c>Add*LockProvider</c> extension. The second claim throws so a host can't accidentally
/// register two providers that fight over the same configuration values (and silently end up
/// with whichever registration ran last).
/// </summary>
/// <remarks>
/// The marker is registered as a singleton on <see cref="IServiceCollection"/>. Detection runs
/// against the unbuilt service collection (not the provider), so registration errors surface at
/// composition time rather than on first resolution. A test that wires multiple providers in
/// isolation can either pass a fresh <see cref="IServiceCollection"/> each time or — for the
/// rare case where the test specifically needs to bypass the guard — call
/// <see cref="ReleaseSection"/>; that's also what the provider unit-test projects use when they
/// exercise both <c>AddAzureLockProvider</c> and <c>AddRedisLockProvider</c> against the same
/// container.
/// </remarks>
public static class WopiLockProviderRegistrationGuard
{
    /// <summary>
    /// Marker registered the first time a provider claims <c>Wopi:LockProvider</c>. The provider
    /// name is stored so the second claim's error message can name the conflicting provider.
    /// </summary>
    public sealed class SectionClaim(string providerName)
    {
        /// <summary>Name of the provider that first claimed the section.</summary>
        public string ProviderName { get; } = providerName;
    }

    /// <summary>
    /// Records that <paramref name="providerName"/> has bound the <c>Wopi:LockProvider</c>
    /// configuration section. Throws <see cref="InvalidOperationException"/> when a different
    /// provider already claimed it.
    /// </summary>
    /// <param name="services">DI service collection.</param>
    /// <param name="providerName">Friendly provider name used in the error message (e.g.
    /// <c>"WopiHost.AzureLockProvider"</c>).</param>
    public static void ClaimSection(IServiceCollection services, string providerName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(SectionClaim));
        if (existing is not null)
        {
            // Re-claim by the same provider is a no-op (idempotent registration is a reasonable
            // user expectation — e.g. AddAzureLockProvider called twice in composition).
            var owner = (existing.ImplementationInstance as SectionClaim)?.ProviderName;
            if (string.Equals(owner, providerName, StringComparison.Ordinal))
            {
                return;
            }
            throw new InvalidOperationException(
                $"The 'Wopi:LockProvider' configuration section is already claimed by '{owner}'. " +
                $"Cannot also register '{providerName}' — both providers bind the same section and the " +
                "second registration would silently overwrite the first IWopiLockProvider. Pick one " +
                "lock-provider package and remove the other Add*LockProvider call.");
        }

        services.AddSingleton(new SectionClaim(providerName));
    }

    /// <summary>
    /// Removes the existing section claim if any. Provided for tests that wire and re-wire lock
    /// providers in the same <see cref="IServiceCollection"/>; not intended for production use.
    /// </summary>
    public static void ReleaseSection(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(SectionClaim))
            {
                services.RemoveAt(i);
            }
        }
    }
}
