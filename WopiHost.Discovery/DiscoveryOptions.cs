using System;

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

        /// <summary>
        /// Determines how often should the discovery file be fetched again.
        /// </summary>
        public TimeSpan RefreshInterval { get; set; }
    }
}
