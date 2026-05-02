using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using WopiHost.Core.Extensions;

namespace WopiHost.Core.Tests.Extensions;

public class HttpRequestExtensionsTests
{
    [Fact]
    public void GetProxyAwareRequestUrl_WithoutProxyHeaders_ReturnsStandardUrl()
    {
        var request = CreateMockRequest(
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
        var request = CreateMockRequest(
            scheme: "http",
            host: "internal.server",
            pathBase: "/internal",
            path: "/wopi/files",
            queryString: "?access_token=123"
        );

        AddProxyHeaders(request, 
            forwardedProto: "https", 
            forwardedHost: "proxy.example.com", 
            forwardedPathBase: "/external");

        var result = request.GetProxyAwareRequestUrl();

        Assert.Equal("https://proxy.example.com/external/wopi/files?access_token=123", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithPartialProxyHeaders_UsesProxyAndOriginalValues()
    {
        var request = CreateMockRequest(
            scheme: "http",
            host: "internal.server",
            pathBase: "/internal",
            path: "/wopi/files",
            queryString: "?access_token=123"
        );

        // Only set proto and host headers, not pathBase
        AddProxyHeaders(request, 
            forwardedProto: "https", 
            forwardedHost: "proxy.example.com");

        var result = request.GetProxyAwareRequestUrl();

        Assert.Equal("https://proxy.example.com/internal/wopi/files?access_token=123", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithEmptyPathBase_HandlesCorrectly()
    {
        var request = CreateMockRequest(
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
        var request = CreateMockRequest(
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
        var request = CreateMockRequest(
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
        var request = CreateMockRequest(
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
        var request = CreateMockRequest(
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
    [InlineData("X-Forwarded-Proto", "https")]
    [InlineData("X-Forwarded-Host", "proxy.example.com")]
    [InlineData("X-Forwarded-PathBase", "/external")]
    public void GetProxyAwareRequestUrl_WithSingleProxyHeader_UsesProxyValueForThatHeader(string headerName, string headerValue)
    {
        var request = CreateMockRequest(
            scheme: "http",
            host: "internal.server",
            pathBase: "/internal",
            path: "/wopi/files",
            queryString: "?test=1"
        );

        var headers = new HeaderDictionary
        {
            [headerName] = headerValue
        };
        Mock.Get(request).Setup(r => r.Headers).Returns(headers);

        var result = request.GetProxyAwareRequestUrl();

        switch (headerName)
        {
            case "X-Forwarded-Proto":
                Assert.StartsWith("https://", result);
                break;
            case "X-Forwarded-Host":
                Assert.Contains("proxy.example.com", result);
                break;
            case "X-Forwarded-PathBase":
                Assert.Contains("/external/wopi/files", result);
                break;
        }
    }

    private static HttpRequest CreateMockRequest(string scheme, string host, string? pathBase, string? path, string? queryString)
    {
        var request = new Mock<HttpRequest>();
        var headers = new HeaderDictionary();
        
        request.Setup(r => r.Scheme).Returns(scheme);
        request.Setup(r => r.Host).Returns(new HostString(host));
        request.Setup(r => r.PathBase).Returns(new PathString(pathBase));
        request.Setup(r => r.Path).Returns(new PathString(path));
        request.Setup(r => r.QueryString).Returns(new QueryString(queryString));
        request.Setup(r => r.Headers).Returns(headers);

        return request.Object;
    }

    private static void AddProxyHeaders(HttpRequest request, string? forwardedProto = null,
        string? forwardedHost = null, string? forwardedPathBase = null)
    {
        var headers = request.Headers;

        if (forwardedProto != null)
            headers["X-Forwarded-Proto"] = forwardedProto;

        if (forwardedHost != null)
            headers["X-Forwarded-Host"] = forwardedHost;

        if (forwardedPathBase != null)
            headers["X-Forwarded-PathBase"] = forwardedPathBase;
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