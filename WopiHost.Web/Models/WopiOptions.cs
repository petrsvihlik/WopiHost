using System;

namespace WopiHost.Web.Models
{
    /// <summary>
    /// Configuration object for the the WopiHost.Web application.
    /// </summary>
    public class WopiOptions
    {
        /// <summary>
        /// Base URI of the WOPI Host server.
        /// </summary>
        public Uri HostUrl { get; set; }

        /// <summary>
        /// Base URI of the WOPI Client server (Office Online Server / Office Web Apps).
        /// </summary>
        public Uri ClientUrl { get; set; }
    }
}
