using Microsoft.Extensions.Logging;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="BlobIdMap"/>.
/// </summary>
public sealed partial class BlobIdMap
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Scanned {count} blob entries")]
    private static partial void LogScannedEntries(ILogger logger, int count);
}
