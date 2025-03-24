using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery.Models;

/// <summary>
/// Represents processed WOPI discovery data optimized for quick lookups.
/// </summary>
public class WopiDiscoveryData
{
    /// <summary>
    /// Gets or sets the collection of application information.
    /// </summary>
    public List<AppInfo> Apps { get; set; } = new();

    /// <summary>
    /// Gets or sets a lookup table for quickly finding if a file extension is supported.
    /// Key: extension, Value: true if supported
    /// </summary>
    public Dictionary<string, bool> ExtensionLookup { get; set; } = new();

    /// <summary>
    /// Gets or sets a lookup table for quickly finding action information by extension and action.
    /// Key: extension, Value: Dictionary of actions mapped to their info
    /// </summary>
    public Dictionary<string, Dictionary<WopiActionEnum, ActionInfo>> ActionLookup { get; set; } = new();

    /// <summary>
    /// Gets or sets a lookup table for quickly finding application information by extension.
    /// Key: extension, Value: The application info
    /// </summary>
    public Dictionary<string, AppInfo> ExtensionToAppLookup { get; set; } = new();
} 