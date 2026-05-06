namespace WopiHost.Discovery;

/// <summary>
/// Configuration object for <see cref="IDiscoverer"/>.
/// </summary>
public class DiscoveryOptions
{
    /// <summary>
    /// Default discovery refresh cadence (24 hours). Office Online publishes a new discovery
    /// XML at most once a day, so refreshing more often produces no useful change.
    /// </summary>
    public const int DefaultRefreshIntervalHours = 24;

    /// <summary>
    /// A network zone to retrieve the configuration from.
    /// </summary>
    public NetZoneEnum NetZone { get; set; }

    /// <summary>
    /// Determines how often should the discovery file be fetched again.
    /// Defaults to <see cref="DefaultRefreshIntervalHours"/> hours.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(DefaultRefreshIntervalHours);
}
