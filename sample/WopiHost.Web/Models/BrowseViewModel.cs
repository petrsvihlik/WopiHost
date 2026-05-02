namespace WopiHost.Web.Models;

/// <summary>
/// Top-level view model for the file/folder browser. Carries the listing of the current
/// container plus enough breadcrumb state to navigate back up the tree.
/// </summary>
public class BrowseViewModel
{
    public required string ContainerId { get; set; }
    public required string ContainerName { get; set; }
    public List<BreadcrumbPart> BreadcrumbParts { get; set; } = [];
    public List<ContainerViewModel> Containers { get; set; } = [];
    public List<FileViewModel> Files { get; set; } = [];
}

/// <summary>One ancestor folder rendered in the breadcrumb trail.</summary>
public sealed record BreadcrumbPart(string Name, string Url);
