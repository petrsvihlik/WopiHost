using System.Net;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// HTTP-level coverage of the read-only file endpoints in
/// <see cref="WopiHost.Core.Endpoints.FileEndpoints"/>: CheckFileInfo, GetFile (contents),
/// ecosystem_pointer, and ancestry.
/// </summary>
[Collection("ReadOnlyEndpoints")]
public sealed class FileEndpointTests(ReadOnlyEndpointsFixture fixture)
{
    private readonly ReadOnlyEndpointsFixture _fixture = fixture;

    [Fact]
    public async Task CheckFileInfo_Returns_200_WithJson()
    {
        var token = await _fixture.MintFileTokenAsync(_fixture.FirstFileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/files/{_fixture.FirstFileId}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"BaseFileName\"", body);
    }

    [Fact]
    public async Task CheckFileInfo_Returns_404_ForMissingFile()
    {
        var missing = new string('0', 64); // SHA-256-shaped but non-existent
        var token = await _fixture.MintFileTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/files/{missing}?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetFile_Returns_FileBody()
    {
        var token = await _fixture.MintFileTokenAsync(_fixture.FirstFileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/files/{_fixture.FirstFileId}/contents?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task GetFile_Returns_412_WhenSizeExceedsMax()
    {
        var token = await _fixture.MintFileTokenAsync(_fixture.FirstFileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Get, $"/wopi/files/{_fixture.FirstFileId}/contents?access_token={Uri.EscapeDataString(token)}");
        req.Headers.Add("X-WOPI-MaxExpectedSize", "1");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.PreconditionFailed, resp.StatusCode);
    }

    [Fact]
    public async Task FileEcosystemPointer_Returns_UrlResponse()
    {
        var token = await _fixture.MintFileTokenAsync(_fixture.FirstFileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/files/{_fixture.FirstFileId}/ecosystem_pointer?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"Url\"", body);
    }

    [Fact]
    public async Task FileEcosystemPointer_Returns_404_ForMissingFile()
    {
        var missing = new string('d', 64);
        var token = await _fixture.MintFileTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/files/{missing}/ecosystem_pointer?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task FileAncestry_Returns_JsonArray()
    {
        var token = await _fixture.MintFileTokenAsync(_fixture.FirstFileId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/files/{_fixture.FirstFileId}/ancestry?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"AncestorsWithRootFirst\"", body);
    }

    [Fact]
    public async Task FileAncestry_Returns_404_ForMissingFile()
    {
        var missing = new string('c', 64);
        var token = await _fixture.MintFileTokenAsync(missing);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/files/{missing}/ancestry?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
