using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// End-to-end test of the OIDC sign-in and sign-out flow against the Navikt
/// <c>mock-oauth2-server</c> Testcontainer. Complements <see cref="OidcStartupTests"/>: that
/// suite verifies the challenge redirect points at the IdP; this one follows through —
/// authorize → callback → cookie → protected page → sign-out → 401.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why this exists.</strong> The PR #479 migration moved <c>AccountController</c> to a
/// minimal-API <c>AccountEndpoints</c> (<c>Results.Challenge</c> / <c>Results.SignOut</c>).
/// <see cref="WopiTokenRoundTripTests"/> only exercises <c>[Authorize]</c> enforcement via
/// <c>TestAuthHandler</c>, which short-circuits the real OIDC handshake. Issue #480 closed the
/// coverage gap by driving the actual sign-in/out endpoints.
/// </para>
/// <para>
/// <strong>Why two HttpClients.</strong> The TestServer in-memory handler routes every request
/// through the app's pipeline by host. So a redirect to the mock IdP container would 404 inside
/// TestServer. The flow is therefore split: the TestClient (with cookie container) drives
/// /account/login and /signin-oidc; a plain HttpClient drives the mock IdP's authorize endpoint
/// out-of-band. The two clients are stitched by passing the authorize URL and callback URL
/// between them. Correlation cookies set during the challenge remain on the TestClient and are
/// replayed on the callback, satisfying the .NET OIDC handler's state/nonce check.
/// </para>
/// </remarks>
[Collection(nameof(MockOidcCollection))]
public partial class OidcSignInTests : IClassFixture<OidcSignInTests.AppFactory>
{
    // Matches <input type="hidden" name="X" value="Y"/> in the mock IdP's response_mode=form_post
    // body. The mock emits attributes in (type, name, value) order; if a future image release
    // reorders them, broaden this regex to two passes.
    [GeneratedRegex("""<input\s+type="hidden"\s+name="([^"]+)"\s+value="([^"]+)"\s*/>""")]
    private static partial Regex FormPostHiddenInputRegex();

    private readonly MockOidcServerFixture _mockOidc;
    private readonly AppFactory _factory;

    public OidcSignInTests(MockOidcServerFixture mockOidc, AppFactory factory)
    {
        _mockOidc = mockOidc;
        _factory = factory;
        _factory.MockOidc = mockOidc;
    }

