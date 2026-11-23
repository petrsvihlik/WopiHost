using System.ComponentModel.DataAnnotations;

namespace WopiHost.Validator.Models;

/// <summary>
/// Represents the WOPI configuration of the WopiHost.Validator sample.
/// </summary>
public class WopiOptions
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
    /// Gets or sets the hard-coded user id that issued access tokens carry. The same value is
    /// accepted verbatim as an access token by <c>SentinelOrJwtAccessTokenService</c>.
    /// </summary>
    public string UserId { get; set; } = "Anonymous";
}
