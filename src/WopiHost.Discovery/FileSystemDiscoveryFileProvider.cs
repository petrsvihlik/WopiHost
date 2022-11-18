using System.Xml.Linq;

namespace WopiHost.Discovery;

/// <summary>
/// Loads the WOPI discovery XML file from the local file system.
/// </summary>
public class FileSystemDiscoveryFileProvider : IDiscoveryFileProvider
	{
		private readonly string _filePath;

		/// <summary>
		/// Initializes the provider using a local file-system path.
		/// </summary>
		/// <param name="filePath">Path to a WOPI XML discovery file.</param>
		public FileSystemDiscoveryFileProvider(string filePath)
		{
			_filePath = filePath;
		}

		/// <inheritdoc/>
		public Task<XElement> GetDiscoveryXmlAsync()
		{
			return Task.FromResult(XElement.Parse(File.ReadAllText(_filePath)));
		}
	}