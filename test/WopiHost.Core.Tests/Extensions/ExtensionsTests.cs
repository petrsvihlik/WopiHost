using Moq;
using WopiHost.Core.Extensions;

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
    public void GetWopiSrc_UnknownResourceType_Throws()
    {
        var url = new Moq.Mock<Microsoft.AspNetCore.Mvc.IUrlHelper>();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            url.Object.GetWopiSrc((WopiHost.Abstractions.WopiResourceType)999, "id"));
    }

    [Theory]
    [InlineData(WopiHost.Abstractions.WopiResourceType.File, WopiHost.Core.Infrastructure.WopiRouteNames.CheckFileInfo)]
    [InlineData(WopiHost.Abstractions.WopiResourceType.Container, WopiHost.Core.Infrastructure.WopiRouteNames.CheckContainerInfo)]
    public void GetWopiSrc_EnumOverload_RoutesToMatchingControllerAction(
        WopiHost.Abstractions.WopiResourceType resourceType, string expectedRouteName)
    {
        // The enum overload of GetWopiSrc maps File → CheckFileInfo, Container → CheckContainerInfo.
        // Both arms (and the throw arm in the test above) round out coverage on the resource-type
        // dispatch. The mock asserts both that the call went out and which route name was used.
        string? observedRouteName = null;
        var url = new Moq.Mock<Microsoft.AspNetCore.Mvc.IUrlHelper>();
        url.Setup(u => u.ActionContext).Returns(new Microsoft.AspNetCore.Mvc.ActionContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { Request = { Scheme = "https", Host = new("h") } }
        });
        url.Setup(u => u.RouteUrl(It.IsAny<Microsoft.AspNetCore.Mvc.Routing.UrlRouteContext>()))
            .Callback<Microsoft.AspNetCore.Mvc.Routing.UrlRouteContext>(ctx => observedRouteName = ctx.RouteName)
            .Returns("/wopi/x");

        var src = url.Object.GetWopiSrc(resourceType, "id", "access");

        Assert.Equal(expectedRouteName, observedRouteName);
        Assert.NotNull(src);
    }

    [Fact]
    public void GetWopiSrc_WhenRouteUrlReturnsNull_FallsBackGracefully()
    {
        // The proxy-aware URL helper returns null when no route matches. The caller (GetWopiSrc)
        // must surface that via an InvalidOperationException so the bug isn't silently masked
        // by an empty Uri. This exercises the null-route-path branch in ProxyAwareRouteUrl.
        var url = new Moq.Mock<Microsoft.AspNetCore.Mvc.IUrlHelper>();
        url.Setup(u => u.ActionContext).Returns(new Microsoft.AspNetCore.Mvc.ActionContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { Request = { Scheme = "https", Host = new("h") } }
        });
        url.Setup(u => u.RouteUrl(It.IsAny<Microsoft.AspNetCore.Mvc.Routing.UrlRouteContext>()))
            .Returns((string?)null);

        Assert.Throws<InvalidOperationException>(() =>
            url.Object.GetWopiSrc(WopiHost.Abstractions.WopiResourceType.File, "id"));
    }
}
