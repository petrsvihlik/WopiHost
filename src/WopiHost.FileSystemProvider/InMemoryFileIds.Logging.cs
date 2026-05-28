using Microsoft.Extensions.Logging;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="InMemoryFileIds"/>.
/// </summary>
public partial class InMemoryFileIds
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Scanned {total} items")]
    private static partial void LogScannedItems(ILogger logger, int total);
}
