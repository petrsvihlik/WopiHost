using Microsoft.Extensions.Logging;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="WopiFileSystemProvider"/>.
/// </summary>
public partial class WopiFileSystemProvider
{
    [LoggerMessage(Level = LogLevel.Information, Message = "WopiFileSystemProvider initialized at root {rootPath}")]
    private static partial void LogProviderInitialized(ILogger logger, string rootPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created file {fileId} at {path}")]
    private static partial void LogFileCreated(ILogger logger, string fileId, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted file {fileId} at {path}")]
    private static partial void LogFileDeleted(ILogger logger, string fileId, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Renamed file {fileId}: {oldPath} → {newPath}")]
    private static partial void LogFileRenamed(ILogger logger, string fileId, string oldPath, string newPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created folder {folderId} at {path}")]
    private static partial void LogFolderCreated(ILogger logger, string folderId, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted folder {folderId} at {path}")]
    private static partial void LogFolderDeleted(ILogger logger, string folderId, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Renamed folder {folderId}: {oldPath} → {newPath}")]
    private static partial void LogFolderRenamed(ILogger logger, string folderId, string oldPath, string newPath);
}
