namespace WopiHost.Abstractions;

/// <summary>
/// Represents the host capability flags reported in <c>CheckFileInfo</c>.
/// </summary>
/// <seealso href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#wopi-host-capabilities-properties">WOPI host capabilities properties</seealso>
public interface IWopiHostCapabilities
{
    /// <summary>
    /// Gets or sets the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/concepts#share-url">Share URL</see> types supported by the host.
    /// </summary>
    /// <remarks>
    /// A WOPI client passes one of these in the <c>X-WOPI-UrlType</c> request header to select
    /// the Share URL type the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getshareurl">GetShareUrl (files)</see>
    /// operation returns. Possible values (see <see cref="WopiShareUrlTypes"/>):
    /// <list type="bullet">
    /// <item>
    /// <term><c>ReadOnly</c></term>
    /// <description>Lets users view the file through the URL, but not edit it.</description>
    /// </item>
    /// <item>
    /// <term><c>ReadWrite</c></term>
    /// <description>Lets users both view and edit the file through the URL.</description>
    /// </item>
    /// </list>
    /// </remarks>
    IEnumerable<string> SupportedShareUrlTypes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports multiple users making changes to the file simultaneously.
    /// </summary>
    bool SupportsCoauth { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports the
    /// <see href="https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/fdc52ab9-b359-4465-a8c7-6aa98aa12e06">AddActivities</see>
    /// operation (comments and mentions reported by the WOPI client).
    /// </summary>
    bool SupportsAddActivities { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports the <c>ExecuteCellStorageRequest</c>
    /// and <c>ExecuteCellStorageRelativeRequest</c> WOPI operations.
    /// </summary>
    /// <remarks>
    /// These operations are used only by OneNote for the web; integrating with Office for the web
    /// or Office for iOS doesn't need them.
    /// </remarks>
    bool SupportsCobalt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports the following WOPI operations:
    /// <list type="bullet">
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/checkcontainerinfo">CheckContainerInfo</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/createchildcontainer">CreateChildContainer</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/createchildfile">CreateChildFile</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/deletecontainer">DeleteContainer</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/deletefile">DeleteFile</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/enumerateancestors">EnumerateAncestors (containers)</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/enumerateancestors">EnumerateAncestors (files)</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/enumeratechildren">EnumerateChildren (containers)</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/getecosystem">GetEcosystem (containers)</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/renamecontainer">RenameContainer</see></description>
    /// </item>
    /// </list>
    /// </summary>
    bool SupportsContainers { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/deletefile">DeleteFile</see> operation.
    /// </summary>
    bool SupportsDeleteFile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports the following WOPI operations:
    /// <list type="bullet">
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/checkecosystem">CheckEcosystem</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/getecosystem">GetEcosystem (containers)</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getecosystem">GetEcosystem (files)</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getrootcontainer">GetRootContainer (ecosystem)</see></description>
    /// </item>
    /// </list>
    /// </summary>
    bool SupportsEcosystem { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports lock ids up to 1024 ASCII characters long.
    /// </summary>
    /// <remarks>
    /// When <see langword="false"/>, WOPI clients assume lock ids are limited to 256 ASCII characters.
    /// </remarks>
    bool SupportsExtendedLockLength { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports creating new files using the WOPI client.
    /// </summary>
    bool SupportsFileCreation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports the following WOPI operations:
    /// <list type="bullet">
    /// <item>
    /// <description><c>CheckFolderInfo</c> (used only by OneNote for the web).</description>
    /// </item>
    /// <item>
    /// <description><c>EnumerateChildren</c> (folders; used only by OneNote for the web).</description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/deletefile">DeleteFile</see></description>
    /// </item>
    /// </list>
    /// </summary>
    bool SupportsFolders { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getfilewopisrc">GetFileWopiSrc (ecosystem)</see> operation.
    /// </summary>
    bool SupportsGetFileWopiSrc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getlock">GetLock</see> operation.
    /// </summary>
    bool SupportsGetLock { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports the following WOPI operations:
    /// <list type="bullet">
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/lock">Lock</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/unlock">Unlock</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/refreshlock">RefreshLock</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/unlockandrelock">UnlockAndRelock</see></description>
    /// </item>
    /// </list>
    /// </summary>
    bool SupportsLocks { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/renamefile">RenameFile</see> operation.
    /// </summary>
    bool SupportsRename { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports scenarios where users can operate on files in limited ways via restricted URLs.
    /// </summary>
    bool SupportsScenarioLinks { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports calls to a secure data store using credentials stored in the file.
    /// </summary>
    bool SupportsSecureStore { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports the following WOPI operations:
    /// <list type="bullet">
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putfile">PutFile</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile">PutRelativeFile</see></description>
    /// </item>
    /// </list>
    /// </summary>
    bool SupportsUpdate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putuserinfo">PutUserInfo</see> operation.
    /// </summary>
    bool SupportsUserInfo { get; set; }
}