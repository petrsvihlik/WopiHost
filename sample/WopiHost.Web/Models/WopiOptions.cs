using System.ComponentModel.DataAnnotations;
using WopiHost.Discovery;

namespace WopiHost.Web.Models;

/// <summary>
/// Configuration object for the the WopiHost.Web application.
/// </summary>
public class WopiOptions : IDiscoveryOptions
{
    /// <summary>
    /// Base URI of the WOPI Host server.
    /// </summary>
    [Required]
    public required Uri HostUrl { get; set; }

    /// <summary>
    /// Base URI of the WOPI Client server (Office Online Server / Office Web Apps).
    /// </summary>
    [Required]
    public required Uri ClientUrl { get; set; }

    //TODO: create configuration sections related to host and client and group the related settings (e.g. discovery stuff and WopiUrlSettings should be together with the client)
}
