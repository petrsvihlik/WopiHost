namespace WopiHost.Abstractions;

/// <summary>
/// Details all WOPI file operation keywords
/// </summary>
public static class WopiFileOperations
{
    /// <summary>
    /// https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/lock
    /// </summary>
    public const string Lock = "LOCK";

    /// <summary>
    /// https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/unlock
    /// </summary>
    public const string Unlock = "UNLOCK";

    /// <summary>
    /// https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/refreshlock
    /// </summary>
    public const string RefreshLock = "REFRESH_LOCK";

    /// <summary>
    /// https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getlock
    /// </summary>
    public const string GetLock = "GET_LOCK";

    /// <summary>
    /// https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putfile
    /// </summary>
    public const string Put = "PUT";

    /// <summary>
    /// https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile
    /// </summary>
    public const string PutRelativeFile = "PUT_RELATIVE";

    /// <summary>
    /// https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putuserinfo
    /// </summary>
    public const string PutUserInfo = "PUT_USER_INFO";

    /// <summary>
    /// https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/deletefile
    /// </summary>
    public const string DeleteFile = "DELETE";

    /// <summary>
    /// https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/renamefile
    /// </summary>
    public const string RenameFile = "RENAME_FILE";

    /// <summary>
    /// Cobalt file operations
    /// </summary>
    public const string Cobalt = "COBALT";
}
