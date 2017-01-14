using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WopiHost.Discovery
{
	public class FileSystemDiscoveryFileProvider : IDiscoveryFileProvider
	{
		private readonly string _filePath;

		public FileSystemDiscoveryFileProvider(string filePath)
		{
			_filePath = filePath;
		}
		public Task<XElement> GetDiscoveryXmlAsync()
		{
			return Task.FromResult(XElement.Parse(File.ReadAllText(_filePath)));
		}
	}
}