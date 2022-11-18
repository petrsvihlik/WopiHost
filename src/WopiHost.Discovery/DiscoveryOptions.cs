namespace WopiHost.Discovery;

/// <summary>
/// Configuration object for <see cref="IDiscoverer"/>.
/// </summary>
public class DiscoveryOptions
{
    /// <summary>
    /// A network zone to retrieve the configuration from.
    /// </summary>
    public NetZoneEnum NetZone { get; set; }

    /// <summary>
    /// Determines how often should the discovery file be fetched again.
    /// The default value is 24 hours.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(24);
}
