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
/// <remarks>
/// <see cref="Url"/> is a relative <see cref="Uri"/> (<see cref="UriKind.Relative"/>) produced by
/// <c>IUrlHelper.Action</c> and rendered into an <c>&lt;a href&gt;</c> by Razor via <see cref="object.ToString"/>.
/// If this type is ever exposed through a JSON API, note that OpenAPI's <c>format: uri</c> implies an
/// absolute URI per RFC 3986; use <c>format: uri-reference</c> (via a schema filter) so strict clients
/// don't reject relative values.
/// </remarks>
public sealed record BreadcrumbPart(string Name, Uri Url);
