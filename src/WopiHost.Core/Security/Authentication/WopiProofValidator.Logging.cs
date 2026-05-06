using Microsoft.Extensions.Logging;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="WopiProofValidator"/>.
/// </summary>
public partial class WopiProofValidator
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "WOPI proof validation failed: missing or unparseable X-WOPI-Proof / X-WOPI-TimeStamp headers")]
    private static partial void LogProofHeadersMissing(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "WOPI proof validation failed: timestamp/keys invalid (age={age}, reason={reason})")]
    private static partial void LogProofTimestampOrKeysInvalid(ILogger logger, TimeSpan age, string reason);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "WOPI proof validation failed: RSA signature did not match any of the discovered keys")]
    private static partial void LogProofSignatureMismatch(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "WOPI proof validation threw")]
    private static partial void LogProofValidationError(ILogger logger, Exception exception);
}
