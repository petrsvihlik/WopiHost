namespace WopiHost.Abstractions;

/// <summary>
/// Breadcrumb / branding slice of the WOPI <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo">CheckFileInfo</see>
/// response — the navigation widgets the WOPI client renders above the document area. Implemented
/// by <see cref="WopiCheckFileInfo"/>.
/// </summary>
public interface IWopiCheckFileInfoBreadcrumb
{
    /// <summary>Brand text (typically the host's product name).</summary>
    string? BreadcrumbBrandName { get; set; }

    /// <summary>Where to navigate when the user clicks the brand text.</summary>
    Uri? BreadcrumbBrandUrl { get; set; }

    /// <summary>Display name of the document; falls back to <see cref="IWopiCheckFileInfoIdentity.BaseFileName"/> when null.</summary>
    string? BreadcrumbDocName { get; set; }

    /// <summary>Display name of the parent folder/container.</summary>
    string? BreadcrumbFolderName { get; set; }

    /// <summary>Where to navigate when the user clicks the parent folder name.</summary>
    Uri? BreadcrumbFolderUrl { get; set; }
}
