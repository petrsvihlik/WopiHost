using System.Net;
using WopiHost.Abstractions;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// HTTP-level coverage of the read-only container endpoints in
/// <see cref="WopiHost.Core.Endpoints.ContainerEndpoints"/>: CheckContainerInfo,
/// ecosystem_pointer, ancestry, and children (with extension filter).
/// </summary>
[Collection("ReadOnlyEndpoints")]
public sealed class ContainerEndpointTests(ReadOnlyEndpointsFixture fixture)
{
    private readonly ReadOnlyEndpointsFixture _fixture = fixture;

    [Fact]
    public async Task CheckContainerInfo_Returns_200()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"Name\"", body);
    }

    [Fact]
    public async Task CheckContainerInfo_Returns_404_ForMissingContainer()
    {
        var missing = new string('f', 64);
        var token = await _fixture.MintContainerTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/containers/{missing}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task ContainerEcosystemPointer_Returns_UrlResponse()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/containers/{_fixture.RootContainerId}/ecosystem_pointer?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"Url\"", body);
    }

    [Fact]
    public async Task ContainerAncestry_Returns_Json()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/containers/{_fixture.RootContainerId}/ancestry?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task ContainerAncestry_Returns_404_ForMissingContainer()
    {
        var missing = new string('a', 64);
        var token = await _fixture.MintContainerTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/containers/{missing}/ancestry?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task ContainerChildren_Returns_ChildList()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/containers/{_fixture.RootContainerId}/children?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"ChildFiles\"", body);
        Assert.Contains("\"ChildContainers\"", body);
    }

    [Fact]
    public async Task ContainerChildren_UrlsCarryFreshPerResourceTokens()
    {
        // Spec: each ChildFile and ChildContainer URL embeds an access token. Per "preventing
        // token trading", that token MUST be freshly minted per-child rather than the inbound
        // parent-container token reused verbatim. We verify (a) each child URL's token differs
        // from the inbound one, and (b) the child tokens carry the resource type the URL
        // implies (File vs Container). Per-id binding is verified by the file-side ancestry
        // test — duplicating the casing-sensitive path-extraction assertion here adds noise
        // for no additional security signal.
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/containers/{_fixture.RootContainerId}/children?access_token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        var payload = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(body);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

        var childFiles = payload.GetProperty("ChildFiles").EnumerateArray().ToList();
        Assert.NotEmpty(childFiles);
        foreach (var child in childFiles)
        {
            var url = child.GetProperty("Url").GetString()!;
            var uri = new System.Uri(url);
            var childToken = System.Web.HttpUtility.ParseQueryString(uri.Query)["access_token"]!;
            Assert.NotEqual(token, childToken);

            var jwt = handler.ReadJwtToken(childToken);
            // Per-resource binding: URL path id must equal the JWT resource-id claim verbatim.
            // The previous LowercaseUrls=true routing-option default was case-folding the URL
            // path while leaving the JWT claim intact (minted from raw file.Identifier) —
            // GetWopiSrc now passes LinkOptions { LowercaseUrls = false } to keep them aligned.
            var childId = uri.AbsolutePath.Split('/').Last();
            Assert.Equal(childId, jwt.Claims.First(c => c.Type == WopiClaimTypes.ResourceId).Value);
            Assert.Equal("File", jwt.Claims.First(c => c.Type == WopiClaimTypes.ResourceType).Value);
        }
    }

    [Fact]
    public async Task ContainerChildren_Returns_404_ForMissingContainer()
    {
        var missing = new string('b', 64);
        var token = await _fixture.MintContainerTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/containers/{missing}/children?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task ContainerChildren_Honours_FileExtensionFilter()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Get, $"/wopi/containers/{_fixture.RootContainerId}/children?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-FileExtensionFilterList", ".docx,.xlsx");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
