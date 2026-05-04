using Microsoft.Extensions.Logging;

namespace WopiHost.Url;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="WopiUrlBuilder"/>.
/// </summary>
public partial class WopiUrlBuilder
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "WOPI URL generated for extension {extension} action {action}")]
    private static partial void LogFileUrlGenerated(ILogger logger, string extension, string action);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "WOPI discovery has no URL template for extension {extension} action {action} — file type is not editable by the configured WOPI client")]
    private static partial void LogTemplateNotFound(ILogger logger, string extension, string action);
}
