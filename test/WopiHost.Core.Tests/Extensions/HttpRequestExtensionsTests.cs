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
        // Arrange
        var request = CreateMockRequest(
            scheme: "https",
            host: "example.com",
            pathBase: "/api",
            path: "/wopi/files",
            queryString: "?access_token=123"
        );

        // Act
        var result = request.GetProxyAwareRequestUrl();

        // Assert
        Assert.Equal("https://example.com/api/wopi/files?access_token=123", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithProxyHeaders_UsesProxyValues()
    {
        // Arrange
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

        // Act
        var result = request.GetProxyAwareRequestUrl();

        // Assert
        Assert.Equal("https://proxy.example.com/external/wopi/files?access_token=123", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithPartialProxyHeaders_UsesProxyAndOriginalValues()
    {
        // Arrange
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

        // Act
        var result = request.GetProxyAwareRequestUrl();

        // Assert
        Assert.Equal("https://proxy.example.com/internal/wopi/files?access_token=123", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithEmptyPathBase_HandlesCorrectly()
    {
        // Arrange
        var request = CreateMockRequest(
            scheme: "https",
            host: "example.com",
            pathBase: "",
            path: "/wopi/files",
            queryString: "?access_token=123"
        );

        // Act
        var result = request.GetProxyAwareRequestUrl();

        // Assert
        Assert.Equal("https://example.com/wopi/files?access_token=123", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithoutQueryString_HandlesCorrectly()
    {
        // Arrange
        var request = CreateMockRequest(
            scheme: "https",
            host: "example.com",
            pathBase: "/api",
            path: "/wopi/files",
            queryString: ""
        );

        // Act
        var result = request.GetProxyAwareRequestUrl();

        // Assert
        Assert.Equal("https://example.com/api/wopi/files", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithRootPath_HandlesCorrectly()
    {
        // Arrange
        var request = CreateMockRequest(
            scheme: "https",
            host: "example.com",
            pathBase: "",
            path: "/",
            queryString: ""
        );

        // Act
        var result = request.GetProxyAwareRequestUrl();

        // Assert
        Assert.Equal("https://example.com/", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithComplexPath_HandlesCorrectly()
    {
        // Arrange
        var request = CreateMockRequest(
            scheme: "https",
            host: "example.com",
            pathBase: "/wopi-host",
            path: "/wopi/files/123/contents",
            queryString: "?access_token=abc&version=1"
        );

        // Act
        var result = request.GetProxyAwareRequestUrl();

        // Assert
        Assert.Equal("https://example.com/wopi-host/wopi/files/123/contents?access_token=abc&version=1", result);
    }

    [Fact]
    public void GetProxyAwareRequestUrl_WithNullValues_HandlesGracefully()
    {
        // Arrange
        var request = CreateMockRequest(
            scheme: "https",
            host: "example.com",
            pathBase: null,
            path: null,
            queryString: null
        );

        // Act
        var result = request.GetProxyAwareRequestUrl();

        // Assert
        Assert.Equal("https://example.com", result);
    }

    [Theory]
    [InlineData("X-Forwarded-Proto", "https")]
    [InlineData("X-Forwarded-Host", "proxy.example.com")]
    [InlineData("X-Forwarded-PathBase", "/external")]
    public void GetProxyAwareRequestUrl_WithSingleProxyHeader_UsesProxyValueForThatHeader(string headerName, string headerValue)
    {
        // Arrange
        var request = CreateMockRequest(
            scheme: "http",
            host: "internal.server",
            pathBase: "/internal",
            path: "/wopi/files",
            queryString: "?test=1"
        );

        var headers = new HeaderDictionary();
        headers[headerName] = headerValue;
        Mock.Get(request).Setup(r => r.Headers).Returns(headers);

        // Act
        var result = request.GetProxyAwareRequestUrl();

        // Assert
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
} 