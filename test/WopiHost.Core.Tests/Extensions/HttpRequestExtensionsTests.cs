using Microsoft.AspNetCore.Http;
using WopiHost.Core.Extensions;

namespace WopiHost.Core.Tests.Extensions;

public class HttpRequestExtensionsTests
{
    [Fact]
    public void GetProxyAwareRequestUrl_WithoutProxyHeaders_ReturnsStandardUrl()
    {
        var request = CreateRequest(
            scheme: "https",
            host: "example.com",
            pathBase: "/api",
            path: "/wopi/files",
            queryString: "?access_token=123"
        );

        var result = request.GetProxyAwareRequestUrl();

        Assert.Equal("https://example.com/api/wopi/files?access_token=123", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithProxyHeaders_UsesProxyValues()
    {
        var request = CreateRequest(
            scheme: "http",
            host: "internal.server",
            pathBase: "/internal",
            path: "/wopi/files",
            queryString: "?access_token=123"
        );

        request.Headers["X-Forwarded-Proto"] = "https";
        request.Headers["X-Forwarded-Host"] = "proxy.example.com";
        request.Headers["X-Forwarded-PathBase"] = "/external";

        var result = request.GetProxyAwareRequestUrl();

        Assert.Equal("https://proxy.example.com/external/wopi/files?access_token=123", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithPartialProxyHeaders_UsesProxyAndOriginalValues()
    {
        var request = CreateRequest(
            scheme: "http",
            host: "internal.server",
            pathBase: "/internal",
            path: "/wopi/files",
            queryString: "?access_token=123"
        );

        // Only set proto and host headers, not pathBase
        request.Headers["X-Forwarded-Proto"] = "https";
        request.Headers["X-Forwarded-Host"] = "proxy.example.com";

        var result = request.GetProxyAwareRequestUrl();

        Assert.Equal("https://proxy.example.com/internal/wopi/files?access_token=123", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithEmptyPathBase_HandlesCorrectly()
    {
        var request = CreateRequest(
            scheme: "https",
            host: "example.com",
            pathBase: "",
            path: "/wopi/files",
            queryString: "?access_token=123"
        );

        var result = request.GetProxyAwareRequestUrl();

        Assert.Equal("https://example.com/wopi/files?access_token=123", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithoutQueryString_HandlesCorrectly()
    {
        var request = CreateRequest(
            scheme: "https",
            host: "example.com",
            pathBase: "/api",
            path: "/wopi/files",
            queryString: ""
        );

        var result = request.GetProxyAwareRequestUrl();

        Assert.Equal("https://example.com/api/wopi/files", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithRootPath_HandlesCorrectly()
    {
        var request = CreateRequest(
            scheme: "https",
            host: "example.com",
            pathBase: "",
            path: "/",
            queryString: ""
        );

        var result = request.GetProxyAwareRequestUrl();

        Assert.Equal("https://example.com/", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithComplexPath_HandlesCorrectly()
    {
        var request = CreateRequest(
            scheme: "https",
            host: "example.com",
            pathBase: "/wopi-host",
            path: "/wopi/files/123/contents",
            queryString: "?access_token=abc&version=1"
        );

        var result = request.GetProxyAwareRequestUrl();

        Assert.Equal("https://example.com/wopi-host/wopi/files/123/contents?access_token=abc&version=1", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithNullValues_HandlesGracefully()
    {
        var request = CreateRequest(
            scheme: "https",
            host: "example.com",
            pathBase: null,
            path: null,
            queryString: null
        );

        var result = request.GetProxyAwareRequestUrl();

        Assert.Equal("https://example.com", result);
    }

    [Theory]
    [InlineData("X-Forwarded-Proto", "https", "https://internal.server/internal/wopi/files?test=1")]
    [InlineData("X-Forwarded-Host", "proxy.example.com", "http://proxy.example.com/internal/wopi/files?test=1")]
    [InlineData("X-Forwarded-PathBase", "/external", "http://internal.server/external/wopi/files?test=1")]
    public void GetProxyAwareRequestUrl_WithSingleProxyHeader_UsesProxyValueForThatHeader(string headerName, string headerValue, string expectedUrl)
    {
        var request = CreateRequest(
            scheme: "http",
            host: "internal.server",
            pathBase: "/internal",
            path: "/wopi/files",
            queryString: "?test=1"
        );

        request.Headers[headerName] = headerValue;

        var result = request.GetProxyAwareRequestUrl();

        Assert.Equal(expectedUrl, result);
    }

    private static HttpRequest CreateRequest(string scheme, string host, string? pathBase, string? path, string? queryString)
    {
        var request = new DefaultHttpContext().Request;

        request.Scheme = scheme;
        request.Host = new HostString(host);
        request.PathBase = new PathString(pathBase);
        request.Path = new PathString(path);
        request.QueryString = new QueryString(queryString);

        return request;
    }

    [Fact]
    public void GetAccessToken_FromQueryString_ReturnsToken()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.QueryString = new QueryString("?access_token=from-query");

        Assert.Equal("from-query", ctx.Request.GetAccessToken());
    }

    [Fact]
    public async Task GetAccessToken_FromFormBody_ReturnsToken()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.ContentType = "application/x-www-form-urlencoded";
        var formContent = "access_token=from-form";
        var bodyBytes = System.Text.Encoding.UTF8.GetBytes(formContent);
        ctx.Request.Body = new MemoryStream(bodyBytes);
        ctx.Request.ContentLength = bodyBytes.Length;
        // Pre-warm the form so HasFormContentType + Form.TryGetValue work.
        await ctx.Request.ReadFormAsync();

        Assert.Equal("from-form", ctx.Request.GetAccessToken());
    }

    [Fact]
    public void GetAccessToken_FromBearerHeader_ReturnsToken()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer from-header";

        Assert.Equal("from-header", ctx.Request.GetAccessToken());
    }

    [Fact]
    public void GetAccessToken_FromNonBearerAuthorizationHeader_ReturnsEmpty()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Basic abc123";

        Assert.Equal(string.Empty, ctx.Request.GetAccessToken());
    }

    [Fact]
    public void GetAccessToken_NoTokenAnywhere_ReturnsEmpty()
    {
        var ctx = new DefaultHttpContext();

        Assert.Equal(string.Empty, ctx.Request.GetAccessToken());
    }
}