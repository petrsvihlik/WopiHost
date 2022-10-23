namespace WopiHost.Web.Models;

public class FileViewModel
{
    public string FileId { get; set; }
    public string FileName { get; set; }
    public bool SupportsView { get; set; }
    public bool SupportsEdit { get; set; }
    public Uri IconUri { get; set; }
}
