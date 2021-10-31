namespace WopiHost.Discovery
{
    /// <summary>
    /// Configuration object for <see cref="IDiscoverer"/>.
    /// </summary>
    public class DiscoveryOptions
    {
        /// <summary>
        /// A network zone to retrieve the configuration from.
        /// </summary>
        public NetZoneEnum NetZone { get; set; }
    }
}
