using System.Net;
using System.Text.Json;
using WopiHost.Abstractions;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// HTTP-level coverage of every mutating endpoint in
/// <see cref="WopiHost.Core.Endpoints.ContainerMutatingEndpoints"/>:
/// CreateChildContainer, CreateChildFile, DeleteContainer, RenameContainer. Shares the
/// <see cref="MutatingEndpointsFixture"/> with the file-side mutating tests via
/// <c>[Collection("MutatingEndpoints")]</c>.
/// </summary>
[Collection("MutatingEndpoints")]
public sealed class ContainerMutatingEndpointTests(MutatingEndpointsFixture fixture)
{
    private readonly MutatingEndpointsFixture _fixture = fixture;

    // ---- GetShareUrl -----------------------------------------------------

    [Theory]
    [InlineData("ReadOnly")]
    [InlineData("ReadWrite")]
    public async Task GetShareUrl_SupportedUrlType_Returns200WithAbsoluteShareUrl(string urlType)
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "GET_SHARE_URL");
        req.Headers.Add("X-WOPI-UrlType", urlType);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var shareUrl = doc.RootElement.GetProperty("ShareUrl").GetString();
        Assert.True(Uri.IsWellFormedUriString(shareUrl, UriKind.Absolute));
    }

    [Fact]
    public async Task GetShareUrl_UnsupportedUrlType_Returns501()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "GET_SHARE_URL");
        req.Headers.Add("X-WOPI-UrlType", "NotASupportedType");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotImplemented, resp.StatusCode);
    }

    [Fact]
    public async Task GetShareUrl_MissingUrlTypeHeader_Returns501()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "GET_SHARE_URL");
        // No X-WOPI-UrlType header → treated as unsupported.
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotImplemented, resp.StatusCode);
    }

    // ---- CreateChildContainer --------------------------------------------

    [Fact]
    public async Task CreateChildContainer_WithSuggestedTarget_Returns_200()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "CREATE_CHILD_CONTAINER");
        req.Headers.Add("X-WOPI-SuggestedTarget", $"folder-{Guid.NewGuid():N}");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"ContainerPointer\"", body);
    }

    [Fact]
    public async Task CreateChildContainer_WithRelativeTarget_OnExisting_Returns_409()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        // First create a folder with a known name…
        var name = $"folder-{Guid.NewGuid():N}";
        var first = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");
        first.Headers.Add("X-WOPI-Override", "CREATE_CHILD_CONTAINER");
        first.Headers.Add("X-WOPI-RelativeTarget", name);
        var firstResp = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.OK, firstResp.StatusCode);

        // …then ask for it again under specific (relative) mode → 409.
        var second = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");
        second.Headers.Add("X-WOPI-Override", "CREATE_CHILD_CONTAINER");
        second.Headers.Add("X-WOPI-RelativeTarget", name);
        var secondResp = await client.SendAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, secondResp.StatusCode);
        Assert.True(secondResp.Headers.Contains("X-WOPI-ValidRelativeTarget"));
    }

    [Fact]
    public async Task CreateChildContainer_BothHeaders_Returns_501()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "CREATE_CHILD_CONTAINER");
        req.Headers.Add("X-WOPI-SuggestedTarget", "a");
        req.Headers.Add("X-WOPI-RelativeTarget", "b");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotImplemented, resp.StatusCode);
    }

    [Fact]
    public async Task CreateChildContainer_Suggested_InvalidName_SanitisesAndReturns_200()
    {
        // Spec: suggested-mode "must never result in a 400 Bad Request or 409 Conflict. Rather,
        // the host must modify the proposed name as needed to create a new container that is
        // legally named."
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "CREATE_CHILD_CONTAINER");
        req.Headers.Add("X-WOPI-SuggestedTarget", $"bad/name-{Guid.NewGuid():N}");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task CreateChildContainer_Relative_InvalidName_Returns_400_WithInvalidContainerNameError()
    {
        // Spec: specific-mode 400 "must include an X-WOPI-InvalidContainerNameError header that
        // describes why the file name was invalid."
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "CREATE_CHILD_CONTAINER");
        req.Headers.Add("X-WOPI-RelativeTarget", "bad/name");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.True(resp.Headers.Contains("X-WOPI-InvalidContainerNameError"));
    }

    [Fact]
    public async Task CreateChildContainer_ResponseUrl_CarriesFreshContainerScopedToken()
    {
        // Spec: response Url property must include an access token. To honour "preventing
        // token trading", it must be bound to the NEW child's resource id, not reuse the
        // inbound parent token.
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var name = $"folder-{Guid.NewGuid():N}";
        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "CREATE_CHILD_CONTAINER");
        req.Headers.Add("X-WOPI-RelativeTarget", name);
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<JsonElement>(body);
        var url = payload.GetProperty("ContainerPointer").GetProperty("Url").GetString()!;
        var uri = new Uri(url);
        var childToken = System.Web.HttpUtility.ParseQueryString(uri.Query)["access_token"]!;
        Assert.NotEqual(token, childToken);

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(childToken);
        var childId = uri.AbsolutePath.Split('/').Last();
        Assert.Equal(childId, jwt.Claims.First(c => c.Type == WopiClaimTypes.ResourceId).Value);
        Assert.NotEqual(_fixture.RootContainerId, jwt.Claims.First(c => c.Type == WopiClaimTypes.ResourceId).Value);
    }

    // ---- CreateChildFile -------------------------------------------------

    [Fact]
    public async Task CreateChildFile_WithSuggestedTarget_Returns_200()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("file-body"u8.ToArray()),
        };
        req.Headers.Add("X-WOPI-Override", "CREATE_CHILD_FILE");
        req.Headers.Add("X-WOPI-SuggestedTarget", ".txt");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task CreateChildFile_WithRelativeTarget_OnExisting_Returns_409()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        // First create a file with a known name…
        var name = $"file-{Guid.NewGuid():N}.txt";
        using var first = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("file-body"u8.ToArray()),
        };
        first.Headers.Add("X-WOPI-Override", "CREATE_CHILD_FILE");
        first.Headers.Add("X-WOPI-RelativeTarget", name);
        var firstResp = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.OK, firstResp.StatusCode);

        // …then ask for it again under specific (relative) mode with no overwrite → 409 + the
        // X-WOPI-ValidRelativeTarget header the negotiator surfaces on a name collision.
        using var second = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent("file-body"u8.ToArray()),
        };
        second.Headers.Add("X-WOPI-Override", "CREATE_CHILD_FILE");
        second.Headers.Add("X-WOPI-RelativeTarget", name);
        var secondResp = await client.SendAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, secondResp.StatusCode);
        Assert.True(secondResp.Headers.Contains("X-WOPI-ValidRelativeTarget"));
    }

    [Fact]
    public async Task CreateChildFile_BothHeaders_Returns_501()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}")
        {
            Content = new ByteArrayContent([]),
        };
        req.Headers.Add("X-WOPI-Override", "CREATE_CHILD_FILE");
        req.Headers.Add("X-WOPI-SuggestedTarget", "a.txt");
        req.Headers.Add("X-WOPI-RelativeTarget", "b.txt");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotImplemented, resp.StatusCode);
    }

    // ---- DeleteContainer -------------------------------------------------

    [Fact]
    public async Task DeleteContainer_OnEmpty_Returns_200()
    {
        // Create a fresh empty subfolder, then delete it.
        using var client = _fixture.WopiBackend.CreateClient();
        var childId = await _fixture.CreateTempContainerAsync();
        var childToken = await _fixture.MintContainerTokenAsync(childId);

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{childId}?access_token={Uri.EscapeDataString(childToken)}");
        req.Headers.Add("X-WOPI-Override", "DELETE_CONTAINER");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteContainer_NonEmpty_Returns_409()
    {
        // Create a subfolder, drop a file into it, then try to delete the folder.
        // FileSystemProvider's DeleteWopiContainer raises InvalidOperationException on a
        // non-empty directory, which the handler maps to 409.
        var childId = await _fixture.CreateTempContainerAsync();
        var fileName = $"child-{Guid.NewGuid():N}.bin";
        using (var scope = _fixture.WopiBackend.Services.CreateScope())
        {
            var writable = scope.ServiceProvider.GetRequiredService<IWopiWritableStorageProvider>();
            await writable.CreateWopiChildFile(childId, fileName);
        }
        var token = await _fixture.MintContainerTokenAsync(childId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{childId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "DELETE_CONTAINER");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteContainer_NotFound_Returns_404()
    {
        var missing = new string('d', 64);
        var token = await _fixture.MintContainerTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{missing}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "DELETE_CONTAINER");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ---- RenameContainer -------------------------------------------------

    [Fact]
    public async Task RenameContainer_Returns_200_WithNewName()
    {
        // Spec: response JSON has a single required Name property that MUST be the new name,
        // not the OLD container snapshot's Name (which would be out-of-sync with what the
        // storage provider actually persisted).
        var childId = await _fixture.CreateTempContainerAsync();
        var token = await _fixture.MintContainerTokenAsync(childId);
        using var client = _fixture.WopiBackend.CreateClient();
        var newName = $"renamed-{Guid.NewGuid():N}";

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{childId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "RENAME_CONTAINER");
        req.Headers.Add("X-WOPI-RequestedName", newName);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(newName, payload.GetProperty("Name").GetString());
    }

    [Fact]
    public async Task RenameContainer_NotFound_Returns_404()
    {
        var missing = new string('e', 64);
        var token = await _fixture.MintContainerTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/containers/{missing}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "RENAME_CONTAINER");
        req.Headers.Add("X-WOPI-RequestedName", "x");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
