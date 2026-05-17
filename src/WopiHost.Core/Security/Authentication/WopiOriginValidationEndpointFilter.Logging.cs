using Microsoft.Extensions.Logging;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for
/// <see cref="WopiOriginValidationEndpointFilter"/>.
/// </summary>
internal sealed partial class WopiOriginValidationEndpointFilter
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "WOPI request rejected: access token is missing")]
    private static partial void LogAccessTokenMissing(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "WOPI request rejected: proof-key validation failed")]
    private static partial void LogProofRejected(ILogger logger);

    // Logged at Error (not Warning) because reaching this filter without an authenticated
    // principal means the host's auth pipeline is misconfigured — operator action required.
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "WOPI proof filter reached on an unauthenticated request — auth pipeline misconfigured ([Authorize] missing, scheme misregistered, or middleware reordered). Returning 401.")]
    private static partial void LogUnauthenticatedRequestReached(ILogger logger);
}
