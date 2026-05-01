using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WopiHost.Web.Oidc;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> wrapping the OIDC sample. Overrides the
/// OIDC authority and signing key so the sample talks to a controllable mock IdP and shares
/// a known WOPI access-token secret with <see cref="WopiBackendFactory"/>.
/// </summary>
public sealed class OidcWebAppFactory : WebApplicationFactory<OidcSampleEntryPoint>
{
    private readonly Uri _authority;
    private readonly string _wopiSigningSecret;
    private readonly string _wopiBackendUrl;

    public OidcWebAppFactory(Uri authority, string wopiSigningSecret, string wopiBackendUrl)
    {
        _authority = authority;
        _wopiSigningSecret = wopiSigningSecret;
        _wopiBackendUrl = wopiBackendUrl;
    }

    /// <summary>Client id the sample registers with the mock IdP. Mock-oauth2-server accepts any id.</summary>
    public const string TestClientId = "wopihost-oidc-sample";

    /// <summary>Client secret. Mock-oauth2-server accepts any value here.</summary>
    public const string TestClientSecret = "test-client-secret";

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
                ["Oidc:ClientId"] = TestClientId,
                ["Oidc:ClientSecret"] = TestClientSecret,
                ["Oidc:RequireHttpsMetadata"] = "false",
                ["Oidc:UsePkce"] = "true",
                ["Oidc:RoleClaimType"] = "roles",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Force HTTP metadata before the framework's PostConfigure validation runs. Configure
            // registers BEFORE PostConfigure, so this wins over the built-in validation.
            services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = false;
                options.Authority = _authority.AbsoluteUri.TrimEnd('/');
                options.BackchannelHttpHandler = new HttpClientHandler();
            });
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
