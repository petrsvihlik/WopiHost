namespace WopiHost.Core.Models;

/// <summary>
/// Represents the response body of the <c>GetShareUrl</c> operation: a single absolute share link.
/// </summary>
/// <seealso href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getshareurl">GetShareUrl</seealso>
/// <param name="ShareUrl">The absolute share URL for the requested <c>X-WOPI-UrlType</c>.</param>
public record ShareUrlResponse(Uri ShareUrl);
