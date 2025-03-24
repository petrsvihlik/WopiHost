using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery.Models;

/// <summary>
/// Represents information about a WOPI action for a specific file extension.
/// </summary>
public class ActionInfo
{
    /// <summary>
    /// Gets or sets the action enum value.
    /// </summary>
    public WopiActionEnum Action { get; set; }

    /// <summary>
    /// Gets or sets the URL template for this action.
    /// </summary>
    public string? UrlTemplate { get; set; }

    /// <summary>
    /// Gets or sets the requirements for this action.
    /// </summary>
    public IEnumerable<string> Requirements { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether this action requires Cobalt.
    /// </summary>
    public bool RequiresCobalt => Requirements.Contains("cobalt");
} 