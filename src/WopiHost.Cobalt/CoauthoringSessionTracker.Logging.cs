using Microsoft.Extensions.Logging;

namespace WopiHost.Cobalt;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="CoauthoringSessionTracker"/>.
/// </summary>
public partial class CoauthoringSessionTracker
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Co-auth editor {userId} ({userName}) joined session for file {fileId}")]
    private static partial void LogEditorJoined(ILogger logger, string userId, string userName, string fileId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Co-auth editor {userId} left session for file {fileId}")]
    private static partial void LogEditorLeft(ILogger logger, string userId, string fileId);
}
