using System.Net;
using Microsoft.Extensions.Logging.Abstractions;

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
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler), NullLogger<HttpDiscoveryFileProvider>.Instance);

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
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler), NullLogger<HttpDiscoveryFileProvider>.Instance);

        await sut.GetDiscoveryXmlAsync();

        Assert.Equal("/hosting/discovery", captured?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_ParsesChildElements()
    {
        var xml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "OOS2016_discovery.xml"));
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(XmlResponse(xml)));
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler), NullLogger<HttpDiscoveryFileProvider>.Instance);

        var result = await sut.GetDiscoveryXmlAsync();

        Assert.NotEmpty(result.Elements());
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_OnHttpRequestException_ThrowsDiscoveryException()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused")));
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler), NullLogger<HttpDiscoveryFileProvider>.Instance);

        await Assert.ThrowsAsync<DiscoveryException>(() => sut.GetDiscoveryXmlAsync());
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_OnHttpRequestException_PreservesInnerException()
    {
        var original = new HttpRequestException("Connection refused");
        var handler = new FakeHttpMessageHandler(_ => Task.FromException<HttpResponseMessage>(original));
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler), NullLogger<HttpDiscoveryFileProvider>.Instance);

        var ex = await Assert.ThrowsAsync<DiscoveryException>(() => sut.GetDiscoveryXmlAsync());

        Assert.Same(original, ex.InnerException);
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_OnHttpRequestException_MessageContainsBaseAddress()
    {
        var baseAddress = new Uri("http://wopi-client.example.com");
        var handler = new FakeHttpMessageHandler(_ =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused")));
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler, baseAddress), NullLogger<HttpDiscoveryFileProvider>.Instance);

        var ex = await Assert.ThrowsAsync<DiscoveryException>(() => sut.GetDiscoveryXmlAsync());

        Assert.Contains(baseAddress.ToString(), ex.Message);
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_On404Response_ThrowsDiscoveryException()
    {
        // Real WOPI clients that don't host the discovery endpoint reply with 404 (or, depending
        // on the deployment, with an HTML error page that's parsed elsewhere). HttpClient
        // surfaces non-2xx as HttpRequestException via GetStreamAsync — the provider must wrap
        // it into DiscoveryException so callers don't have to handle the underlying HTTP type.
        var handler = new FakeHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("not found"),
            }));
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler), NullLogger<HttpDiscoveryFileProvider>.Instance);

        var ex = await Assert.ThrowsAsync<DiscoveryException>(() => sut.GetDiscoveryXmlAsync());

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_OnMalformedXml_ThrowsDiscoveryException()
    {
        // A WOPI client that returns 200 with a non-XML body (HTML error pages, JSON, raw text)
        // would otherwise leak System.Xml.XmlException. The provider wraps it into
        // DiscoveryException so callers see a single failure type for "discovery didn't work."
        const string nonXmlBody = "<html><body>This is not the discovery XML you're looking for.</body><unclosed>";
        var handler = new FakeHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(nonXmlBody, System.Text.Encoding.UTF8, "application/xml"),
            }));
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler), NullLogger<HttpDiscoveryFileProvider>.Instance);

        var ex = await Assert.ThrowsAsync<DiscoveryException>(() => sut.GetDiscoveryXmlAsync());

        Assert.IsType<System.Xml.XmlException>(ex.InnerException);
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_OnEmptyResponse_ThrowsDiscoveryException()
    {
        // Sibling to malformed-XML: an empty body is the zero-byte degenerate case (e.g. server
        // returned 200 with Content-Length: 0). XElement.Load on an empty stream throws
        // XmlException — must wrap the same way.
        var handler = new FakeHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/xml"),
            }));
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler), NullLogger<HttpDiscoveryFileProvider>.Instance);

        await Assert.ThrowsAsync<DiscoveryException>(() => sut.GetDiscoveryXmlAsync());
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_OnHttpTimeout_ThrowsDiscoveryException()
    {
        // HttpClient.Timeout fires a TaskCanceledException whose InnerException is a
        // TimeoutException — that's the .NET 6+ convention for "the operation timed out
        // server-side" as distinct from "the caller cancelled." The provider catches that
        // specific shape and wraps; user-cancellation paths (separate test) propagate unchanged.
        var timeout = new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.",
            innerException: new TimeoutException("A connection could not be established within the configured timeout."));
        var handler = new FakeHttpMessageHandler(_ => Task.FromException<HttpResponseMessage>(timeout));
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler), NullLogger<HttpDiscoveryFileProvider>.Instance);

        var ex = await Assert.ThrowsAsync<DiscoveryException>(() => sut.GetDiscoveryXmlAsync());

        Assert.IsType<TaskCanceledException>(ex.InnerException);
        Assert.IsType<TimeoutException>(ex.InnerException!.InnerException);
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_OnPureCancellation_DoesNotWrap()
    {
        // Negative case for the timeout-wrap above: a plain TaskCanceledException (no inner
        // TimeoutException) is what callers see when they cancel via CancellationToken. That
        // signal must propagate so the caller observes the cancellation it asked for — wrapping
        // would mask the cooperative-cancellation contract.
        var cancellation = new TaskCanceledException("operation cancelled by caller");
        var handler = new FakeHttpMessageHandler(_ => Task.FromException<HttpResponseMessage>(cancellation));
        var sut = new HttpDiscoveryFileProvider(CreateHttpClient(handler), NullLogger<HttpDiscoveryFileProvider>.Instance);

        await Assert.ThrowsAsync<TaskCanceledException>(() => sut.GetDiscoveryXmlAsync());
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request);
    }
}
