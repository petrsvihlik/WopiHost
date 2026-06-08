namespace WopiHost.Core.Models;

/// <summary>
/// Response body for the GetShareUrl operation — a single absolute share link.
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getshareurl"/>.
/// </summary>
/// <param name="ShareUrl">The absolute share URL for the requested <c>X-WOPI-UrlType</c>.</param>
public record ShareUrlResponse(Uri ShareUrl);
