namespace WopiHost.Discovery.Models;

/// <summary>
/// Represents information about a WOPI application.
/// </summary>
public class AppInfo
{
    /// <summary>
    /// Gets or sets the application name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the application favicon URL.
    /// </summary>
    public string? FavIconUrl { get; set; }

    /// <summary>
    /// Gets or sets the supported file extensions and their actions.
    /// </summary>
    public Dictionary<string, List<ActionInfo>> Extensions { get; set; } = new();
} 