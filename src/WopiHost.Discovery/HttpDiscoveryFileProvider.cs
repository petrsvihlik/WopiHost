using System.Diagnostics;
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
    }
}
