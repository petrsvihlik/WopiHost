using System.Xml.Linq;

namespace WopiHost.Discovery;

/// <summary>
/// Loads the WOPI discovery XML file from the local file system.
/// </summary>
/// <remarks>
/// Initializes the provider using a local file-system path.
/// </remarks>
/// <param name="filePath">Path to a WOPI XML discovery file.</param>
public class FileSystemDiscoveryFileProvider(string filePath) : IDiscoveryFileProvider
	{
		private readonly string _filePath = filePath;

    /// <inheritdoc/>
    public Task<XElement> GetDiscoveryXmlAsync() => Task.FromResult(XElement.Parse(File.ReadAllText(_filePath)));
}