using Microsoft.Extensions.Logging;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="WopiOriginValidationActionFilter"/>.
/// </summary>
public partial class WopiOriginValidationActionFilter
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "WOPI request rejected: access token is missing")]
    private static partial void LogAccessTokenMissing(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "WOPI request rejected: proof-key validation failed")]
    private static partial void LogProofRejected(ILogger logger);
}
