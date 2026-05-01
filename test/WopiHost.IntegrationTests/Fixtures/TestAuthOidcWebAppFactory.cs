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
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Wopi:HostUrl"] = _wopiBackendUrl,
                ["Wopi:ClientUrl"] = "https://office.example.test",
                ["Wopi:Discovery:NetZone"] = "ExternalHttps",
                ["Wopi:Discovery:RefreshInterval"] = "12:00:00",
                ["Wopi:StorageProvider:RootPath"] = TestPaths.WopiDocsRoot,
                ["Wopi:Security:SigningKey"] = Convert.ToBase64String(SigningKeyBytes(_wopiSigningSecret)),
                ["Oidc:Authority"] = _authority.AbsoluteUri.TrimEnd('/'),
                ["Oidc:ClientId"] = OidcWebAppFactory.TestClientId,
                ["Oidc:ClientSecret"] = OidcWebAppFactory.TestClientSecret,
                ["Oidc:RequireHttpsMetadata"] = "false",
                ["Oidc:RoleClaimType"] = "roles",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the default cookie scheme with our test handler so [Authorize] passes when
            // TestUser.SignIn() is in scope.
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
            // ClientUrl (which is unreachable test hostname). FakeDiscoverer returns canned values.
            services.RemoveAll<IDiscoverer>();
            services.AddSingleton<IDiscoverer, FakeDiscoverer>();
        });

        builder.UseEnvironment("Development");
    }

    private static byte[] SigningKeyBytes(string secret)
    {
        var raw = System.Text.Encoding.UTF8.GetBytes(secret);
        if (raw.Length >= 32) return raw;
        var padded = new byte[32];
        Array.Copy(raw, padded, raw.Length);
        return padded;
    }
}
