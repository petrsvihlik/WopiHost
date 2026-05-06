using Microsoft.Extensions.Logging;

namespace WopiHost.Core.Security.Authorization;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="WopiAuthorizationHandler"/>.
/// </summary>
public partial class WopiAuthorizationHandler
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Token bound to resource '{tokenRid}' is being used against route id '{routeId}'. " +
            "This is allowed by default (WOPI tokens are session-scoped); register a stricter IAuthorizationHandler if you need to block cross-resource reuse.")]
    private static partial void LogResourceBindingMismatch(ILogger logger, string tokenRid, string? routeId);
}
