using Microsoft.Extensions.Logging;

namespace WopiHost.Discovery;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="HttpDiscoveryFileProvider"/>.
/// </summary>
public partial class HttpDiscoveryFileProvider
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "WOPI discovery XML fetched from {baseAddress} in {elapsedMs}ms")]
    private static partial void LogDiscoveryFetched(ILogger logger, Uri? baseAddress, long elapsedMs);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "WOPI discovery XML fetch failed from {baseAddress}")]
    private static partial void LogDiscoveryFetchFailed(ILogger logger, Exception exception, Uri? baseAddress);
}
