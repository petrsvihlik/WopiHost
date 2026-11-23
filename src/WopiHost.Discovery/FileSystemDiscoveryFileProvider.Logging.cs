using Microsoft.Extensions.Logging;

namespace WopiHost.Discovery;

public partial class FileSystemDiscoveryFileProvider
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "WOPI discovery XML loaded from file {filePath}")]
    private static partial void LogDiscoveryFileLoaded(ILogger logger, string filePath);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "WOPI discovery XML failed to load from file {filePath}")]
    private static partial void LogDiscoveryFileLoadFailed(ILogger logger, Exception exception, string filePath);
}
