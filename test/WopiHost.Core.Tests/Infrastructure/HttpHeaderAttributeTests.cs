using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Routing;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

public class HttpHeaderAttributeTests
{
    [Fact]
    public void Accept_HeaderMatches_ReturnsTrue()
    {
        var headerName = "X-Test-Header";
        var headerValue = "TestValue";
        var attribute = new HttpHeaderAttribute(headerName, headerValue);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[headerName] = headerValue;

        var routeContext = new RouteContext(httpContext);
        var actionConstraintContext = new ActionConstraintContext
        {
            RouteContext = routeContext
        };

        var result = attribute.Accept(actionConstraintContext);

        Assert.True(result);
    }

    [Fact]
    public void Accept_HeaderDoesNotMatch_ReturnsFalse()
    {
        var headerName = "X-Test-Header";
        var headerValue = "TestValue";
        var attribute = new HttpHeaderAttribute(headerName, headerValue);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[headerName] = "DifferentValue";

        var routeContext = new RouteContext(httpContext);
        var actionConstraintContext = new ActionConstraintContext
        {
            RouteContext = routeContext
        };

        var result = attribute.Accept(actionConstraintContext);

        Assert.False(result);
    }

    [Fact]
    public void Accept_HeaderNotPresent_ReturnsFalse()
    {
        var headerName = "X-Test-Header";
        var headerValue = "TestValue";
        var attribute = new HttpHeaderAttribute(headerName, headerValue);

        var httpContext = new DefaultHttpContext();

        var routeContext = new RouteContext(httpContext);
        var actionConstraintContext = new ActionConstraintContext
        {
            RouteContext = routeContext
        };

        var result = attribute.Accept(actionConstraintContext);

        Assert.False(result);
    }
}
