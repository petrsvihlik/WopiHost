using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc.Testing;
using WopiHost.Web.Oidc;

namespace WopiHost.IntegrationTests.Fixtures;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> wrapping the OIDC sample. Overrides the
/// OIDC authority and signing key so the sample talks to a controllable mock IdP and shares
/// a known WOPI access-token secret with <see cref="WopiBackendFactory"/>.
/// </summary>
public sealed class OidcWebAppFactory(Uri authority, string wopiSigningSecret, string wopiBackendUrl) : WebApplicationFactory<OidcSampleEntryPoint>
{
    /// <summary>Client id the sample registers with the mock IdP. Mock-oauth2-server accepts any id.</summary>
    public const string TestClientId = OidcSampleTestConfig.TestClientId;

    /// <summary>Client secret. Mock-oauth2-server accepts any value here.</summary>
    public const string TestClientSecret = OidcSampleTestConfig.TestClientSecret;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(OidcSampleTestConfig.Build(
                oidcAuthority: authority.AbsoluteUri.TrimEnd('/'),
                wopiSigningSecret: wopiSigningSecret,
                wopiBackendUrl: wopiBackendUrl));
        });

        builder.ConfigureServices(services =>
        {
            // Force HTTP metadata before the framework's PostConfigure validation runs. Configure
            // registers BEFORE PostConfigure, so this wins over the built-in validation.
            services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = false;
                options.Authority = authority.AbsoluteUri.TrimEnd('/');
                options.BackchannelHttpHandler = new HttpClientHandler();

                // Correlation/Nonce cookies default to SameSite=None — which the framework
                // auto-promotes to Secure (browsers reject None without Secure). Secure cookies
                // are dropped by HttpClient on http://localhost, so the TestServer never sees
                // them on the callback and the OIDC handler throws "Correlation failed." Force
                // Lax + non-Secure so OidcSignInTests can drive the handshake over plain HTTP.
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
                options.NonceCookie.SameSite = SameSiteMode.Lax;
                options.NonceCookie.SecurePolicy = CookieSecurePolicy.None;
            });
        });

        builder.UseEnvironment("Development");
    }
}
