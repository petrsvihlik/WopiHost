using Microsoft.Extensions.Logging;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for
/// <see cref="WopiTelemetryActionFilter"/>.
/// </summary>
public sealed partial class WopiTelemetryActionFilter
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "WOPI {operation} on {resourceId} by {userId} (override={wopiOverride}) → {outcome}")]
    private static partial void LogActionCompleted(
        ILogger logger,
        string operation,
        string resourceId,
        string? userId,
        string? wopiOverride,
        string outcome);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "WOPI {operation} on {resourceId} threw an unhandled exception")]
    private static partial void LogActionFailed(
        ILogger logger,
        Exception exception,
        string operation,
        string resourceId);
}