    [Fact]
    public async Task SignIn_FullFlow_AuthenticatesUserAndAllowsAccessToProtectedPage()
    {
        Assert.SkipUnless(_mockOidc.IsAvailable, _mockOidc.UnavailableReason ?? "Docker unavailable.");

        var factory = _factory.CreateInstance();
        using var testClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost"),
            HandleCookies = true,
        });

        // Phase 1 — /account/login fires Results.Challenge → 302 to the mock IdP's authorize
        // endpoint. The OIDC handler also drops .AspNetCore.Correlation.* and
        // .AspNetCore.OpenIdConnect.Nonce.* cookies that the callback step depends on;
        // CookieContainer holds onto them. (See OidcWebAppFactory for the SameSite=Lax /
        // non-Secure override that makes those cookies survive an http://localhost client.)
        var loginResp = await testClient.GetAsync("/account/login");

        Assert.Equal(HttpStatusCode.Redirect, loginResp.StatusCode);
        var authorizeUrl = loginResp.Headers.Location
            ?? throw new InvalidOperationException("Challenge response missing Location header.");
        Assert.Equal(_mockOidc.Authority!.Host, authorizeUrl.Host);
        Assert.Contains("response_type=code", authorizeUrl.Query);

        // Phase 2 — Drive the mock IdP out-of-band. Two notable shapes here:
        //  (a) GET /authorize returns an HTML form (single <input name="username">). The
        //      OIDC handshake skips the GET; a POST to the same URL with the username field
        //      is enough, since the form action is the same URL.
        //  (b) The OIDC handler requested response_mode=form_post, so the mock IdP answers
        //      with 200 + HTML containing an auto-submitting <form action="/signin-oidc"
        //      method="post"> carrying hidden inputs `code` and `state`. We scrape those and
        //      replay them as a POST to the callback in Phase 3.
        using var idpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "test-user",
        });
        var authorizeResp = await idpClient.PostAsync(authorizeUrl, loginForm);

        Assert.Equal(HttpStatusCode.OK, authorizeResp.StatusCode);
        var callbackFormBody = await authorizeResp.Content.ReadAsStringAsync();
        var callbackFields = ParseFormPostFields(callbackFormBody);
        Assert.Contains("code", callbackFields.Keys);
        Assert.Contains("state", callbackFields.Keys);

        // Phase 3 — POST the callback fields to /signin-oidc on the TestClient. The correlation
        // cookie set in Phase 1 rides along, the OIDC handler validates state/nonce, exchanges
        // the code for tokens, and issues the .AspNetCore.Cookies auth cookie. Result: 302 to /.
        using var callbackForm = new FormUrlEncodedContent(callbackFields);
        var callbackResp = await testClient.PostAsync("/signin-oidc", callbackForm);

        Assert.Equal(HttpStatusCode.Redirect, callbackResp.StatusCode);
        Assert.Equal("/", callbackResp.Headers.Location?.OriginalString);

        // Phase 4 — The auth cookie now resolves [Authorize] and we get the Browse page.
        var homeResp = await testClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeResp.StatusCode);
    }

    [Fact]
    public async Task SignOut_AfterSignIn_ClearsCookieAndProtectedAccessIs401()
    {
        Assert.SkipUnless(_mockOidc.IsAvailable, _mockOidc.UnavailableReason ?? "Docker unavailable.");

        var factory = _factory.CreateInstance();
        using var testClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost"),
            HandleCookies = true,
        });

        await SignInAsync(testClient);

        // Sanity: signed in, / returns 200.
        var beforeSignOut = await testClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, beforeSignOut.StatusCode);

        // POST /account/logout — matches what the MainLayout's sign-out form does. The
        // endpoint accepts POST and is AllowAnonymous; anti-forgery is NOT enforced because
        // minimal-API endpoints without form binding don't carry IAntiforgeryMetadata, so a
        // bare POST is sufficient here.
        var logoutResp = await testClient.PostAsync("/account/logout", content: null);

        // SignOut redirects to / after clearing the cookie schemes. The 302 is the proof the
        // SignOut result fired; the missing auth cookie is verified by the follow-up request.
        Assert.Equal(HttpStatusCode.Redirect, logoutResp.StatusCode);

        // After sign-out, hitting the protected root again triggers a fresh challenge — i.e.
        // 302 to the IdP, not 200. (TestAuthHandler returns 401 directly; the real cookie
        // handler with a LoginPath redirects.)
        var afterSignOut = await testClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, afterSignOut.StatusCode);
        Assert.Equal(_mockOidc.Authority!.Host, afterSignOut.Headers.Location?.Host);
    }

    /// <summary>Drives Phases 1-3 of <see cref="SignIn_FullFlow_AuthenticatesUserAndAllowsAccessToProtectedPage"/> to land an authenticated cookie on <paramref name="testClient"/>.</summary>
    private async Task SignInAsync(HttpClient testClient)
    {
        var loginResp = await testClient.GetAsync("/account/login");
        var authorizeUrl = loginResp.Headers.Location!;

        using var idpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "test-user",
        });
        var authorizeResp = await idpClient.PostAsync(authorizeUrl, loginForm);
        var callbackFields = ParseFormPostFields(await authorizeResp.Content.ReadAsStringAsync());

        using var callbackForm = new FormUrlEncodedContent(callbackFields);
        var callbackResp = await testClient.PostAsync("/signin-oidc", callbackForm);
        if (callbackResp.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException(
                $"OIDC callback did not complete the handshake (status {(int)callbackResp.StatusCode}). " +
                $"Body: {await callbackResp.Content.ReadAsStringAsync()}");
        }
    }

    /// <summary>
    /// Parses hidden-input name/value pairs out of a mock-oauth2-server form_post HTML body.
    /// Returns the fields a real browser would have auto-submitted to the callback URL.
    /// </summary>
    private static Dictionary<string, string> ParseFormPostFields(string formHtml)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in FormPostHiddenInputRegex().Matches(formHtml))
        {
            fields[m.Groups[1].Value] = System.Net.WebUtility.HtmlDecode(m.Groups[2].Value);
        }
        return fields;
    }

    /// <summary>
    /// Per-test-class wrapper that holds the single <see cref="OidcWebAppFactory"/> instance.
    /// Mirrors <see cref="OidcStartupTests.AppFactory"/>; kept separate so each test class
    /// gets an independent lifecycle (xUnit class fixtures are per-class-per-type).
    /// </summary>
    public sealed class AppFactory : IDisposable
    {
        public MockOidcServerFixture MockOidc { get; set; } = default!;
        private OidcWebAppFactory? _instance;

        public OidcWebAppFactory CreateInstance()
        {
            if (_instance is not null) return _instance;
            if (!MockOidc.IsAvailable)
            {
                throw new InvalidOperationException("Cannot create OIDC factory without a running mock IdP.");
            }
            _instance = new OidcWebAppFactory(
                MockOidc.Authority!,
                wopiSigningSecret: "integration-test-shared-key-32bytes!",
                wopiBackendUrl: "http://wopi-backend.test");
            return _instance;
        }

        public void Dispose() => _instance?.Dispose();
    }
}
