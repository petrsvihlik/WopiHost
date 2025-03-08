namespace WopiHost.Core.Models;

/// <summary>
/// Response object containing an Url.
/// </summary>
/// <param name="Url">the absolute Url to return.</param>
#pragma warning disable CA1056 // URI-like properties should not be strings
public record UrlResponse(string Url);
#pragma warning restore CA1056 // URI-like properties should not be strings
