using System.Net;
using WopiHost.IntegrationTests.Fixtures;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// HTTP-level coverage of the ecosystem endpoints in
/// <see cref="WopiHost.Core.Endpoints.EcosystemEndpoints"/>: CheckEcosystem and
/// the root container pointer.
/// </summary>
[Collection("ReadOnlyEndpoints")]
public sealed class EcosystemEndpointTests(ReadOnlyEndpointsFixture fixture)
{
    private readonly ReadOnlyEndpointsFixture _fixture = fixture;

    [Fact]
    public async Task CheckEcosystem_Returns_200()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/ecosystem?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"SupportsContainers\"", body);
    }

    [Fact]
    public async Task GetRootContainer_Returns_RootContainerInfo()
    {
        var token = await _fixture.MintContainerTokenAsync(_fixture.RootContainerId);
        using var client = _fixture.WopiBackend.CreateClient();

        var resp = await client.GetAsync($"/wopi/ecosystem/root_container_pointer?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"ContainerPointer\"", body);
    }
}
