using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WopiHost.Discovery;
using WopiHost.Web.Oidc;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// Variant of <see cref="OidcWebAppFactory"/> that swaps the cookie/OIDC auth pipeline for
/// <see cref="TestAuthHandler"/> so tests can exercise the WOPI-minting controllers without a
/// live IdP. The mock OIDC server still has to exist (the OIDC handler validates discovery at
/// startup) — this factory just routes the actual authentication to the test handler.
/// </summary>
public sealed class TestAuthOidcWebAppFactory : WebApplicationFactory<OidcSampleEntryPoint>
{
    private readonly Uri _authority;
    private readonly string _wopiSigningSecret;
    private readonly string _wopiBackendUrl;

    /// <summary>Default placeholder authority — never contacted because TestAuth bypasses OIDC.</summary>
    public const string PlaceholderAuthority = "https://placeholder.invalid/";

    public TestAuthOidcWebAppFactory(string wopiSigningSecret, string wopiBackendUrl, Uri? authority = null)
    {
        _authority = authority ?? new Uri(PlaceholderAuthority);
        _wopiSigningSecret = wopiSigningSecret;
        _wopiBackendUrl = wopiBackendUrl;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(OidcSampleTestConfig.Build(
                oidcAuthority: _authority.AbsoluteUri.TrimEnd('/'),
                wopiSigningSecret: _wopiSigningSecret,
                wopiBackendUrl: _wopiBackendUrl));
        });

        builder.ConfigureServices(services =>
        {
            // Replace the default cookie scheme with our test handler so [Authorize] passes when
            // a request includes the test-user headers (see TestAuthClientExtensions.AsUser).
            var defaultScheme = TestAuthHandler.SchemeName;
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = defaultScheme;
                options.DefaultAuthenticateScheme = defaultScheme;
                options.DefaultChallengeScheme = defaultScheme;
                options.DefaultSignInScheme = defaultScheme;
                options.DefaultSignOutScheme = defaultScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(defaultScheme, _ => { });

            services.AddAuthorization();

            // Replace the discoverer so tests don't try to fetch discovery XML from the configured
            // ClientUrl (which is an unreachable test hostname). FakeDiscoverer returns canned values.
            services.RemoveAll<IDiscoverer>();
            services.AddSingleton<IDiscoverer, FakeDiscoverer>();
        });

        builder.UseEnvironment("Development");
    }
}
