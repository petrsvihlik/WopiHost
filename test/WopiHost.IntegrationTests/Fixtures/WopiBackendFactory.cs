using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> wrapping the WOPI backend sample. Overrides
/// the access-token signing secret to a known test value (must match
/// <see cref="OidcWebAppFactory"/>) and replaces the default proof validator with a no-op so
/// tests can synthesize WOPI requests without forging proof-key headers.
/// </summary>
public sealed class WopiBackendFactory(string wopiSigningSecret) : WebApplicationFactory<global::WopiHost.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Wopi:UseCobalt"] = "false",
                ["Wopi:StorageProviderAssemblyName"] = "WopiHost.FileSystemProvider",
                ["Wopi:LockProviderAssemblyName"] = "WopiHost.MemoryLockProvider",
                ["Wopi:StorageProvider:RootPath"] = TestPaths.WopiDocsRoot,
                ["Wopi:ClientUrl"] = "https://office.example.test",
                ["Wopi:Discovery:NetZone"] = "ExternalHttps",
                ["Wopi:Discovery:RefreshInterval"] = "12:00:00",
                ["Wopi:Security:SigningKey"] = Convert.ToBase64String(OidcSampleTestConfig.SigningKeyBytes(wopiSigningSecret)),
            });
        });

        builder.ConfigureServices(services =>
        {
            // Tests synthesize WOPI requests without the X-WOPI-Proof headers a real Office client
            // would send; without this swap, every request 500s in WopiOriginValidationActionFilter.
            services.RemoveAll<IWopiProofValidator>();
            services.AddSingleton<IWopiProofValidator, AlwaysValidProofValidator>();
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
    public Task<bool> ValidateProofAsync(Microsoft.AspNetCore.Http.HttpContext httpContext, string accessToken) => Task.FromResult(true);
}
