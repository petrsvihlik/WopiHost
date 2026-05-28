using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="JwtAccessTokenService"/>.
/// </summary>
public partial class JwtAccessTokenService
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Issued access token for user {userId} bound to {resourceType}:{resourceId} with file_perm={filePermissions} container_perm={containerPermissions}, expires {expires:o}")]
    private static partial void LogAccessTokenIssued(
        ILogger logger,
        string userId,
        WopiResourceType resourceType,
        string resourceId,
        WopiFilePermissions filePermissions,
        WopiContainerPermissions containerPermissions,
        DateTimeOffset expires);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Access token rejected: {reason}")]
    private static partial void LogAccessTokenRejected(ILogger logger, Exception ex, string reason);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "WopiSecurityOptions.SigningKey is not configured; generated an ephemeral in-memory key. " +
            "Tokens will be invalidated on restart and cannot work across multiple host instances. " +
            "Configure Wopi:Security:SigningKey for any non-development scenario.")]
    private static partial void LogEphemeralSigningKey(ILogger logger);
}
