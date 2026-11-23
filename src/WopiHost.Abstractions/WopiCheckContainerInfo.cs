namespace WopiHost.Abstractions;

/// <summary>
/// Represents the response of the
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/checkcontainerinfo">CheckContainerInfo</see>
/// operation.
/// </summary>
/// <remarks>
/// Does not include the members of the original MS-WOPI
/// <see href="https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/4b1d38ff-0d6a-42e4-8901-175b3c3c5890">CheckFolderInfo</see>
/// response; see <see cref="WopiCheckFolderInfo"/> for that.
/// </remarks>
public class WopiCheckContainerInfo
{
    #region "Required properties"

    /// <summary>
    /// Gets or sets the name of the container without a path. The WOPI client displays this value in its UI.
    /// </summary>
    public required string Name { get; set; }

    #endregion

    #region "Optional properties"

    /// <summary>
    /// Gets or sets a URI to a webpage for the container.
    /// </summary>
    public Uri? HostUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is anonymous, that is, not authenticated with the host.
    /// Should match <see cref="WopiCheckFileInfo.IsAnonymousUser"/>.
    /// </summary>
    public bool IsAnonymousUser { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is an education user.
    /// Should match <see cref="WopiCheckFileInfo.IsEduUser"/>.
    /// </summary>
    public bool IsEduUser { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is a business user.
    /// Should match <see cref="WopiCheckFileInfo.LicenseCheckForEditIsEnabled"/>.
    /// </summary>
    public bool LicenseCheckForEditIsEnabled { get; set; }

    /// <summary>
    /// Gets or sets a URI to a webpage that lets the user control sharing of the container.
    /// Analogous to <see cref="WopiCheckFileInfo.FileSharingUrl"/>.
    /// </summary>
    public Uri? SharingUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has permission to create a new container in the container.
    /// </summary>
    public bool UserCanCreateChildContainer { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has permission to create a new file in the container.
    /// </summary>
    public bool UserCanCreateChildFile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has permission to delete the container.
    /// </summary>
    public bool UserCanDelete { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has permission to rename the container.
    /// </summary>
    public bool UserCanRename { get; set; }

    /// <summary>
    /// Gets or sets the Share URL types the host supports for this container. These values can be passed in the
    /// <c>X-WOPI-UrlType</c> request header to the
    /// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/getshareurl">GetShareUrl (containers)</see>
    /// operation. Mirrors <see cref="WopiCheckFileInfo.SupportedShareUrlTypes"/>.
    /// </summary>
    public IEnumerable<string> SupportedShareUrlTypes { get; set; } = [];

    #endregion
}
