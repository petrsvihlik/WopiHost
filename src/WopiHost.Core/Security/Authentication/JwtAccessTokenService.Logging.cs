using Microsoft.Extensions.Logging;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="JwtAccessTokenService"/>.
/// </summary>
public partial class JwtAccessTokenService
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Access token rejected: {reason}")]
    private static partial void LogAccessTokenRejected(ILogger logger, Exception ex, string reason);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "WopiSecurityOptions.SigningKey is not configured; generated an ephemeral in-memory key. " +
            "Tokens will be invalidated on restart and cannot work across multiple host instances. " +
            "Configure Wopi:Security:SigningKey for any non-development scenario.")]
    private static partial void LogEphemeralSigningKey(ILogger logger);
}
