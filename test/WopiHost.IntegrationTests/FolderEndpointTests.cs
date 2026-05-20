using System.Net;
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
}
