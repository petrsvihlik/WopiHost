using System.ComponentModel.DataAnnotations;

namespace WopiHost.Validator.Models;

/// <summary>
/// Configuration object for the the WopiHost.Web application.
/// </summary>
public class WopiOptions
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
    /// The hard-coded userId to use WopiSecurityHandler
    /// </summary>
    public string UserId { get; set; } = "Anonymous";

    //TODO: create configuration sections related to host and client and group the related settings (e.g. discovery stuff and WopiUrlSettings should be together with the client)
}
