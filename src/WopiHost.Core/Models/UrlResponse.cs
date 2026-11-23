namespace WopiHost.Core.Models;

/// <summary>
/// Represents a response body containing a single URL.
/// </summary>
/// <param name="Url">The absolute URL to return.</param>
public record UrlResponse(Uri Url);
