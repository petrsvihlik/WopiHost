using Microsoft.Extensions.Logging;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for
/// <see cref="WopiTelemetryEndpointFilter"/>. Lives in a partial so the source generator
/// can emit the strongly-typed delegate without the per-call allocation of
/// <c>logger.LogX(string, params object?[])</c>.
/// </summary>
/// <remarks>
/// <c>userId</c> intentionally stays out of the message template — the telemetry filter
/// already pushes it into the log scope under <see cref="WopiTelemetry.Tags.UserId"/>.
/// </remarks>
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
