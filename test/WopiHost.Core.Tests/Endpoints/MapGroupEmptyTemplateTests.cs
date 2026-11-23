using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace WopiHost.Core.Tests.Endpoints;

/// <summary>
/// Pins the routing-template quirk that <see cref="WopiHost.Core.Endpoints.EcosystemEndpoints"/>
/// and <see cref="WopiHost.Core.Endpoints.BootstrapEndpoints"/> work around:
/// <c>MapGroup("/x")</c> plus <c>MapGet("")</c> normalises to <c>/x/</c> (with a trailing slash),
/// not <c>/x</c>.
/// </summary>
/// <remarks>
/// <para>
/// The named-route lookup <see cref="WopiHost.Core.Infrastructure.WopiRouteNames.CheckEcosystem"/>
/// and the WOPI bootstrap URL need the exact-prefix form, so both classes register their
/// bare-group endpoint on the receiver instead of a nested <c>MapGroup</c>.
/// </para>
/// <para>
/// The test fails once ASP.NET Core stops appending the trailing slash; both classes can then
/// switch to <c>MapGroup</c>.
/// </para>
/// </remarks>
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

        await app.StartAsync(TestContext.Current.CancellationToken);

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
