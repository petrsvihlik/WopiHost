namespace WopiHost.Abstractions;

/// <summary>
/// Provides the <c>X-WOPI-Override</c> values of the WOPI container operations.
/// </summary>
public static class WopiContainerOperations
{
    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/createchildcontainer">CreateChildContainer</see> operation.
    /// </summary>
    public const string CreateChildContainer = "CREATE_CHILD_CONTAINER";

    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/createchildfile">CreateChildFile</see> operation.
    /// </summary>
    public const string CreateChildFile = "CREATE_CHILD_FILE";

    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/deletecontainer">DeleteContainer</see> operation.
    /// </summary>
    public const string DeleteContainer = "DELETE_CONTAINER";

    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/renamecontainer">RenameContainer</see> operation.
    /// </summary>
    public const string RenameContainer = "RENAME_CONTAINER";

    /// <summary>
    /// The <c>X-WOPI-Override</c> value for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/getshareurl">GetShareUrl (containers)</see> operation.
    /// </summary>
    public const string GetShareUrl = "GET_SHARE_URL";
}
