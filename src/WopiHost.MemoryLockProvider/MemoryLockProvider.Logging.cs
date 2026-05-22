using Microsoft.Extensions.Logging;

namespace WopiHost.MemoryLockProvider;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="MemoryLockProvider"/>.
/// </summary>
public partial class MemoryLockProvider
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "WOPI lock acquired on file {fileId} with lock id {lockId}")]
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
        Level = LogLevel.Debug,
        Message = "WOPI lock {lockId} on file {fileId} expired and was evicted")]
    private static partial void LogLockExpired(ILogger logger, string fileId, string lockId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "WOPI lock {lockId} on file {fileId} removed")]
    private static partial void LogLockRemoved(ILogger logger, string fileId, string lockId);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "MemoryLockProvider: no TimeProvider supplied — falling back to TimeProvider.System. " +
                  "If you registered a custom TimeProvider (e.g. FakeTimeProvider for tests) the provider was constructed without it.")]
    private static partial void LogTimeProviderFallback(ILogger logger);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "MemoryLockProvider: no IWopiLockComparer supplied — falling back to OrdinalWopiLockComparer. " +
                  "If you registered a custom comparer (e.g. JsonShapedWopiLockComparer) for OOS-style lock-id absorption, the provider was constructed without it.")]
    private static partial void LogLockComparerFallback(ILogger logger);
}
