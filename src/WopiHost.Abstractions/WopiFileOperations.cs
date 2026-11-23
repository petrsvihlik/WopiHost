namespace WopiHost.Abstractions;

/// <summary>
/// Provides the <c>X-WOPI-Override</c> values of the WOPI file operations.
/// </summary>
public static class WopiFileOperations
{
    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/lock">Lock</see> operation.
    /// </summary>
    public const string Lock = "LOCK";

    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/unlock">Unlock</see> operation.
    /// </summary>
    public const string Unlock = "UNLOCK";

    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/refreshlock">RefreshLock</see> operation.
    /// </summary>
    public const string RefreshLock = "REFRESH_LOCK";

    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getlock">GetLock</see> operation.
    /// </summary>
    public const string GetLock = "GET_LOCK";

    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putfile">PutFile</see> operation.
    /// </summary>
    public const string Put = "PUT";

    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile">PutRelativeFile</see> operation.
    /// </summary>
    public const string PutRelativeFile = "PUT_RELATIVE";

    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putuserinfo">PutUserInfo</see> operation.
    /// </summary>
    public const string PutUserInfo = "PUT_USER_INFO";

    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/deletefile">DeleteFile</see> operation.
    /// </summary>
    public const string DeleteFile = "DELETE";

    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/renamefile">RenameFile</see> operation.
    /// </summary>
    public const string RenameFile = "RENAME_FILE";

    /// <summary>
    /// The <c>X-WOPI-Override</c> value for MS-FSSHTTP (Cobalt) requests.
    /// </summary>
    public const string Cobalt = "COBALT";

    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getshareurl">GetShareUrl (files)</see> operation.
    /// </summary>
    public const string GetShareUrl = "GET_SHARE_URL";

    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/fdc52ab9-b359-4465-a8c7-6aa98aa12e06">AddActivities</see> operation.
    /// </summary>
    public const string AddActivities = "ADD_ACTIVITIES";
}
