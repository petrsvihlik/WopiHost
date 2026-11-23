using Microsoft.Extensions.Logging;

namespace WopiHost.FileSystemProvider;

/// <content>Contains the source-generated logging methods.</content>
public partial class InMemoryFileIds
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Scanned {total} items")]
    private static partial void LogScannedItems(ILogger logger, int total);
}
