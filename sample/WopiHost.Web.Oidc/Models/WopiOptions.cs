using System.ComponentModel.DataAnnotations;
using WopiHost.Discovery;

namespace WopiHost.Web.Oidc.Models;

/// <summary>
/// Configuration for the OIDC-enabled WopiHost frontend sample.
/// </summary>
public class WopiOptions : IDiscoveryOptions
{
    /// <summary>Base URI of the WOPI Host server.</summary>
    [Required]
    public required Uri HostUrl { get; set; }

    /// <summary>Base URI of the WOPI Client server (Office Online Server / Office Web Apps).</summary>
    [Required]
    public required Uri ClientUrl { get; set; }
}
