using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace WopiHost.Discovery;

/// <summary>
/// A discovery file provider that loads the discovery file from a WOPI client over HTTP.
/// </summary>
/// <remarks>
/// Creates an instance of a discovery file provider that loads the discovery file from a WOPI client over HTTP.
/// </remarks>
/// <param name="httpClient">An HTTP client with a <see cref="HttpClient.BaseAddress"/> configured to point to a WOPI client.</param>
/// <param name="logger">Logger.</param>
public partial class HttpDiscoveryFileProvider(HttpClient httpClient, ILogger<HttpDiscoveryFileProvider> logger) : IDiscoveryFileProvider
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly ILogger<HttpDiscoveryFileProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<XElement> GetDiscoveryXmlAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var stream = await _httpClient.GetStreamAsync(new Uri("/hosting/discovery", UriKind.Relative)).ConfigureAwait(false);
            var xml = XElement.Load(stream);
            LogDiscoveryFetched(_logger, _httpClient.BaseAddress, sw.ElapsedMilliseconds);
            return xml;
        }
        catch (HttpRequestException e)
        {
            LogDiscoveryFetchFailed(_logger, e, _httpClient.BaseAddress);
            throw new DiscoveryException($"There was a problem retrieving the discovery file. Please check availability of the WOPI Client at '{_httpClient.BaseAddress}'.", e);
        }
        // HttpClient.Timeout surfaces as TaskCanceledException whose InnerException is a
        // TimeoutException (.NET 6+ convention). Distinguish that from a real CancellationToken
        // cancellation triggered by the caller — those have InnerException == null and must be
        // allowed to propagate so the caller observes the cancellation it asked for.
        catch (TaskCanceledException e) when (e.InnerException is TimeoutException)
        {
            LogDiscoveryFetchFailed(_logger, e, _httpClient.BaseAddress);
            throw new DiscoveryException($"The WOPI Client at '{_httpClient.BaseAddress}' did not respond within the configured HttpClient.Timeout.", e);
        }
        // Malformed XML from the WOPI client (or a server returning 200 with a non-XML body —
        // e.g. an error page) surfaces as XmlException. Wrap it so callers see a single
        // discovery-failure exception type.
        catch (XmlException e)
        {
            LogDiscoveryFetchFailed(_logger, e, _httpClient.BaseAddress);
            throw new DiscoveryException($"The WOPI Client at '{_httpClient.BaseAddress}' returned a discovery payload that could not be parsed as XML.", e);
        }
    }
}
