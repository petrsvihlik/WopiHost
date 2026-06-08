namespace WopiHost.Abstractions;

/// <summary>
/// The Share URL types a host can advertise in <c>SupportedShareUrlTypes</c> and that a WOPI client
/// passes in the <c>X-WOPI-UrlType</c> request header on the
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getshareurl">GetShareUrl</see>
/// operation.
/// </summary>
public static class WopiShareUrlTypes
{
    /// <summary>A read-only link to the resource.</summary>
    public const string ReadOnly = "ReadOnly";

    /// <summary>A read-write link to the resource.</summary>
    public const string ReadWrite = "ReadWrite";

    /// <summary>The full set the default host implementation can produce.</summary>
    public static readonly string[] All = [ReadOnly, ReadWrite];
}
