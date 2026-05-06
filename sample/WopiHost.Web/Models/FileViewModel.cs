namespace WopiHost.Web.Models;

public class FileViewModel
{
    public required string FileId { get; set; }
    public required string FileName { get; set; }
    public DateTime LastModified { get; set; }
    public long Size { get; set; }
    public string FormattedSize { get; set; } = string.Empty;
    public bool SupportsView { get; set; }
    public bool SupportsEdit { get; set; }

    /// <summary>Icon image source. Rendered as <c>&lt;img src&gt;</c> by Razor via <see cref="object.ToString"/>.</summary>
    /// <remarks>
    /// May be absolute (returned by <c>IDiscoverer.GetApplicationFavIconAsync</c>) or relative
    /// (the <c>/file.ico</c> fallback). If this type is ever exposed through a JSON API, note that
    /// OpenAPI's <c>format: uri</c> implies an absolute URI per RFC 3986; use <c>format: uri-reference</c>
    /// (via a schema filter) so strict clients don't reject relative values.
    /// </remarks>
    public required Uri IconUri { get; set; }
}
