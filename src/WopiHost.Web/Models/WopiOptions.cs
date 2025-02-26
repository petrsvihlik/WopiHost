namespace WopiHost.Web.Models;

/// <summary>
/// Configuration object for the the WopiHost.Web application.
/// </summary>
public class WopiOptions
{
    /// <summary>
    /// Base URI of the WOPI Host server.
    /// </summary>
    public required Uri HostUrl { get; set; }

    /// <summary>
    /// Base URI of the WOPI Client server (Office Online Server / Office Web Apps).
    /// </summary>
    public required Uri ClientUrl { get; set; }

    //TODO: create configuration sections related to host and client and group the related settings (e.g. discovery stuff and WopiUrlSettings should be together with the client)
}
