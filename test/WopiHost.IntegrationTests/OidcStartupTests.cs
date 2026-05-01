using System.Net;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// Verifies the OIDC sample boots against the mock IdP and accepts/rejects requests as
/// authenticated/anonymous correctly. Skipped if Docker / Testcontainers is unavailable.
/// </summary>
[Collection(nameof(MockOidcCollection))]
public class OidcStartupTests : IClassFixture<OidcStartupTests.AppFactory>
{
    private readonly MockOidcServerFixture _mockOidc;
    private readonly AppFactory _factory;

    public OidcStartupTests(MockOidcServerFixture mockOidc, AppFactory factory)
    {
        _mockOidc = mockOidc;
        _factory = factory;
        _factory.MockOidc = mockOidc;
    }

    [SkippableFact]
    public async Task AnonymousRequest_RedirectsToOidcAuthority()
    {
        Skip.IfNot(_mockOidc.IsAvailable, _mockOidc.UnavailableReason ?? "Docker unavailable.");

        using var client = _factory.CreateInstance().CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost"),
        });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains(_mockOidc.Authority!.AbsoluteUri, location);
        Assert.Contains("response_type=code", location);
        Assert.Contains("client_id=" + OidcWebAppFactory.TestClientId, location);
    }

    [SkippableFact]
    public async Task DiscoveryDocument_IsReachableFromAppHost()
    {
        Skip.IfNot(_mockOidc.IsAvailable, _mockOidc.UnavailableReason ?? "Docker unavailable.");

        using var http = new HttpClient { BaseAddress = _mockOidc.Authority };
        var discovery = await http.GetAsync(".well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);
        var doc = System.Text.Json.JsonDocument.Parse(await discovery.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("issuer", out _));
        Assert.True(doc.RootElement.TryGetProperty("authorization_endpoint", out _));
        Assert.True(doc.RootElement.TryGetProperty("jwks_uri", out _));
    }

    [SkippableFact]
    public async Task HealthEndpoint_IsAnonymous()
    {
        Skip.IfNot(_mockOidc.IsAvailable, _mockOidc.UnavailableReason ?? "Docker unavailable.");

        using var client = _factory.CreateInstance().CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost"),
        });

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

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
