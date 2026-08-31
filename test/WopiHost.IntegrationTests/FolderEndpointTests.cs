using System.Net;
using WopiHost.Abstractions;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// HTTP-level coverage of the read-only folder endpoints in
/// <see cref="WopiHost.Core.Endpoints.FolderEndpoints"/>. Folders are containers
/// permission-wise but expose only the legacy folder shape (ChildFiles only — no
/// ChildContainers).
/// </summary>
[Collection("ReadOnlyEndpoints")]
public sealed class FolderEndpointTests(ReadOnlyEndpointsFixture fixture)
{
    private readonly ReadOnlyEndpointsFixture _fixture = fixture;

    [Fact]
    public async Task CheckFolderInfo_Returns_200()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/folders/{_fixture.RootContainerId}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task CheckFolderInfo_Returns_404_ForMissingFolder()
    {
        var missing = new string('e', 64);
        var token = await _fixture.MintContainerTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/folders/{missing}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task FolderChildren_Returns_OnlyChildFiles()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/folders/{_fixture.RootContainerId}/children?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"ChildFiles\"", body);
        // Folder surface intentionally omits ChildContainers per the legacy controller shape.
        Assert.DoesNotContain("\"ChildContainers\"", body);
    }

    [Fact]
    public async Task FolderChildren_Returns_404_ForMissingFolder()
    {
        var missing = new string('f', 64);
        var token = await _fixture.MintContainerTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/folders/{missing}/children?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task CheckFolderInfo_FiresOnCheckFolderInfoHook_AndReturnsItsResult()
    {
        // FolderEndpoints.CheckFolderInfo is the only caller of the OnCheckFolderInfoAsync
        // extension point; a rewritten FolderName in the response proves the hook both fired
        // and had its result honoured. Uses a dedicated backend so the shared read-only
        // fixture keeps the default extensions.
        const string signingSecret = "folder-hook-tests-shared-key-32b!";
        using var backend = new WopiBackendFactory(signingSecret, configureServices: services =>
        {
            services.RemoveAll<IWopiHostExtensions>();
            services.AddSingleton<IWopiHostExtensions>(new FolderNameRewritingExtensions());
        });

        string rootId;
        using (var scope = backend.Services.CreateScope())
        {
            rootId = scope.ServiceProvider.GetRequiredService<IWopiStorageProvider>().RootContainer.Identifier;
        }
        var token = await FixtureTokens.MintContainerTokenAsync(
            backend, new FixtureUser("folder-hook-user", "Folder Hook User", "folder-hook@example.com"), rootId);
        using var client = backend.CreateClient();

        var resp = await client.GetAsync($"/wopi/folders/{rootId}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains($"\"{RewrittenFolderName}\"", body);
    }

    private const string RewrittenFolderName = "folder-name-rewritten-by-hook";

    private sealed class FolderNameRewritingExtensions : WopiHostExtensions
    {
        public override Task<WopiCheckFolderInfo> OnCheckFolderInfoAsync(
            WopiCheckFolderInfoContext context, CancellationToken cancellationToken = default)
        {
            context.CheckFolderInfo.FolderName = RewrittenFolderName;
            return Task.FromResult(context.CheckFolderInfo);
        }
    }
}
