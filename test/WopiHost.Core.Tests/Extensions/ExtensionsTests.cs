using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Extensions;

public class ExtensionsTests
{
    [Fact]
    public void ToUnixTimestamp_DateTime_ReturnsUnixSeconds()
    {
        long ticks = 1664582400;
        DateTime dateTime = new(2022, 10, 1);

        long actual = dateTime.ToUnixTimestamp();

        Assert.Equal(ticks, actual);
    }

    [Fact]
    public async Task ReadBytesAsync_CopiesStreamContents()
    {
        var content = "hello"u8.ToArray();
        using var input = new MemoryStream(content);

        var result = await input.ReadBytesAsync();

        Assert.Equal(content, result);
    }

    [Fact]
    public void ToNullableInt_ValidInteger_ReturnsParsedValue()
    {
        Assert.Equal(42, "42".ToNullableInt());
    }

    [Fact]
    public void ToNullableInt_InvalidInteger_ReturnsNull()
    {
        Assert.Null("not-a-number".ToNullableInt());
    }

    [Fact]
    public async Task GetWopiSrc_UnknownResourceType_Throws()
    {
        await using var app = await BuildAppAsync(_ => { });

        var ctx = new DefaultHttpContext { RequestServices = app.Services };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ctx.GetWopiSrc((WopiResourceType)999, "id"));
    }

    [Theory]
    [InlineData(WopiResourceType.File, WopiRouteNames.CheckFileInfo, "/files/{id}")]
    [InlineData(WopiResourceType.Container, WopiRouteNames.CheckContainerInfo, "/containers/{id}")]
    public async Task GetWopiSrc_EnumOverload_RoutesToMatchingNamedRoute(
        WopiResourceType resourceType, string expectedRouteName, string routeTemplate)
    {
        // The enum overload of GetWopiSrc maps File → CheckFileInfo, Container → CheckContainerInfo.
        // Both arms (plus the throw arm in the test above) round out coverage on the resource-type
        // dispatch.
        await using var app = await BuildAppAsync(endpoints =>
            endpoints.MapGet(routeTemplate, () => "ok").WithName(expectedRouteName));

        var ctx = new DefaultHttpContext
        {
            RequestServices = app.Services,
            Request = { Scheme = "https", Host = new("h") },
        };

        var src = ctx.GetWopiSrc(resourceType, "abc", "access");

        Assert.NotNull(src);
        Assert.Contains("abc", src.ToString(), StringComparison.Ordinal);
        Assert.Contains("access_token=access", src.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetWopiSrc_WhenRouteUnknown_Throws()
    {
        // If the named route isn't registered, the helper must surface an explicit error rather
        // than silently masking with an empty Uri.
        await using var app = await BuildAppAsync(_ => { });

        var ctx = new DefaultHttpContext
        {
            RequestServices = app.Services,
            Request = { Scheme = "https", Host = new("h") },
        };

        Assert.Throws<InvalidOperationException>(() =>
            ctx.GetWopiSrc(WopiResourceType.File, "id"));
    }

    private static async Task<WebApplication> BuildAppAsync(Action<IEndpointRouteBuilder> mapEndpoints)
    {
        var builder = WebApplication.CreateEmptyBuilder(new());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        var app = builder.Build();
        app.UseRouting();
        mapEndpoints(app);
        await app.StartAsync();
        return app;
    }
}
