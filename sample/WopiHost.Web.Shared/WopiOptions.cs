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
    /// Default configuration section path this options class binds to. Use with
    /// <c>builder.Configuration.GetSection(WopiOptions.SectionName)</c>.
    /// </summary>
    public const string SectionName = "Wopi";

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
}
