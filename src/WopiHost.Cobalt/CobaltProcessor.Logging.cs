using Microsoft.Extensions.Logging;

namespace WopiHost.Cobalt;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="CobaltProcessor"/>.
/// </summary>
public sealed partial class CobaltProcessor
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Co-auth session created for file {fileId}")]
    private static partial void LogSessionCreated(ILogger logger, string fileId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Co-auth session evicted (idle) for file {fileId}")]
    private static partial void LogSessionEvicted(ILogger logger, string fileId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Co-auth changes flushed to disk for file {fileId}")]
    private static partial void LogContentFlushed(ILogger logger, string fileId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Co-auth session creation failed for file {fileId}")]
    private static partial void LogSessionCreateFailed(ILogger logger, Exception exception, string fileId);
}
