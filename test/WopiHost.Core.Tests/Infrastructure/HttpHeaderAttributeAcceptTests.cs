using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Routing;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

public class HttpHeaderAttributeAcceptTests
{
    [Fact]
    public void Accept_NullContext_ReturnsFalse()
    {
        var attr = new HttpHeaderAttribute("X-Test", "yes");

        Assert.False(attr.Accept(context: null!));
    }

    [Fact]
    public void Order_IsZero()
    {
        var attr = new HttpHeaderAttribute("X-Test", "yes");

        Assert.Equal(0, attr.Order);
    }

    [Fact]
    public void Accept_HeaderMatchesAllowedValue_ReturnsTrue()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Test"] = "yes";
        var ctx = new ActionConstraintContext
        {
            RouteContext = new RouteContext(http),
        };
        var attr = new HttpHeaderAttribute("X-Test", "yes");

        Assert.True(attr.Accept(ctx));
    }
}
