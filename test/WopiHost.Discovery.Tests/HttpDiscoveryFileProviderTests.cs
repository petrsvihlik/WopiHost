using System.Net;
using System.Xml.Linq;

namespace WopiHost.Discovery.Tests;

public class HttpDiscoveryFileProviderTests
{
    private static HttpClient CreateHttpClient(HttpMessageHandler handler, Uri? baseAddress = null)
        => new(handler) { BaseAddress = baseAddress ?? new Uri("http://wopi-client.example.com") };

    private static HttpResponseMessage XmlResponse(string xml)
        => new(HttpStatusCode.OK) { Content = new StringContent(xml, System.Text.Encoding.UTF8, "application/xml") };

    [Fact]
    public async Task GetDiscoveryXmlAsync_ReturnsRootElement()
    {
        const string xml = "<wopi-discovery><net-zone /></wopi-discovery>";
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(XmlResponse(xml)));
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler));

        var result = await sut.GetDiscoveryXmlAsync();

        Assert.Equal("wopi-discovery", result.Name.LocalName);
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_RequestsDiscoveryEndpoint()
    {
        const string xml = "<wopi-discovery />";
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            captured = req;
            return Task.FromResult(XmlResponse(xml));
        });
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler));

        await sut.GetDiscoveryXmlAsync();

        Assert.Equal("/hosting/discovery", captured?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_ParsesChildElements()
    {
        var xml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "OOS2016_discovery.xml"));
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(XmlResponse(xml)));
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler));

        var result = await sut.GetDiscoveryXmlAsync();

        Assert.NotEmpty(result.Elements());
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_OnHttpRequestException_ThrowsDiscoveryException()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused")));
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler));

        // Act & Assert
        await Assert.ThrowsAsync<DiscoveryException>(() => sut.GetDiscoveryXmlAsync());
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_OnHttpRequestException_PreservesInnerException()
    {
        var original = new HttpRequestException("Connection refused");
        var handler = new FakeHttpMessageHandler(_ => Task.FromException<HttpResponseMessage>(original));
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler));

        var ex = await Assert.ThrowsAsync<DiscoveryException>(() => sut.GetDiscoveryXmlAsync());

        Assert.Same(original, ex.InnerException);
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_OnHttpRequestException_MessageContainsBaseAddress()
    {
        var baseAddress = new Uri("http://wopi-client.example.com");
        var handler = new FakeHttpMessageHandler(_ =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused")));
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler, baseAddress));

        var ex = await Assert.ThrowsAsync<DiscoveryException>(() => sut.GetDiscoveryXmlAsync());

        Assert.Contains(baseAddress.ToString(), ex.Message);
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request);
    }
}
