namespace WopiHost.Core.Models;

/// <summary>
/// Model according to <see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#wopi-host-capabilities-properties">WOPI host capabilities properties</see>
/// </summary>
public class HostCapabilities
{
    /// <summary>
    /// A Boolean value that indicates that the WOPI server supports multiple users making changes to this file simultaneously.
    /// </summary>
    public bool SupportsCoauth { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the following WOPI operations:
    /// <list type="bullet">
    /// <item>
    /// <description>ExecuteCellStorageRequest</description>
    /// </item>
    /// <item>
    /// <description>ExecuteCellStorageRelativeRequest</description>
    /// </item>
    /// </list>
    /// These operations are only used by OneNote for the web and are thus not needed to integrate with Office for the web or Office for iOS. These are included for completeness but do not need to be implemented.
    /// </summary>
    public bool SupportsCobalt { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the following WOPI operations:
    /// <list type="bullet">
    /// <item>
    /// <description>CheckFolderInfo - This operation is only used by OneNote for the web and is thus not needed to integrate with Office for the web or Office for iOS. It is included for completeness but does not need to be implemented.</description>
    /// </item>
    /// <item>
    /// <description>EnumerateChildren (folders) - This operation is only used by OneNote for the web and is thus not needed to integrate with Office for the web or Office for iOS. It is included for completeness but does not need to be implemented.</description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/deletefile">DeleteFile</see></description>
    /// </item>
    /// </list>
    /// </summary>  
    public bool SupportsFolders { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the following WOPI operations:
    /// <list type="bullet">
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/containers/checkcontainerinfo">CheckContainerInfo</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/containers/createchildcontainer">CreateChildContainer</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/containers/createchildfile">CreateChildFile</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/containers/deletecontainer">DeleteContainer</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/deletefile">DeleteFile</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/containers/enumerateancestors">EnumerateAncestors (containers)</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/enumerateancestors">EnumerateAncestors (files)</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/containers/enumeratechildren">EnumerateChildren (containers)</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/containers/getecosystem">GetEcosystem (containers)</see></description>
    /// </item>
    ///  <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/containers/renamecontainer">RenameContainer</see></description>
    /// </item>
    /// </list>
    /// </summary>
    public bool SupportsContainers { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the following WOPI operations:
    /// <list type="bullet">
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/lock">Lock</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/unlock">Unlock</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/refreshlock">RefreshLock</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/unlockandrelock">UnlockAndRelock</see> operations for this file.</description>
    /// </item>
    /// </list>
    /// </summary>
    public bool SupportsLocks { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the <see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/getlock">GetLock</see> operation.
    /// </summary>
    public bool SupportsGetLock { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports lock IDs up to 1024 ASCII characters long. If not provided, WOPI clients will assume that lock IDs are limited to 256 ASCII characters.
    /// </summary>
    public bool SupportsExtendedLockLength { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the following WOPI operations:
    /// <list type="bullet">
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/ecosystem/checkecosystem">CheckEcosystem</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/containers/getecosystem">GetEcosystem (containers)</see></description>
    /// </item>
    ///  <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/getecosystem">GetEcosystem (files)</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getrootcontainer">GetRootContainer (ecosystem)</see></description>
    /// </item>
    /// </list>
    /// </summary>
    public bool SupportsEcosystem { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the <see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getfilewopisrc">GetFileWopiSrc (ecosystem)</see> operation.
    /// </summary>
    public bool SupportsGetFileWopiSrc { get; set; }

    /// <summary>
    /// An array of strings containing the <see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/concepts#share-url">Share URL</see> types supported by the host.
    /// These types can be passed in the X-WOPI-UrlType request header to signify which Share URL type to return for the <see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/getshareurl">GetShareUrl (files)</see> operation.
    /// <para> Possible Values:
    /// <list type="bullet">
    /// <item>
    /// <description>ReadOnly - This type of Share URL allows users to view the file using the URL, but does not give them permission to edit the file.</description>
    /// </item>
    /// <item>
    /// <description>ReadWrite - This type of Share URL allows users to both view and edit the file using the URL.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </summary>
    public IEnumerable<string> SupportedShareUrlTypes { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports scenarios where users can operate on files in limited ways via restricted URLs.
    /// </summary>
    public bool SupportsScenarioLinks { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports calls to a secure data store utilizing credentials stored in the file.
    /// </summary>
    public bool SupportsSecureStore { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports creating new files using the WOPI client.
    /// </summary>
    public bool SupportsFileCreation { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the following WOPI operations:
    /// <list type="bullet">
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/putfile">PutFile</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile">PutRelativeFile</see></description>
    /// </item>
    /// </list>
    /// </summary>
    public bool SupportsUpdate { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the <see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/renamefile">RenameFile</see> operation.
    /// </summary>
    public bool SupportsRename { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the <see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/deletefile">DeleteFile</see> operation.
    /// </summary>
    public bool SupportsDeleteFile { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the <see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/putuserinfo">PutUserInfo</see> operation.
    /// </summary>
    public bool SupportsUserInfo { get; set; }
}
