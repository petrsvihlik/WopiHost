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

                RelaxOidcCookiePolicyForInMemoryHttpTestServer(options);
            });
        });

        builder.UseEnvironment("Development");
    }

    /// <summary>
    /// <strong>TEST-ONLY.</strong> Loosens the OIDC correlation/nonce cookies from
    /// <c>SameSite=None; Secure</c> to <c>SameSite=Lax</c> + non-Secure, exclusively so the
    /// in-memory <see cref="Microsoft.AspNetCore.TestHost.TestServer"/> + <see cref="HttpClient"/>
    /// pair can complete the OIDC handshake over <c>http://localhost</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Do NOT mirror this in production (<c>sample/WopiHost.Web.Oidc/Program.cs</c>).</strong>
    /// The OIDC handler uses <c>response_mode=form_post</c>, meaning the IdP returns the user
    /// via a cross-site POST (browser auto-submits an HTML form to <c>/signin-oidc</c>).
    /// <c>SameSite=Lax</c> does not send cookies on cross-site POSTs — only on top-level GETs —
    /// so flipping production to Lax breaks real sign-ins. Production deliberately keeps the
    /// OIDC handler defaults (<c>SameSite=None; Secure</c>) and relies on real HTTPS to satisfy
    /// the Secure flag.
    /// </para>
    /// <para>
    /// <strong>Why the defaults can't stay in tests.</strong> <see cref="HttpClient"/>
    /// silently drops Secure cookies on plaintext <c>http://localhost</c>, and TestServer's
    /// transport is in-memory HTTP only (no TLS). The alternative would be to switch the test
    /// host to Kestrel with a dev cert — a meaningful architecture change for a single test
    /// class. Loosening the cookie policy only on the test factory is the smaller knob.
    /// </para>
    /// </remarks>
    private static void RelaxOidcCookiePolicyForInMemoryHttpTestServer(OpenIdConnectOptions options)
    {
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
        options.NonceCookie.SameSite = SameSiteMode.Lax;
        options.NonceCookie.SecurePolicy = CookieSecurePolicy.None;
    }
}
