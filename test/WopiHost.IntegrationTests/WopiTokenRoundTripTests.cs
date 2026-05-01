using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text.RegularExpressions;
using WopiHost.Abstractions;
using WopiHost.IntegrationTests.Fixtures;
using WopiHost.Web.Oidc.Infrastructure;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// End-to-end test: a signed-in OIDC user opens a file, the frontend mints a WOPI access token
/// from their identity + roles, and the WOPI backend accepts that token and reflects the user
/// identity in CheckFileInfo. No Docker required (TestAuthHandler stands in for the OIDC handler).
/// </summary>
public sealed class WopiTokenRoundTripTests : IClassFixture<WopiTokenRoundTripTests.Fixture>
{
    private const string SharedSigningSecret = "integration-test-shared-key-32bytes!";
    private static readonly Regex AccessTokenInput = new(
        """<input name="access_token" value="([^"]+)" type="hidden" />""",
        RegexOptions.Compiled);

    private readonly Fixture _fixture;

    public WopiTokenRoundTripTests(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SignedInEditor_HostpageEmitsToken_ContainingOidcIdentity()
    {
        using var client = _fixture.OidcFrontend.CreateClient()
            .AsUser("user-1234", "Alice Editor", "alice@example.com", OidcRolePermissionMapper.EditorRole);

        var fileId = await ResolveFirstFileIdAsync();
        var response = await client.GetAsync($"/Home/Detail/{fileId}?wopiAction=Edit");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var token = ExtractAccessToken(html);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("user-1234", jwt.Claims.First(c => c.Type == "nameid").Value);
        Assert.Equal("Alice Editor", jwt.Claims.First(c => c.Type == WopiClaimTypes.UserDisplayName).Value);
        Assert.Equal("alice@example.com", jwt.Claims.First(c => c.Type == "email").Value);
        Assert.Equal(fileId, jwt.Claims.First(c => c.Type == WopiClaimTypes.ResourceId).Value);

        var permissionsClaim = jwt.Claims.First(c => c.Type == WopiClaimTypes.FilePermissions).Value;
        Assert.Contains(nameof(WopiFilePermissions.UserCanWrite), permissionsClaim);
    }

    [Fact]
    public async Task SignedInViewer_HostpageEmitsToken_WithoutWritePermissions()
    {
        using var client = _fixture.OidcFrontend.CreateClient()
            .AsUser("user-2222", "Bob Viewer", "bob@example.com", OidcRolePermissionMapper.ViewerRole);

        var fileId = await ResolveFirstFileIdAsync();
        var response = await client.GetAsync($"/Home/Detail/{fileId}?wopiAction=View");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var token = ExtractAccessToken(html);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var permissionsClaim = jwt.Claims.First(c => c.Type == WopiClaimTypes.FilePermissions).Value;
        Assert.DoesNotContain(nameof(WopiFilePermissions.UserCanWrite), permissionsClaim);
        Assert.Contains(nameof(WopiFilePermissions.UserCanAttend), permissionsClaim);
    }

    [Fact]
    public async Task MintedToken_AcceptedBy_BackendCheckFileInfo()
    {
        // 1. Frontend mints a token bound to user + file.
        using var frontend = _fixture.OidcFrontend.CreateClient()
            .AsUser("user-3333", "Carol Editor", "carol@example.com", OidcRolePermissionMapper.EditorRole);
        var fileId = await ResolveFirstFileIdAsync();
        var hostpage = await frontend.GetAsync($"/Home/Detail/{fileId}?wopiAction=Edit");
        hostpage.EnsureSuccessStatusCode();
        var token = ExtractAccessToken(await hostpage.Content.ReadAsStringAsync());

        // 2. Backend validates the token and answers CheckFileInfo with our user identity.
        using var backend = _fixture.WopiBackend.CreateClient();
        var checkInfoResponse = await backend.GetAsync($"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, checkInfoResponse.StatusCode);
        var body = await checkInfoResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"UserId\"", body);
        Assert.Contains("user-3333", body);
        Assert.Contains("Carol Editor", body);
    }

    [Fact]
    public async Task AnonymousRequest_ReturnsUnauthorized()
    {
        // No AsUser() → TestAuthHandler returns NoResult → [Authorize] challenges → 401.
        using var client = _fixture.OidcFrontend.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost"),
        });

        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidToken_RejectedBy_Backend()
    {
        var fileId = await ResolveFirstFileIdAsync();
        using var backend = _fixture.WopiBackend.CreateClient();
        var response = await backend.GetAsync($"/wopi/files/{fileId}?access_token=this-is-not-a-jwt");

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.InternalServerError,
            $"Expected 401 or 500 for an invalid token, got {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task UserWithoutRoles_GetsTokenWithoutPermissions()
    {
        using var client = _fixture.OidcFrontend.CreateClient()
            .AsUser("user-no-roles", "Dave", "dave@example.com");

        var fileId = await ResolveFirstFileIdAsync();
        var response = await client.GetAsync($"/Home/Detail/{fileId}?wopiAction=View");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var token = ExtractAccessToken(html);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.DoesNotContain(jwt.Claims, c => c.Type == WopiClaimTypes.FilePermissions);
    }

    private async Task<string> ResolveFirstFileIdAsync()
    {
        // Sign in just to retrieve the file list — viewer is sufficient.
        using var client = _fixture.OidcFrontend.CreateClient()
            .AsUser("bootstrap", "bootstrap", "b@example.com", OidcRolePermissionMapper.ViewerRole);
        var index = await client.GetAsync("/");
        index.EnsureSuccessStatusCode();
        var html = await index.Content.ReadAsStringAsync();

        var match = Regex.Match(html, """asp-route-id="([^"]+)"|/Home/Detail/([^?"]+)\?""");
        if (!match.Success)
        {
            throw new InvalidOperationException("No file id found on the index page. Did sample/wopi-docs lose its files?");
        }
        return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
    }

    private static string ExtractAccessToken(string html)
    {
        var match = AccessTokenInput.Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException("Hostpage did not contain an access_token input.");
        }
        return match.Groups[1].Value;
    }

    public sealed class Fixture : IDisposable
    {
        public TestAuthOidcWebAppFactory OidcFrontend { get; }
        public WopiBackendFactory WopiBackend { get; }

        public Fixture()
        {
            OidcFrontend = new TestAuthOidcWebAppFactory(
                wopiSigningSecret: SharedSigningSecret,
                wopiBackendUrl: "http://wopi-backend.test");
            WopiBackend = new WopiBackendFactory(wopiSigningSecret: SharedSigningSecret);
        }

        public void Dispose()
        {
            OidcFrontend.Dispose();
            WopiBackend.Dispose();
        }
    }
}
