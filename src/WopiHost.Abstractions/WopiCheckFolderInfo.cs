namespace WopiHost.Abstractions;

/// <summary>
/// Represents the response of the MS-WOPI
/// <see href="https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/4b1d38ff-0d6a-42e4-8901-175b3c3c5890">CheckFolderInfo</see>
/// operation, used by OneNote for the web.
/// </summary>
public class WopiCheckFolderInfo
{
    #region "Required properties"

    /// <summary>
    /// Gets or sets the name of the folder without the path, for display in the UI.
    /// </summary>
    public required string FolderName { get; set; }

    #endregion

    #region "Optional properties"

    /// <summary>
    /// Gets or sets a string that uniquely identifies the owner of the folder.
    /// </summary>
    public string? OwnerId { get; set; }

    /// <summary>
    /// Gets or sets a string that uniquely identifies the user currently accessing the folder.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the name of the user, suitable for display in UI.
    /// </summary>
    public string? UserFriendlyName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is anonymous, that is, not authenticated with the host.
    /// </summary>
    public bool IsAnonymousUser { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has permission to alter the folder.
    /// </summary>
    public bool UserCanWrite { get; set; }

    /// <summary>
    /// Gets or sets a URI to a web page that provides a viewing experience for the folder.
    /// </summary>
    public Uri? HostViewUrl { get; set; }

    /// <summary>
    /// Gets or sets a URI to a web page that provides an editing experience for the folder.
    /// </summary>
    public Uri? HostEditUrl { get; set; }

    /// <summary>
    /// Gets or sets a URI to a web page that the WOPI client navigates to when the application closes.
    /// </summary>
    public Uri? CloseUrl { get; set; }

    /// <summary>
    /// Gets or sets a URI to a location that allows the user to share the folder.
    /// </summary>
    public Uri? FileSharingUrl { get; set; }

    /// <summary>
    /// Gets or sets the brand name of the host.
    /// </summary>
    public string? BreadcrumbBrandName { get; set; }

    /// <summary>
    /// Gets or sets a URI to a web page that the WOPI client navigates to when the user clicks UI that displays <see cref="BreadcrumbBrandName"/>.
    /// </summary>
    public Uri? BreadcrumbBrandUrl { get; set; }

    /// <summary>
    /// Gets or sets the name of the container that contains this folder.
    /// </summary>
    public string? BreadcrumbFolderName { get; set; }

    /// <summary>
    /// Gets or sets a URI to a web page that the WOPI client navigates to when the user clicks UI that displays <see cref="BreadcrumbFolderName"/>.
    /// </summary>
    public Uri? BreadcrumbFolderUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the WOPI client should disable all print functionality.
    /// </summary>
    public bool DisablePrint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the WOPI client should close the browser window when the user activates any Close UI.
    /// </summary>
    public bool CloseButtonClosesWindow { get; set; }

    #endregion
}
