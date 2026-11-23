using Microsoft.Extensions.Logging;

namespace WopiHost.Core.Security.Authentication;

public partial class AccessTokenHandler
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Access token validation failed: {reason}")]
    private static partial void LogTokenValidationFailed(ILogger logger, string? reason);
}
