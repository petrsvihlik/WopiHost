using WopiHost.Abstractions;

namespace WopiHost.Core.Models;

/// <summary>
/// Represents the <c>Bootstrap</c> object of a WOPI bootstrapper response.
/// </summary>
/// <seealso href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/getrootcontainer#sample-response">GetRootContainer sample response</seealso>
public class BootstrapInfo
{
    /// <summary>
    /// Gets or sets the URL of the host's ecosystem endpoint, with a WOPI access token appended.
    /// A GET request to this URL invokes the <c>CheckEcosystem</c> operation.
    /// </summary>
    public Uri? EcosystemUrl { get; set; }

    /// <summary>
    /// Gets or sets a value uniquely identifying the user making the request. It should match <see cref="WopiCheckFileInfo.UserId"/>,
    /// and is expected to be unique per user and consistent over time.
    /// </summary>
    public string? UserId { get; set; }

	/// <summary>
	/// Gets or sets a value identifying the user making the request, used to distinguish a user's accounts when the user has several with the host. Often, but not necessarily, an email address.
	/// </summary>
	public string? SignInName { get; set; }

	/// <summary>
	/// Gets or sets the display name of the user. It should match <see cref="WopiCheckFileInfo.UserFriendlyName"/>.
	/// </summary>
	public string? UserFriendlyName { get; set; }
}
