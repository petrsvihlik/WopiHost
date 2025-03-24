namespace WopiHost.Discovery.Models;

/// <summary>
/// Represents a collection of WOPI applications.
/// </summary>
public class WopiApps
{
    /// <summary>
    /// Gets or sets the collection of applications.
    /// </summary>
    public List<AppInfo> Apps { get; set; } = new();
} 