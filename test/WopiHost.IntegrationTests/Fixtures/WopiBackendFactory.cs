using Microsoft.AspNetCore.Mvc.Testing;
using WopiHost.Abstractions;
using WopiHost.Core.Security;
using WopiHost.Discovery;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> wrapping the WOPI backend sample. Overrides
/// the access-token signing secret to a known test value (must match
/// <see cref="OidcWebAppFactory"/>) and, by default, replaces the proof validator with a no-op
/// so tests can synthesize WOPI requests without forging proof-key headers.
/// </summary>
/// <param name="wopiSigningSecret">JWT-signing secret shared with the test frontend factory.</param>
/// <param name="storageRootPath">
/// Override for the file-system storage root. Defaults to <see cref="TestPaths.WopiDocsRoot"/>
/// (the shared sample). Mutating tests must pass a per-test temp directory so they don't
/// corrupt the sample for sibling tests.
/// </param>
/// <param name="configureServices">Optional callback for additional service registration,
/// e.g. wiring the <c>WopiAuthenticationSchemes.Bootstrap</c> auth scheme for bootstrap tests.</param>
/// <param name="useRealProofValidator">
/// When <see langword="true"/>, leaves the production <see cref="IWopiProofValidator"/> in place
/// and swaps the <see cref="IDiscoverer"/> for a test-only stub that exposes the in-process
/// RSA key pair (<see cref="FakeDiscovererWithProofKeys"/>). Lets a dedicated suite exercise
/// the proof-validation pipeline end-to-end with real signatures — see <see cref="ProofKeys"/>.
/// Pre-#456 every integration test ran through <see cref="AlwaysValidProofValidator"/>, so the
/// proof-validation surface had no integration coverage at all.
/// </param>
public sealed class WopiBackendFactory(
    string wopiSigningSecret,
    string? storageRootPath = null,
    Action<IServiceCollection>? configureServices = null,
    bool useRealProofValidator = false) : WebApplicationFactory<global::WopiHost.Program>
{
    private FakeDiscovererWithProofKeys? _proofKeyDiscoverer;

    /// <summary>
    /// When <c>useRealProofValidator</c> was set on construction, this surfaces the RSA key pair
    /// the production <see cref="IDiscoverer"/> is reporting as the WOPI client's proof keys.
    /// Tests use these to sign canonical request bytes; <see langword="null"/> in the default
    /// (no-op-validator) mode. The host application must be built (e.g. <c>CreateClient()</c>
    /// called) before this property is populated — services aren't materialised until then.
    /// </summary>
    public FakeDiscovererWithProofKeys? ProofKeys => _proofKeyDiscoverer;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Wopi:UseCobalt"] = "false",
                ["Sample:StorageProvider"] = "FileSystem",
                ["Sample:LockProvider"] = "Memory",
                ["Wopi:StorageProvider:RootPath"] = storageRootPath ?? TestPaths.WopiDocsRoot,
                ["Wopi:ClientUrl"] = "https://office.example.test",
                ["Wopi:Discovery:NetZone"] = "ExternalHttps",
                ["Wopi:Discovery:RefreshInterval"] = "12:00:00",
                [$"{WopiSecurityOptions.SectionName}:{nameof(WopiSecurityOptions.SigningKey)}"] = Convert.ToBase64String(OidcSampleTestConfig.SigningKeyBytes(wopiSigningSecret)),
            });
        });

        builder.ConfigureServices(services =>
        {
            if (useRealProofValidator)
            {
                // Leave WopiProofValidator in place and swap the discoverer so it returns known
                // RSA public keys. Tests can then sign requests with the corresponding private
                // keys to drive the validator through its full code path.
                _proofKeyDiscoverer = new FakeDiscovererWithProofKeys();
                services.RemoveAll<IDiscoverer>();
                services.AddSingleton<IDiscoverer>(_proofKeyDiscoverer);
            }
            else
            {
                // Tests synthesize WOPI requests without the X-WOPI-Proof headers a real Office
                // client would send; without this swap, every request 500s in
                // WopiOriginValidationEndpointFilter.
                services.RemoveAll<IWopiProofValidator>();
                services.AddSingleton<IWopiProofValidator, AlwaysValidProofValidator>();
            }
            configureServices?.Invoke(services);
        });

        builder.UseEnvironment("Development");
    }

    // No explicit Dispose override needed: _proofKeyDiscoverer is registered as the singleton
    // IDiscoverer in DI, and the host's ServiceProvider disposes singletons (including those
    // implementing IDisposable like FakeDiscovererWithProofKeys) when WebApplicationFactory's
    // base Dispose tears down the host.
}

internal static class ServiceCollectionRemoveAllExtensions
{
    public static IServiceCollection RemoveAll<TService>(this IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(TService))
            {
                services.RemoveAt(i);
            }
        }
        return services;
    }
}

/// <summary>Test-only proof validator. Real hosts must keep <c>WopiProofValidator</c>.</summary>
internal sealed class AlwaysValidProofValidator : IWopiProofValidator
{
    public Task<bool> ValidateProofAsync(WopiHost.Abstractions.WopiRequestInfo request, string accessToken) => Task.FromResult(true);
}
