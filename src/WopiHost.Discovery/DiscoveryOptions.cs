namespace WopiHost.Discovery;

/// <summary>
/// Represents the configuration options for <see cref="IDiscoverer"/>.
/// </summary>
public class DiscoveryOptions
{
    /// <summary>
    /// The default configuration section path this options class binds to. Use with
    /// <c>builder.Configuration.GetSection(DiscoveryOptions.SectionName)</c>.
    /// </summary>
    public const string SectionName = "Wopi:Discovery";

    /// <summary>
    /// The default discovery refresh cadence, in hours. Office Online publishes a new discovery
    /// XML at most once a day, so refreshing more often produces no useful change.
    /// </summary>
    public const int DefaultRefreshIntervalHours = 24;

    /// <summary>
    /// Gets or sets the network zone whose apps and actions are read from the discovery XML.
    /// </summary>
    public NetZoneEnum NetZone { get; set; }

    /// <summary>
    /// Gets or sets how often the discovery file is fetched again.
    /// Defaults to <see cref="DefaultRefreshIntervalHours"/> hours.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(DefaultRefreshIntervalHours);
}
