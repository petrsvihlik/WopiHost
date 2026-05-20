using System.Net;
using Microsoft.AspNetCore.Authentication;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authentication;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// HTTP-level coverage of the bootstrap endpoints. Bootstrap requires a separate
/// authentication scheme (<see cref="WopiAuthenticationSchemes.Bootstrap"/>, OAuth2 Bearer in
/// production) that the sample WOPI host doesn't register on its own — the fixture wires a
/// header-driven <see cref="TestAuthHandler"/> under that scheme name via the
/// <see cref="WopiBackendFactory"/>'s configure-services callback so requests can authenticate
/// without standing up a real IdP.
/// </summary>
public sealed class BootstrapEndpointTests(BootstrapEndpointTests.Fixture fixture) : IClassFixture<BootstrapEndpointTests.Fixture>
{
    private const string SharedSigningSecret = "bootstrap-tests-shared-key-32bytes!";
    // The proof-validation filter that's attached to every WOPI endpoint (including bootstrap)
    // rejects requests without an access_token in query string — production hosts would either
    // disable proof validation or have a custom proof validator that exempts bootstrap. For
    // these tests we pass any value and let AlwaysValidProofValidator accept it.
    private const string BootstrapDummyToken = "dummy-token-for-proof-filter";
    private readonly Fixture _fixture = fixture;

    [Fact]
    public async Task Bootstrap_GET_Returns_BootstrapInfo_WithEcosystemUrl()
    {
        using var client = _fixture.WopiBackend.CreateClient().AsBootstrapUser("user-1", "Alice");

        var resp = await client.GetAsync($"/wopibootstrapper?access_token={BootstrapDummyToken}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"Bootstrap\"", body);
        Assert.Contains("\"EcosystemUrl\"", body);
        Assert.Contains("user-1", body);
    }

    [Fact]
    public async Task Bootstrap_GET_Without_BootstrapToken_Returns_401()
    {
        // No AsBootstrapUser → TestAuthHandler returns NoResult → group's
        // RequireAuthorization with AddAuthenticationSchemes(Bootstrap) challenges → 401.
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopibootstrapper?access_token={BootstrapDummyToken}");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetRootContainer_POST_Returns_BootstrapInfo_AndRootContainerInfo()
    {
        using var client = _fixture.WopiBackend.CreateClient().AsBootstrapUser("user-2", "Bob");

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopibootstrapper?access_token={BootstrapDummyToken}");
        req.Headers.Add("X-WOPI-EcosystemOperation", "GET_ROOT_CONTAINER");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"Bootstrap\"", body);
        Assert.Contains("\"RootContainerInfo\"", body);
        Assert.Contains("\"ContainerPointer\"", body);
    }

    [Fact]
    public async Task GetNewAccessToken_POST_WithFileWopiSrc_Returns_Token()
    {
        var fileId = await _fixture.FirstFileIdAsync();

        using var client = _fixture.WopiBackend.CreateClient().AsBootstrapUser("user-3", "Carol");

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopibootstrapper?access_token={BootstrapDummyToken}");
        req.Headers.Add("X-WOPI-EcosystemOperation", "GET_NEW_ACCESS_TOKEN");
        req.Headers.Add("X-WOPI-WopiSrc", $"https://wopi.example.com/wopi/files/{fileId}");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"AccessTokenInfo\"", body);
        Assert.Contains("\"AccessToken\"", body);
        Assert.Contains("\"AccessTokenExpiry\"", body);
    }

    [Fact]
    public async Task GetNewAccessToken_POST_WithContainerWopiSrc_Returns_Token()
    {
        var rootId = _fixture.RootContainerId;
        using var client = _fixture.WopiBackend.CreateClient().AsBootstrapUser("user-4", "Dave");

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopibootstrapper?access_token={BootstrapDummyToken}");
        req.Headers.Add("X-WOPI-EcosystemOperation", "GET_NEW_ACCESS_TOKEN");
        req.Headers.Add("X-WOPI-WopiSrc", $"https://wopi.example.com/wopi/containers/{rootId}");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"AccessTokenInfo\"", body);
    }

    [Fact]
    public async Task GetNewAccessToken_POST_NoWopiSrcHeader_Returns_404()
    {
        using var client = _fixture.WopiBackend.CreateClient().AsBootstrapUser("user-5", "Eve");

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopibootstrapper?access_token={BootstrapDummyToken}");
        req.Headers.Add("X-WOPI-EcosystemOperation", "GET_NEW_ACCESS_TOKEN");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetNewAccessToken_POST_UnknownFile_Returns_404()
    {
        using var client = _fixture.WopiBackend.CreateClient().AsBootstrapUser("user-6", "Frank");
        var missing = new string('0', 64);

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopibootstrapper?access_token={BootstrapDummyToken}");
        req.Headers.Add("X-WOPI-EcosystemOperation", "GET_NEW_ACCESS_TOKEN");
        req.Headers.Add("X-WOPI-WopiSrc", $"https://wopi.example.com/wopi/files/{missing}");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetNewAccessToken_POST_UnknownContainer_Returns_404()
    {
        using var client = _fixture.WopiBackend.CreateClient().AsBootstrapUser("user-7", "Grace");
        var missing = new string('f', 64);

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopibootstrapper?access_token={BootstrapDummyToken}");
        req.Headers.Add("X-WOPI-EcosystemOperation", "GET_NEW_ACCESS_TOKEN");
        req.Headers.Add("X-WOPI-WopiSrc", $"https://wopi.example.com/wopi/containers/{missing}");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task UnknownEcosystemOperation_POST_Returns_501()
    {
        using var client = _fixture.WopiBackend.CreateClient().AsBootstrapUser("user-8", "Heidi");

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopibootstrapper?access_token={BootstrapDummyToken}");
        req.Headers.Add("X-WOPI-EcosystemOperation", "BOGUS_OPERATION");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotImplemented, resp.StatusCode);
    }

    /// <summary>
    /// Wires the Bootstrap auth scheme under TestAuthHandler so bootstrap requests can carry
    /// the test identity via headers (same pattern the OIDC integration tests use). The
    /// production scheme would be an OAuth2 Bearer JWT validator against the host's IdP.
    /// </summary>
    public sealed class Fixture : IDisposable
    {
        public WopiBackendFactory WopiBackend { get; }
        public string RootContainerId { get; }

        public Fixture()
        {
            WopiBackend = new WopiBackendFactory(
                SharedSigningSecret,
                configureServices: services =>
                {
                    services.AddAuthentication()
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(WopiAuthenticationSchemes.Bootstrap, _ => { });
                });

            using var scope = WopiBackend.Services.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IWopiStorageProvider>();
            RootContainerId = storage.RootContainer.Identifier;
        }

        public async Task<string> FirstFileIdAsync()
        {
            using var scope = WopiBackend.Services.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IWopiStorageProvider>();
            await foreach (var f in storage.GetWopiFiles(RootContainerId))
            {
                return f.Identifier;
            }
            throw new InvalidOperationException("sample/wopi-docs is empty.");
        }

        public void Dispose() => WopiBackend.Dispose();
    }
}

internal static class BootstrapAuthClientExtensions
{
    /// <summary>
    /// Attaches the TestAuthHandler headers — the bootstrap scheme uses the same header
    /// protocol since both schemes share TestAuthHandler in this fixture.
    /// </summary>
    public static HttpClient AsBootstrapUser(this HttpClient client, string sub, string? name = null, string? email = null)
        => client.AsUser(sub, name, email);
}
