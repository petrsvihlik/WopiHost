using Microsoft.Extensions.DependencyInjection;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.Core.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="WopiLockProviderRegistrationGuard"/>. Lives in Core.Tests so we
/// don't have to spin up a dedicated test project for one class in Abstractions; the guard's
/// surface is small enough that a colocated test sufice.
/// </summary>
public class WopiLockProviderRegistrationGuardTests
{
    [Fact]
    public void ClaimSection_FirstClaim_Succeeds()
    {
        var services = new ServiceCollection();

        WopiLockProviderRegistrationGuard.ClaimSection(services, "WopiHost.AzureLockProvider");

        // The marker is registered as a singleton — assert we can resolve it.
        using var sp = services.BuildServiceProvider();
        var claim = sp.GetRequiredService<WopiLockProviderRegistrationGuard.SectionClaim>();
        Assert.Equal("WopiHost.AzureLockProvider", claim.ProviderName);
    }

    [Fact]
    public void ClaimSection_DuplicateClaim_BySameProvider_IsIdempotent()
    {
        // Re-claim by the same provider is a no-op (idempotent registration) so callers can
        // call Add*LockProvider multiple times in composition without tripping the guard.
        var services = new ServiceCollection();
        WopiLockProviderRegistrationGuard.ClaimSection(services, "WopiHost.AzureLockProvider");
        WopiLockProviderRegistrationGuard.ClaimSection(services, "WopiHost.AzureLockProvider");

        var markers = services.Where(d => d.ServiceType == typeof(WopiLockProviderRegistrationGuard.SectionClaim)).ToList();
        Assert.Single(markers);
    }

    [Fact]
    public void ClaimSection_DifferentProvider_Throws()
    {
        // The whole point of the guard: a host that wires both Azure and Redis lock providers
        // against 'Wopi:LockProvider' must fail fast, not silently let the second registration
        // overwrite the first IWopiLockProvider.
        var services = new ServiceCollection();
        WopiLockProviderRegistrationGuard.ClaimSection(services, "WopiHost.AzureLockProvider");

        var ex = Assert.Throws<InvalidOperationException>(
            () => WopiLockProviderRegistrationGuard.ClaimSection(services, "WopiHost.RedisLockProvider"));
        Assert.Contains("WopiHost.AzureLockProvider", ex.Message);
        Assert.Contains("WopiHost.RedisLockProvider", ex.Message);
    }

    [Fact]
    public void ReleaseSection_RemovesClaim_AllowingReRegistration()
    {
        // ReleaseSection is the test-only escape hatch — production hosts never call it, but
        // test code that wires providers in sequence relies on it to reset state between cases.
        var services = new ServiceCollection();
        WopiLockProviderRegistrationGuard.ClaimSection(services, "WopiHost.AzureLockProvider");
        WopiLockProviderRegistrationGuard.ReleaseSection(services);

        WopiLockProviderRegistrationGuard.ClaimSection(services, "WopiHost.RedisLockProvider");

        using var sp = services.BuildServiceProvider();
        var claim = sp.GetRequiredService<WopiLockProviderRegistrationGuard.SectionClaim>();
        Assert.Equal("WopiHost.RedisLockProvider", claim.ProviderName);
    }

    [Fact]
    public void ClaimSection_NullArguments_Throw()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(
            () => WopiLockProviderRegistrationGuard.ClaimSection(null!, "x"));
        Assert.Throws<ArgumentException>(
            () => WopiLockProviderRegistrationGuard.ClaimSection(services, ""));
        Assert.Throws<ArgumentNullException>(
            () => WopiLockProviderRegistrationGuard.ClaimSection(services, null!));
    }
}
