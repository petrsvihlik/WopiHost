namespace WopiHost.Abstractions;

/// <summary>
/// Model implemented in accordance with the MS-WOPI specification:
/// https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/4b1d38ff-0d6a-42e4-8901-175b3c3c5890
/// Used by CheckFolderInfo which is a OneNote for the web operation.
/// </summary>
public class WopiCheckFolderInfo
{
    #region "Required properties"

    /// <summary>
    /// The name of the folder without the path. Used for display in the UI.
    /// </summary>
    public required string FolderName { get; set; }

    #endregion

    #region "Optional properties"

    /// <summary>
    /// A string that uniquely identifies the owner of the folder.
    /// </summary>
    public string? OwnerId { get; set; }

    /// <summary>
    /// A string value uniquely identifying the user currently accessing the folder.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// A string that is the name of the user, suitable for displaying in UI.
    /// </summary>
    public string? UserFriendlyName { get; set; }

    /// <summary>
    /// A Boolean value indicating whether the user is authenticated with the host or not.
    /// </summary>
    public bool IsAnonymousUser { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the user has permissions to alter the folder.
    /// </summary>
    public bool UserCanWrite { get; set; }

    /// <summary>
    /// A URI to a web page that provides a viewing experience for the folder.
    /// </summary>
    public Uri? HostViewUrl { get; set; }

    /// <summary>
    /// A URI to a web page that provides an editing experience for the folder.
    /// </summary>
    public Uri? HostEditUrl { get; set; }

    /// <summary>
    /// A URI to a web page that the WOPI client should navigate to when the application closes.
    /// </summary>
    public Uri? CloseUrl { get; set; }

    /// <summary>
    /// A URI to a location that allows the user to share the folder.
    /// </summary>
    public Uri? FileSharingUrl { get; set; }

    /// <summary>
    /// A string that indicates the name of the brand of the host.
    /// </summary>
    public string? BreadcrumbBrandName { get; set; }

    /// <summary>
    /// A URI to a web page that the WOPI client should navigate to when the user clicks on UI that displays <see cref="BreadcrumbBrandName"/>.
    /// </summary>
    public Uri? BreadcrumbBrandUrl { get; set; }

    /// <summary>
    /// A string that indicates the name of the container that contains this folder.
    /// </summary>
    public string? BreadcrumbFolderName { get; set; }

    /// <summary>
    /// A URI to a web page that the WOPI client should navigate to when the user clicks on UI that displays <see cref="BreadcrumbFolderName"/>.
    /// </summary>
    public Uri? BreadcrumbFolderUrl { get; set; }

    /// <summary>
    /// A Boolean value that indicates the WOPI client should disable all print functionality.
    /// </summary>
    public bool DisablePrint { get; set; }

    /// <summary>
    /// A Boolean value that indicates the WOPI client should close the browser window when the user activates any Close UI.
    /// </summary>
    public bool CloseButtonClosesWindow { get; set; }

    #endregion
}
