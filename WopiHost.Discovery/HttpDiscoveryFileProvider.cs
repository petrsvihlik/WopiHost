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
			if (_discoveryXml == null)
			{
				HttpClient client = new HttpClient();
				Stream stream = await client.GetStreamAsync(_wopiClientUrl + "/hosting/discovery");
				_discoveryXml = XElement.Load(stream);
			}
			return _discoveryXml;
		}
	}
}
