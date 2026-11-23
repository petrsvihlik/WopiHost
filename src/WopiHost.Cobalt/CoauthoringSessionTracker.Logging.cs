using Microsoft.Extensions.Logging;

namespace WopiHost.Cobalt;

/// <content>Contains the source-generated logging methods.</content>
public partial class CoauthoringSessionTracker
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Co-auth editor {userId} ({userName}) joined session for file {fileId}")]
    private static partial void LogEditorJoined(ILogger logger, string userId, string userName, string fileId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Co-auth editor {userId} left session for file {fileId}")]
    private static partial void LogEditorLeft(ILogger logger, string userId, string fileId);
}
