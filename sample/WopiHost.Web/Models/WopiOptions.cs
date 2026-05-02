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

    /// <summary>
    /// IETF BCP 47 language tag (e.g. <c>en-US</c>) used to populate the <c>UI_LLCC</c> placeholder
    /// on WOPI URLs. Leave unset to fall back to <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>.
    /// </summary>
    public string? UiCulture { get; set; }

    //TODO: create configuration sections related to host and client and group the related settings (e.g. discovery stuff and WopiUrlSettings should be together with the client)
}
