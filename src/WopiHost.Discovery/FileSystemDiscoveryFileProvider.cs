using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WopiHost.Discovery;

/// <summary>
/// Loads the WOPI discovery XML file from the local file system.
/// </summary>
/// <remarks>
/// Initializes the provider using a local file-system path.
/// </remarks>
/// <param name="filePath">Path to a WOPI XML discovery file.</param>
/// <param name="logger">Optional logger. When omitted, a <see cref="NullLogger{T}"/> is used so the package
/// stays usable without DI.</param>
public partial class FileSystemDiscoveryFileProvider(string filePath, ILogger<FileSystemDiscoveryFileProvider>? logger = null) : IDiscoveryFileProvider
{
    private readonly string _filePath = filePath;
    private readonly ILogger<FileSystemDiscoveryFileProvider> _logger = logger ?? NullLogger<FileSystemDiscoveryFileProvider>.Instance;

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
