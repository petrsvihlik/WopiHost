using Microsoft.Extensions.Logging;

namespace WopiHost.Discovery;

/// <summary>
/// Source-generated <see cref="LoggerMessageAttribute"/> declarations for <see cref="WopiDiscoverer"/>.
/// </summary>
public partial class WopiDiscoverer
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "WOPI discovery refreshed: {appCount} apps loaded for NetZone {netZone}")]
    private static partial void LogDiscoveryRefreshed(ILogger logger, int appCount, string netZone);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "WOPI discovery proof keys refreshed")]
    private static partial void LogProofKeyRefreshed(ILogger logger);
}
