using Microsoft.AspNetCore.Mvc.Testing;
using WopiHost.Abstractions;
using WopiHost.Core.Security;
using WopiHost.Discovery;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// Represents a <see cref="WebApplicationFactory{TEntryPoint}"/> wrapping the WOPI backend sample. Overrides
/// the access-token signing secret to a known test value (must match
/// <see cref="OidcWebAppFactory"/>) and, by default, replaces the proof validator with a no-op
/// so tests can synthesize WOPI requests without forging proof-key headers.
/// </summary>
/// <param name="wopiSigningSecret">The JWT-signing secret shared with the test frontend factory.</param>
/// <param name="storageRootPath">
/// The file-system storage root, or <see langword="null"/> for <see cref="TestPaths.WopiDocsRoot"/>
/// (the shared sample). Mutating tests must pass a per-test temp directory so they don't
/// corrupt the sample for sibling tests.
/// </param>
/// <param name="configureServices">An optional callback for additional service registration,
/// for example wiring the <c>WopiAuthenticationSchemes.Bootstrap</c> auth scheme for bootstrap tests.</param>
/// <param name="useRealProofValidator">
/// When <see langword="true"/>, leaves the production <see cref="IWopiProofValidator"/> in place
/// and swaps the <see cref="IDiscoverer"/> for a test-only stub that exposes the in-process
/// RSA key pairs (<see cref="FakeDiscovererWithProofKeys"/>), so a dedicated suite can exercise
/// the proof-validation pipeline end-to-end with real signatures (see <see cref="ProofKeys"/>).
/// When <see langword="false"/>, the proof validator is replaced by <see cref="AlwaysValidProofValidator"/>.
/// </param>
public sealed class WopiBackendFactory(
    string wopiSigningSecret,
    string? storageRootPath = null,
    Action<IServiceCollection>? configureServices = null,
    bool useRealProofValidator = false) : WebApplicationFactory<global::WopiHost.Program>
{
    private FakeDiscovererWithProofKeys? _proofKeyDiscoverer;

    /// <summary>
    /// Gets the discoverer whose RSA key pairs the backend reports as the WOPI client's proof keys,
    /// or <see langword="null"/> unless <c>useRealProofValidator</c> was set on construction.
    /// Tests use these keys to sign canonical request bytes.
    /// </summary>
    /// <remarks>
    /// The property is populated only once the host is built (for example by calling
    /// <c>CreateClient()</c>), because services aren't registered until then.
    /// </remarks>
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

/// <summary>Represents a test-only proof validator that accepts every request. Real hosts must keep <c>WopiProofValidator</c>.</summary>
internal sealed class AlwaysValidProofValidator : IWopiProofValidator
{
    public Task<bool> ValidateProofAsync(WopiHost.Abstractions.WopiRequestInfo request, string accessToken) => Task.FromResult(true);
}
