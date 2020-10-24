using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WopiHost.Discovery
{
    /// <summary>
    /// A discovery file provider that loads the discovery file from a WOPI client over HTTP.
    /// </summary>
    public class HttpDiscoveryFileProvider : IDiscoveryFileProvider
    {
        private readonly HttpClient _httpClient;
        private XElement _discoveryXml;

        /// <summary>
        /// Creates an instance of a discovery file provider that loads the discovery file from a WOPI client over HTTP.
        /// </summary>
        /// <param name="httpClient">An HTTP client with a <see cref="HttpClient.BaseAddress"/> configured to point to a WOPI client.</param>
        public HttpDiscoveryFileProvider(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<XElement> GetDiscoveryXmlAsync()
        {
            if (_discoveryXml is null)
            {
                try
                {
                    var stream = await _httpClient.GetStreamAsync("/hosting/discovery");
                    _discoveryXml = XElement.Load(stream);
                }
                catch (HttpRequestException e)
                {
                    throw new DiscoveryException($"There was a problem retrieving the discovery file. Please check availability of the WOPI Client at '{_httpClient.BaseAddress}'.", e);
                }
            }
            return _discoveryXml;
        }
    }
}
