using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace WopiHost.Discovery;

/// <summary>
/// Loads the WOPI discovery XML file from the local file system.
/// </summary>
/// <remarks>
/// Initializes the provider using a local file-system path.
/// </remarks>
/// <param name="filePath">Path to a WOPI XML discovery file.</param>
/// <param name="logger">Logger.</param>
public partial class FileSystemDiscoveryFileProvider(string filePath, ILogger<FileSystemDiscoveryFileProvider> logger) : IDiscoveryFileProvider
{
    private readonly string _filePath = filePath;
    private readonly ILogger<FileSystemDiscoveryFileProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public Task<XElement> GetDiscoveryXmlAsync()
    {
        try
        {
            var xml = XElement.Parse(File.ReadAllText(_filePath));
            LogDiscoveryFileLoaded(_logger, _filePath);
            return Task.FromResult(xml);
        }
        catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
        {
            LogDiscoveryFileLoadFailed(_logger, ex, _filePath);
            throw;
        }
    }
}
