using System.Net;
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
