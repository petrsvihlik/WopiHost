using System.ComponentModel.DataAnnotations;
using WopiHost.Discovery;

namespace WopiHost.Web.Shared;

/// <summary>
/// Configuration object shared by the WopiHost.Web (anonymous) and WopiHost.Web.Oidc samples.
/// Both bind to the <c>Wopi</c> configuration section; OIDC-specific settings live alongside
/// in <c>WopiHost.Web.Oidc.Models</c>.
/// </summary>
public class WopiOptions : IDiscoveryOptions
{
    /// <summary>
    /// Specifies the configuration section path this options class binds to. Use with
    /// <c>builder.Configuration.GetSection(WopiOptions.SectionName)</c>.
    /// </summary>
    public const string SectionName = "Wopi";

    /// <summary>
    /// Gets or sets the base URI of the WOPI host server.
    /// </summary>
    [Required]
    public required Uri HostUrl { get; set; }

    /// <summary>
    /// Gets or sets the base URI of the WOPI client server (Office Online Server / Office Web Apps).
    /// </summary>
    [Required]
    public required Uri ClientUrl { get; set; }

    /// <summary>
    /// Gets or sets the IETF BCP 47 language tag (e.g. <c>en-US</c>) used to populate the <c>UI_LLCC</c>
    /// placeholder on WOPI URLs. When unset or blank, <see cref="System.Globalization.CultureInfo.CurrentUICulture"/> is used.
    /// </summary>
    public string? UiCulture { get; set; }
}
