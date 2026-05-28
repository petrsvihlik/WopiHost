using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace WopiHost.Core.Tests.Endpoints;

/// <summary>
/// Locks in the long-standing routing-template quirk cited by
/// <see cref="WopiHost.Core.Endpoints.EcosystemEndpoints"/> and
/// <see cref="WopiHost.Core.Endpoints.BootstrapEndpoints"/>:
/// <c>MapGroup("/x") + MapGet("")</c> normalises to <c>/x/</c> (with a trailing slash), NOT
/// <c>/x</c>. The named-route lookup <c>WopiRouteNames.CheckEcosystem</c> and the WOPI
/// bootstrap URL must be the exact-prefix form, so both files register their bare-group
/// endpoint via the receiver instead of a nested <c>MapGroup</c>.
/// This test fails the moment ASP.NET Core normalises the trailing slash away — at which
/// point those two files can switch to <c>MapGroup</c> for symmetry.
/// </summary>
public sealed class MapGroupEmptyTemplateTests
{
    [Fact]
    public async Task MapGroup_Plus_Empty_Child_Pattern_Still_Produces_Trailing_Slash()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        await using var app = builder.Build();

        var group = app.MapGroup("/ecosystem");
        group.MapGet("", () => "root");
        group.MapGet("/root_container_pointer", () => "rcp");

        await app.StartAsync();

        var templates = app.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText)
            .ToArray();

        // Regression assertion — when this flips to "/ecosystem" the comment in
        // EcosystemEndpoints/BootstrapEndpoints can be deleted and both files can swap to
        // MapGroup for symmetry with the rest of the codebase.
        Assert.Contains("/ecosystem/", templates);
        Assert.DoesNotContain("/ecosystem", templates);
        Assert.Contains("/ecosystem/root_container_pointer", templates);
    }
}
