using Microsoft.Extensions.Logging;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="WopiAzureStorageProvider"/>.
/// </summary>
public partial class WopiAzureStorageProvider
{
    [LoggerMessage(Level = LogLevel.Information, Message = "WopiAzureStorageProvider initialized for container {container}")]
    private static partial void LogInitialized(ILogger logger, string container);

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Renamed folder {folderId} ({blobCount} blobs): {oldPath} → {newPath}")]
    private static partial void LogFolderRenamed(ILogger logger, string folderId, string oldPath, string newPath, int blobCount);
}
