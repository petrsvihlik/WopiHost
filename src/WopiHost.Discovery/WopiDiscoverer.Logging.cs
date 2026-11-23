using Microsoft.Extensions.Logging;

namespace WopiHost.Discovery;

public partial class WopiDiscoverer
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "WOPI discovery refreshed: {appCount} apps loaded for NetZone {netZone}")]
    private static partial void LogDiscoveryRefreshed(ILogger logger, int appCount, NetZoneEnum netZone);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "WOPI discovery proof keys refreshed")]
    private static partial void LogProofKeyRefreshed(ILogger logger);
}
