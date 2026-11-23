using Microsoft.Extensions.Logging;

namespace WopiHost.AzureStorageProvider;

/// <content>Contains the source-generated logging methods.</content>
public sealed partial class BlobIdMap
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Scanned {count} blob entries")]
    private static partial void LogScannedEntries(ILogger logger, int count);
}
