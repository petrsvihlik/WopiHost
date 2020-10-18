using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WopiHost.Discovery
{
    public class HttpDiscoveryFileProvider : IDiscoveryFileProvider
    {
        private XElement _discoveryXml;
        private readonly string _wopiClientUrl;

        public HttpDiscoveryFileProvider(string wopiClientUrl)
        {
            _wopiClientUrl = wopiClientUrl;
        }

        public async Task<XElement> GetDiscoveryXmlAsync()
        {
            if (_discoveryXml is null)
            {
                try
                {
                    Stream stream;
                    using (HttpClient client = new HttpClient())
                    {
                        stream = await client.GetStreamAsync(_wopiClientUrl + "/hosting/discovery");
                    }
                    _discoveryXml = XElement.Load(stream);
                }
                catch (HttpRequestException e)
                {
                    throw new DiscoveryException($"There was a problem retrieving the discovery file. Please check availability of the WOPI Client at '{_wopiClientUrl}'.", e);
                }
            }
            return _discoveryXml;
        }
    }
}
