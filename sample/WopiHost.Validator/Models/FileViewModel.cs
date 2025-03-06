namespace WopiHost.Validator.Models;

public class FileViewModel
{
    public required string FileId { get; set; }
    public required string FileName { get; set; }
    public DateTime LastModified { get; set; }
    public long Size { get; set; }
    public string FormattedSize { get; set; } = string.Empty;
    public bool SupportsView { get; set; }
    public bool SupportsEdit { get; set; }
    public required Uri IconUri { get; set; }
}
