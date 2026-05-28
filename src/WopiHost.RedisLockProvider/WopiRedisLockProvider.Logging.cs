using Microsoft.Extensions.Logging;

namespace WopiHost.RedisLockProvider;

/// <summary>
/// LoggerMessage-generated log sinks for <see cref="WopiRedisLockProvider"/>. Kept in a separate
/// partial so the operational story (event ids, log levels, messages) is reviewable in one place.
/// </summary>
public sealed partial class WopiRedisLockProvider
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Lock acquired in Redis for {FileId} (LockId={LockId}).")]
    static partial void LogLockAcquired(ILogger logger, string fileId, string lockId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Lock acquisition rejected for {FileId} (RequestedLockId={LockId}, ExistingLockId={ExistingLockId}).")]
    static partial void LogLockAddRejected(ILogger logger, string fileId, string lockId, string? existingLockId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "Lock removed in Redis for {FileId}.")]
    static partial void LogLockRemoved(ILogger logger, string fileId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "Expired lock evicted in Redis for {FileId} (LockId={LockId}).")]
    static partial void LogLockExpired(ILogger logger, string fileId, string lockId);
}
