using Microsoft.Extensions.Logging;

namespace WopiHost.Core.Infrastructure;

// userId stays out of the message templates: the filter already pushes it into the log scope
// under WopiTelemetry.Tags.UserId.
internal sealed partial class WopiTelemetryEndpointFilter
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "WOPI {operation} on {resourceId} (override={wopiOverride}) → {outcome}")]
    private static partial void LogActionCompleted(
        ILogger logger,
        string operation,
        string resourceId,
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

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "WOPI {operation} on {resourceId} cancelled (client disconnected)")]
    private static partial void LogActionCancelled(
        ILogger logger,
        string operation,
        string resourceId);
}
