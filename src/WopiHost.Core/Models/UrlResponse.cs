namespace WopiHost.Core.Models;

/// <summary>
/// Response object containing an Url.
/// </summary>
/// <param name="Url">the absolute Url to return.</param>
public record UrlResponse(Uri Url);
