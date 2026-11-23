using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace WopiHost.Discovery;

/// <summary>
/// Loads the WOPI discovery XML file from the local file system.
/// </summary>
/// <param name="filePath">The path to a WOPI discovery XML file.</param>
/// <param name="logger">The logger.</param>
/// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
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
