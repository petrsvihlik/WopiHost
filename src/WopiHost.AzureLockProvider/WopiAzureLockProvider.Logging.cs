using Microsoft.Extensions.Logging;

namespace WopiHost.AzureLockProvider;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="WopiAzureLockProvider"/>.
/// </summary>
public partial class WopiAzureLockProvider
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "WOPI lock acquired on file {fileId} with lock id {lockId} (Azure blob lease)")]
    private static partial void LogLockAcquired(ILogger logger, string fileId, string lockId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "WOPI lock conflict on file {fileId}: requested lock {requestedLockId} rejected, existing lock {existingLockId}")]
    private static partial void LogLockAddRejected(
        ILogger logger,
        string fileId,
        string requestedLockId,
        string? existingLockId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "WOPI lock conflict on file {fileId}: requested lock {requestedLockId} lost the create-blob race")]
    private static partial void LogLockAddRaceLost(
        ILogger logger,
        string fileId,
        string requestedLockId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "WOPI lock {lockId} on file {fileId} expired and was evicted")]
    private static partial void LogLockExpired(ILogger logger, string fileId, string lockId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "WOPI lock {lockId} on file {fileId} removed")]
    private static partial void LogLockRemoved(ILogger logger, string fileId, string? lockId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to acquire Azure blob lease for fileId {fileId}")]
    private static partial void LogLeaseAcquireFailed(ILogger logger, Exception exception, string fileId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to renew Azure blob lease for fileId {fileId}")]
    private static partial void LogLeaseRenewFailed(ILogger logger, Exception exception, string fileId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to update lock metadata for fileId {fileId}")]
    private static partial void LogLockMetadataUpdateFailed(ILogger logger, Exception exception, string fileId);
}
