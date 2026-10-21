using Microsoft.Extensions.Logging;

namespace WopiHost.CellBridge;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="CellBridgeProcessor"/>.
/// </summary>
public sealed partial class CellBridgeProcessor
{
    [LoggerMessage(Level = LogLevel.Information, Message = "cellbridge document {resourceId} created for file {fileId}")]
    private static partial void LogDocumentImported(ILogger logger, string fileId, Guid resourceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "cellbridge content version {contentVersion} flushed to storage for file {fileId}")]
    private static partial void LogContentFlushed(ILogger logger, string fileId, uint contentVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unrecognized COBALT body framing for file {fileId}; first bytes {preview}")]
    private static partial void LogUnknownFraming(ILogger logger, string fileId, string preview);

    [LoggerMessage(Level = LogLevel.Warning, Message = "cellbridge rejected a cell write for file {fileId}: {lockError}")]
    private static partial void LogLockError(ILogger logger, string fileId, string lockError);

    [LoggerMessage(Level = LogLevel.Error, Message = "cellbridge document import failed for file {fileId}")]
    private static partial void LogImportFailed(ILogger logger, Exception exception, string fileId);
}
