using Microsoft.AspNetCore.Mvc.Testing;
using WopiHost.Abstractions;
using WopiHost.Core.Security;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> wrapping the WOPI backend sample. Overrides
/// the access-token signing secret to a known test value (must match
/// <see cref="OidcWebAppFactory"/>) and replaces the default proof validator with a no-op so
/// tests can synthesize WOPI requests without forging proof-key headers.
/// </summary>
/// <param name="wopiSigningSecret">JWT-signing secret shared with the test frontend factory.</param>
/// <param name="storageRootPath">
/// Override for the file-system storage root. Defaults to <see cref="TestPaths.WopiDocsRoot"/>
/// (the shared sample). Mutating tests must pass a per-test temp directory so they don't
/// corrupt the sample for sibling tests.
/// </param>
/// <param name="configureServices">Optional callback for additional service registration,
/// e.g. wiring the <c>WopiAuthenticationSchemes.Bootstrap</c> auth scheme for bootstrap tests.</param>
public sealed class WopiBackendFactory(
    string wopiSigningSecret,
    string? storageRootPath = null,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<global::WopiHost.Program>
{
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
            // Tests synthesize WOPI requests without the X-WOPI-Proof headers a real Office client
            // would send; without this swap, every request 500s in WopiOriginValidationEndpointFilter.
            services.RemoveAll<IWopiProofValidator>();
            services.AddSingleton<IWopiProofValidator, AlwaysValidProofValidator>();
            configureServices?.Invoke(services);
        });

        builder.UseEnvironment("Development");
    }
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
