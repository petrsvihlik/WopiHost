using Microsoft.Extensions.Logging;

namespace WopiHost.FileSystemProvider;

public partial class InMemoryFileIds
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Scanned {total} items")]
    private static partial void LogScannedItems(ILogger logger, int total);
}
