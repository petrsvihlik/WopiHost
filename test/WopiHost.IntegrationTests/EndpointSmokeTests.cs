using System.Net;
using WopiHost.Abstractions;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// HTTP-level smoke coverage of every endpoint registered by <c>MapWopiEndpoints</c>.
/// Boots the WOPI backend sample via <see cref="WopiBackendFactory"/>, mints tokens through
/// the backend's own <see cref="IWopiAccessTokenService"/>, and asserts response shape /
/// status code for the happy path (and a couple of 404 / 412 negatives where they hit
/// distinct code paths).
/// </summary>
/// <remarks>
/// Phase 5 of the #430 migration — the controller test suites (which produced the original
/// per-action coverage) were deleted in Phase 4. These tests intentionally stay at the HTTP
/// boundary so they survive future endpoint refactors that don't change the wire contract.
/// </remarks>
public sealed class EndpointSmokeTests(EndpointSmokeTests.Fixture fixture) : IClassFixture<EndpointSmokeTests.Fixture>
{
    private const string SharedSigningSecret = "endpoint-smoke-shared-key-32bytes!";

    private readonly Fixture _fixture = fixture;

    private async Task<string> MintFileTokenAsync(string fileId, WopiFilePermissions permissions = WopiFilePermissions.UserCanWrite)
    {
        using var scope = _fixture.WopiBackend.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IWopiAccessTokenService>();
        var token = await tokens.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "smoke-user",
            UserDisplayName = "Smoke User",
            UserEmail = "smoke@example.com",
            ResourceId = fileId,
            ResourceType = WopiResourceType.File,
            FilePermissions = permissions,
        });
        return token.Token;
    }

    private async Task<string> MintContainerTokenAsync(string containerId)
    {
        using var scope = _fixture.WopiBackend.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IWopiAccessTokenService>();
        var token = await tokens.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "smoke-user",
            UserDisplayName = "Smoke User",
            UserEmail = "smoke@example.com",
            ResourceId = containerId,
            ResourceType = WopiResourceType.Container,
            ContainerPermissions = WopiContainerPermissions.UserCanCreateChildContainer | WopiContainerPermissions.UserCanCreateChildFile | WopiContainerPermissions.UserCanDelete | WopiContainerPermissions.UserCanRename,
        });
        return token.Token;
    }

    private string FirstFileId() => _fixture.FirstFileId;
    private string RootContainerId() => _fixture.RootContainerId;

    // ---- File GETs ---------------------------------------------------------

    [Fact]
    public async Task CheckFileInfo_Returns_200_WithJson()
    {
        var token = await MintFileTokenAsync(FirstFileId());
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/files/{FirstFileId()}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"BaseFileName\"", body);
    }

    [Fact]
    public async Task CheckFileInfo_Returns_404_ForMissingFile()
    {
        var missing = new string('0', 64); // SHA-256-shaped but non-existent
        var token = await MintFileTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/files/{missing}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetFile_Returns_FileBody()
    {
        var token = await MintFileTokenAsync(FirstFileId());
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/files/{FirstFileId()}/contents?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task GetFile_Returns_412_WhenSizeExceedsMax()
    {
        var token = await MintFileTokenAsync(FirstFileId());
        using var client = _fixture.WopiBackend.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, $"/wopi/files/{FirstFileId()}/contents?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-MaxExpectedSize", "1");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.PreconditionFailed, resp.StatusCode);
    }

    [Fact]
    public async Task FileEcosystemPointer_Returns_UrlResponse()
    {
        var token = await MintFileTokenAsync(FirstFileId());
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/files/{FirstFileId()}/ecosystem_pointer?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"Url\"", body);
    }

    [Fact]
    public async Task FileAncestry_Returns_JsonArray()
    {
        var token = await MintFileTokenAsync(FirstFileId());
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/files/{FirstFileId()}/ancestry?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"AncestorsWithRootFirst\"", body);
    }

    // ---- Container GETs ----------------------------------------------------

    [Fact]
    public async Task CheckContainerInfo_Returns_200()
    {
        var token = await MintContainerTokenAsync(RootContainerId());
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/containers/{RootContainerId()}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"Name\"", body);
    }

    [Fact]
    public async Task CheckContainerInfo_Returns_404_ForMissingContainer()
    {
        var missing = new string('f', 64);
        var token = await MintContainerTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/containers/{missing}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task ContainerEcosystemPointer_Returns_UrlResponse()
    {
        var token = await MintContainerTokenAsync(RootContainerId());
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/containers/{RootContainerId()}/ecosystem_pointer?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"Url\"", body);
    }

    [Fact]
    public async Task ContainerAncestry_Returns_Json()
    {
        var token = await MintContainerTokenAsync(RootContainerId());
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/containers/{RootContainerId()}/ancestry?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task ContainerChildren_Returns_ChildList()
    {
        var token = await MintContainerTokenAsync(RootContainerId());
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/containers/{RootContainerId()}/children?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"ChildFiles\"", body);
        Assert.Contains("\"ChildContainers\"", body);
    }

    [Fact]
    public async Task ContainerAncestry_Returns_404_ForMissingContainer()
    {
        var missing = new string('a', 64);
        var token = await MintContainerTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/containers/{missing}/ancestry?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task ContainerChildren_Returns_404_ForMissingContainer()
    {
        var missing = new string('b', 64);
        var token = await MintContainerTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/containers/{missing}/children?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task FileAncestry_Returns_404_ForMissingFile()
    {
        var missing = new string('c', 64);
        var token = await MintFileTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/files/{missing}/ancestry?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task FileEcosystemPointer_Returns_404_ForMissingFile()
    {
        var missing = new string('d', 64);
        var token = await MintFileTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/files/{missing}/ecosystem_pointer?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task CheckFolderInfo_Returns_404_ForMissingFolder()
    {
        var missing = new string('e', 64);
        var token = await MintContainerTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/folders/{missing}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task FolderChildren_Returns_404_ForMissingFolder()
    {
        var missing = new string('f', 64);
        var token = await MintContainerTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/folders/{missing}/children?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task ContainerChildren_Honours_FileExtensionFilter()
    {
        var token = await MintContainerTokenAsync(RootContainerId());
        using var client = _fixture.WopiBackend.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, $"/wopi/containers/{RootContainerId()}/children?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-FileExtensionFilterList", ".docx,.xlsx");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ---- Folder GETs -------------------------------------------------------

    [Fact]
    public async Task CheckFolderInfo_Returns_200()
    {
        var token = await MintContainerTokenAsync(RootContainerId());
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/folders/{RootContainerId()}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task FolderChildren_Returns_OnlyChildFiles()
    {
        var token = await MintContainerTokenAsync(RootContainerId());
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/folders/{RootContainerId()}/children?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"ChildFiles\"", body);
        // Folder surface intentionally omits ChildContainers per the legacy controller shape.
        Assert.DoesNotContain("\"ChildContainers\"", body);
    }

    // ---- Ecosystem GETs ----------------------------------------------------

    [Fact]
    public async Task CheckEcosystem_Returns_200()
    {
        var token = await MintContainerTokenAsync(RootContainerId());
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/ecosystem?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"SupportsContainers\"", body);
    }

    [Fact]
    public async Task GetRootContainer_Returns_RootContainerInfo()
    {
        var token = await MintContainerTokenAsync(RootContainerId());
        using var client = _fixture.WopiBackend.CreateClient();
        var resp = await client.GetAsync($"/wopi/ecosystem/root_container_pointer?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"ContainerPointer\"", body);
    }

    // ---- File mutating: Lock/Unlock cycle ---------------------------------

    [Fact]
    public async Task Lock_Unlock_Cycle_Succeeds()
    {
        var fileId = FirstFileId();
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        var url = $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}";

        // Lock
        var lockReq = new HttpRequestMessage(HttpMethod.Post, url);
        lockReq.Headers.Add("X-WOPI-Override", "LOCK");
        lockReq.Headers.Add("X-WOPI-Lock", "lock-1");
        var lockResp = await client.SendAsync(lockReq);
        Assert.Equal(HttpStatusCode.OK, lockResp.StatusCode);

        // GetLock
        var getLockReq = new HttpRequestMessage(HttpMethod.Post, url);
        getLockReq.Headers.Add("X-WOPI-Override", "GET_LOCK");
        var getLockResp = await client.SendAsync(getLockReq);
        Assert.Equal(HttpStatusCode.OK, getLockResp.StatusCode);
        Assert.Equal("lock-1", getLockResp.Headers.GetValues("X-WOPI-Lock").FirstOrDefault());

        // RefreshLock
        var refreshReq = new HttpRequestMessage(HttpMethod.Post, url);
        refreshReq.Headers.Add("X-WOPI-Override", "REFRESH_LOCK");
        refreshReq.Headers.Add("X-WOPI-Lock", "lock-1");
        var refreshResp = await client.SendAsync(refreshReq);
        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);

        // Unlock
        var unlockReq = new HttpRequestMessage(HttpMethod.Post, url);
        unlockReq.Headers.Add("X-WOPI-Override", "UNLOCK");
        unlockReq.Headers.Add("X-WOPI-Lock", "lock-1");
        var unlockResp = await client.SendAsync(unlockReq);
        Assert.Equal(HttpStatusCode.OK, unlockResp.StatusCode);
    }

    [Fact]
    public async Task Lock_Mismatch_Returns_409_WithLockHeader()
    {
        var fileId = FirstFileId();
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        var url = $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}";

        // Acquire initial lock
        var lockReq = new HttpRequestMessage(HttpMethod.Post, url);
        lockReq.Headers.Add("X-WOPI-Override", "LOCK");
        lockReq.Headers.Add("X-WOPI-Lock", "lock-initial");
        var lockResp = await client.SendAsync(lockReq);
        Assert.Equal(HttpStatusCode.OK, lockResp.StatusCode);

        try
        {
            // Try to acquire a different lock — should 409 with the original lock in header
            var conflict = new HttpRequestMessage(HttpMethod.Post, url);
            conflict.Headers.Add("X-WOPI-Override", "LOCK");
            conflict.Headers.Add("X-WOPI-Lock", "lock-other");
            var conflictResp = await client.SendAsync(conflict);
            Assert.Equal(HttpStatusCode.Conflict, conflictResp.StatusCode);
            Assert.Equal("lock-initial", conflictResp.Headers.GetValues("X-WOPI-Lock").FirstOrDefault());
        }
        finally
        {
            // Clean up — release the initial lock
            var cleanup = new HttpRequestMessage(HttpMethod.Post, url);
            cleanup.Headers.Add("X-WOPI-Override", "UNLOCK");
            cleanup.Headers.Add("X-WOPI-Lock", "lock-initial");
            await client.SendAsync(cleanup);
        }
    }

    [Fact]
    public async Task Unknown_Override_Returns_404()
    {
        var fileId = FirstFileId();
        var token = await MintFileTokenAsync(fileId);
        using var client = _fixture.WopiBackend.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, $"/wopi/files/{fileId}?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-Override", "BOGUS_OPERATION");
        var resp = await client.SendAsync(req);

        // WopiOverrideMatcherPolicy invalidates all override-bearing candidates; framework
        // falls through to 404 (documented behaviour — see policy XML remarks).
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    public sealed class Fixture : IDisposable
    {
        public WopiBackendFactory WopiBackend { get; }
        public string FirstFileId { get; }
        public string RootContainerId { get; }

        public Fixture()
        {
            WopiBackend = new WopiBackendFactory(SharedSigningSecret);

            using var scope = WopiBackend.Services.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IWopiStorageProvider>();
            RootContainerId = storage.RootContainer.Identifier;

            // Pull the first file the FileSystem provider enumerates from sample/wopi-docs.
            FirstFileId = ResolveFirstFileId(storage).GetAwaiter().GetResult();
        }

        private static async Task<string> ResolveFirstFileId(IWopiStorageProvider storage)
        {
            await foreach (var f in storage.GetWopiFiles(storage.RootContainer.Identifier))
            {
                return f.Identifier;
            }
            throw new InvalidOperationException("sample/wopi-docs is empty — at least one file is required for the smoke fixture.");
        }

        public void Dispose() => WopiBackend.Dispose();
    }
}
