using System.Threading.Tasks;
using System.Xml.Linq;

namespace WopiHost.Discovery
{
	public interface IDiscoveryFileProvider
	{
		Task<XElement> GetDiscoveryXmlAsync();
	}
}