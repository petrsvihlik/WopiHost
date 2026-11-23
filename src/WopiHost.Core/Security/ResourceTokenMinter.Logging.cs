using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Security;

// Source-generated LoggerMessage declarations for ResourceTokenMinter. Each mint emits one
// Debug-level entry so token-minting decisions can be traced alongside JwtAccessTokenService's
// token-issued log, without repeating the signing-level details.
public partial class ResourceTokenMinter
{
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Debug,
        Message = "Minted file-scoped WOPI access token for user {userId} bound to file {fileId} with {filePermissions}")]
    private static partial void LogFileTokenMinted(
        ILogger logger,
        string userId,
        string fileId,
        WopiFilePermissions filePermissions);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Debug,
        Message = "Minted container-scoped WOPI access token for user {userId} bound to container {containerId} with {containerPermissions}")]
    private static partial void LogContainerTokenMinted(
        ILogger logger,
        string userId,
        string containerId,
        WopiContainerPermissions containerPermissions);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Debug,
        Message = "Minted ecosystem_pointer WOPI access token for user {userId} bound to {resourceType}:{resourceId}")]
    private static partial void LogEcosystemTokenMinted(
        ILogger logger,
        string userId,
        WopiResourceType resourceType,
        string resourceId);
}
