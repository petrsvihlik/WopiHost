using Microsoft.Extensions.Logging;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="InMemoryFileIds"/>.
/// </summary>
public partial class InMemoryFileIds
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Scanned {total} items")]
    private static partial void LogScannedItems(ILogger logger, int total);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolved unmapped id {fileId} to {path} by scan")]
    private static partial void LogIdResolvedByScan(ILogger logger, string fileId, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Id {fileId} did not resolve to any path in the tree")]
    private static partial void LogIdScanMiss(ILogger logger, string fileId);
}
