using System.Diagnostics;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WopiHost.Discovery;

/// <summary>
/// A discovery file provider that loads the discovery file from a WOPI client over HTTP.
/// </summary>
/// <remarks>
/// Creates an instance of a discovery file provider that loads the discovery file from a WOPI client over HTTP.
/// </remarks>
/// <param name="httpClient">An HTTP client with a <see cref="HttpClient.BaseAddress"/> configured to point to a WOPI client.</param>
/// <param name="logger">Optional logger. When omitted, a <see cref="NullLogger{T}"/> is used so the package
/// stays usable without DI.</param>
public partial class HttpDiscoveryFileProvider(HttpClient httpClient, ILogger<HttpDiscoveryFileProvider>? logger = null) : IDiscoveryFileProvider
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<HttpDiscoveryFileProvider> _logger = logger ?? NullLogger<HttpDiscoveryFileProvider>.Instance;

    /// <inheritdoc />
    public async Task<XElement> GetDiscoveryXmlAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var stream = await _httpClient.GetStreamAsync(new Uri("/hosting/discovery", UriKind.Relative));
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
