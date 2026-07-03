using Microsoft.Extensions.Logging;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="FileIdMapSynchronizer"/>.
/// </summary>
internal sealed partial class FileIdMapSynchronizer
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Watching {rootPath} for file changes")]
    private static partial void LogWatcherStarted(ILogger logger, string rootPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not watch {rootPath} for file changes; id-map convergence falls back to lazy registration and reconciliation sweeps")]
    private static partial void LogWatcherStartFailed(ILogger logger, string rootPath, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "File watcher on {rootPath} reported an error; scheduling a reconciliation sweep")]
    private static partial void LogWatcherError(ILogger logger, string rootPath, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Watcher event {changeType} for {path} could not be applied to the id map")]
    private static partial void LogWatcherEventFailed(ILogger logger, WatcherChangeTypes changeType, string path, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reconciled the id map with {rootPath}; {registered} entries registered")]
    private static partial void LogReconciled(ILogger logger, string rootPath, int registered);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reconciliation sweep of {rootPath} failed")]
    private static partial void LogReconcileFailed(ILogger logger, string rootPath, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolved unmapped id {fileId} to {path} by reconciliation sweep")]
    private static partial void LogIdResolvedByReconcile(ILogger logger, string fileId, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Id {fileId} did not resolve to any path under {rootPath}")]
    private static partial void LogIdMiss(ILogger logger, string fileId, string rootPath);
}
