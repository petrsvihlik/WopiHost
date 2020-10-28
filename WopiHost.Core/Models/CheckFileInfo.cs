namespace WopiHost.Core.Models
{
    /// <summary>
    /// Model according to https://wopirest.readthedocs.io/en/latest/files/CheckFileInfo.html#checkfileinfo and https://msdn.microsoft.com/en-us/library/hh622920.aspx
    /// </summary>
    public class CheckFileInfo
    {
        //TODO: and https://wopi.readthedocs.io/en/latest/scenarios/customization.html?highlight=checkfileinfo
        #region "Required properties"

        /// <summary>
        /// The string name of the file, including extension, without a path. Used for display in user interface (UI), and determining the extension of the file.
        /// </summary>
        public string BaseFileName { get; set; }

        /// <summary>
        /// A string that uniquely identifies the owner of the file. In most cases, the user who uploaded or created the file should be considered the owner. This ID is subject to uniqueness and consistency requirements. See <see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/CheckFileInfo.html?highlight=checkfileinfo#user-identity-requirements">Requirements for user identity properties</see> for more information.
        /// </summary>
        public string OwnerId { get; set; }

        /// <summary>
        /// The size of the file in bytes, expressed as a long, a 64-bit signed integer.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// A string value uniquely identifying the user currently accessing the file. This ID is subject to uniqueness and consistency requirements. See <see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/CheckFileInfo.html?highlight=checkfileinfo#user-identity-requirements">Requirements for user identity properties</see> for more information.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// The current version of the file based on the server’s file version schema, as a string. This value must change when the file changes, and version values must never repeat for a given file.
        /// This value must be a string, even if numbers are used to represent versions.
        /// </summary>
        public string Version { get; set; }

        #endregion

        #region "Optional properties"

        public string LastModifiedTime { get; set; }

        public string FileExtension { get; set; }

        public int FileNameMaxLength { get; set; }

        public string BreadcrumbBrandName { get; set; }

        public string BreadcrumbBrandUrl { get; set; }

        public string BreadcrumbDocName { get; set; }

        public string BreadcrumbDocUrl { get; set; }

        public string BreadcrumbFolderName { get; set; }

        public string BreadcrumbFolderUrl { get; set; }

        public string ClientUrl { get; set; }

        public bool CloseButtonClosesWindow { get; set; }

        public string CloseUrl { get; set; }

        public bool DisableBrowserCachingOfUserContent { get; set; }

        public bool AllowAdditionalMicrosoftServices { get; set; }

        public bool AllowExternalMarketplace { get; set; }

        public bool DisablePrint { get; set; }

        public bool DisableTranslation { get; set; }

        public string DownloadUrl { get; set; }

        public string FileSharingUrl { get; set; }

        public string FileUrl { get; set; }

        public string HostAuthenticationId { get; set; }

        public string HostEditUrl { get; set; }

        public string HostEmbeddedEditUrl { get; set; }

        public string HostEmbeddedViewUrl { get; set; }

        public string HostName { get; set; }

        public string HostNotes { get; set; }

        public string HostRestUrl { get; set; }

        public string HostViewUrl { get; set; }

        public string IrmPolicyDescription { get; set; }

        public string IrmPolicyTitle { get; set; }

        public string PresenceProvider { get; set; }

        public string PresenceUserId { get; set; }

        public string PrivacyUrl { get; set; }

        public bool ProtectInClient { get; set; }

        public string SignInUrl { get; set; }

        public bool ReadOnly { get; set; }

        public bool RestrictedWebViewOnly { get; set; }

        public string Sha256 { get; set; }

        public string UniqueContentId { get; set; }

        public string SignoutUrl { get; set; }

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
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/DeleteFile.html#deletefile">DeleteFile</see></description>
        /// </item>
        /// </list>
        /// </summary>   
        public bool SupportsFolders
        {
            get => SupportsContainers;
            set => SupportsContainers = value;
        }

        /// <summary>
        /// A Boolean value that indicates that the host supports the following WOPI operations:
        /// <list type="bullet">
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/containers/CheckContainerInfo.html#checkcontainerinfo">CheckContainerInfo</see></description>
        /// </item>
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/containers/CreateChildContainer.html#createchildcontainer">CreateChildContainer</see></description>
        /// </item>
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/containers/CreateChildFile.html#createchildfile">CreateChildFile</see></description>
        /// </item>
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/containers/DeleteContainer.html#deletecontainer">DeleteContainer</see></description>
        /// </item>
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/DeleteFile.html#deletefile">DeleteFile</see></description>
        /// </item>
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/containers/EnumerateAncestors.html#enumerateancestors-containers">EnumerateAncestors (containers)</see></description>
        /// </item>
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/EnumerateAncestors.html#enumerateancestors-files">EnumerateAncestors (files)</see></description>
        /// </item>
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/containers/EnumerateChildren.html#enumeratechildren">EnumerateChildren (containers)</see></description>
        /// </item>
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/containers/GetEcosystem.html#getecosystem-containers">GetEcosystem (containers)</see></description>
        /// </item>
        ///  <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/containers/RenameContainer.html#renamecontainer">RenameContainer</see></description>
        /// </item>
        /// </list>
        /// </summary>
        public bool SupportsContainers { get; set; }

        /// <summary>
        /// A Boolean value that indicates that the host supports the following WOPI operations:
        /// <list type="bullet">
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/Lock.html#lock">Lock</see></description>
        /// </item>
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/Unlock.html#unlock">Unlock</see></description>
        /// </item>
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/RefreshLock.html#refreshlock">RefreshLock</see></description>
        /// </item>
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/UnlockAndRelock.html#unlockandrelock">UnlockAndRelock</see> operations for this file.</description>
        /// </item>
        /// </list>
        /// </summary>
        public bool SupportsLocks { get; set; }

        /// <summary>
        /// A Boolean value that indicates that the host supports the <see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/GetLock.html#getlock">GetLock</see> operation.
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
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/ecosystem/CheckEcosystem.html#checkecosystem">CheckEcosystem</see></description>
        /// </item>
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/containers/GetEcosystem.html#getecosystem-containers">GetEcosystem (containers)</see></description>
        /// </item>
        ///  <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/GetEcosystem.html#getecosystem-files">GetEcosystem (files)</see></description>
        /// </item>
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/ecosystem/GetRootContainer.html#getrootcontainer">GetRootContainer (ecosystem)</see></description>
        /// </item>
        /// </list>
        /// </summary>
        public bool SupportsEcosystem { get; set; }

        /// <summary>
        /// A Boolean value that indicates that the host supports the <see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/ecosystem/GetFileWopiSrc.html#getfilewopisrc">GetFileWopiSrc (ecosystem)</see> operation.
        /// </summary>
        public bool SupportsGetFileWopiSrc { get; set; }

        /// <summary>
        /// An array of strings containing the <see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/concepts.html#term-share-url">Share URL</see> types supported by the host.
        /// These types can be passed in the X-WOPI-UrlType request header to signify which Share URL type to return for the <see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/GetShareUrl.html#getshareurl-files">GetShareUrl (files)</see> operation.
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
        public string[] SupportedShareUrlTypes { get; set; }

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
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/PutFile.html#putfile">PutFile</see></description>
        /// </item>
        /// <item>
        /// <description><see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/PutRelativeFile.html#putrelativefile">PutRelativeFile</see></description>
        /// </item>
        /// </list>
        /// </summary>
        public bool SupportsUpdate { get; set; }

        /// <summary>
        /// A Boolean value that indicates that the host supports the <see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/RenameFile.html#renamefile">RenameFile</see> operation.
        /// </summary>
        public bool SupportsRename { get; set; }

        /// <summary>
        /// A Boolean value that indicates that the host supports the <see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/DeleteFile.html#deletefile">DeleteFile</see> operation.
        /// </summary>
        public bool SupportsDeleteFile { get; set; }

        /// <summary>
        /// A Boolean value that indicates that the host supports the <see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/PutUserInfo.html#putuserinfo">PutUserInfo</see> operation.
        /// </summary>
        public bool SupportsUserInfo { get; set; }

        public string UserInfo { get; set; }

        public string TenantId { get; set; }

        public string TermsOfUseUrl { get; set; }

        public string TimeZone { get; set; }

        public bool IsAnonymousUser { get; set; }

        public bool IsEduUser { get; set; }

        public bool LicenseCheckForEditIsEnabled { get; set; }

        public bool UserCanAttend { get; set; }

        public bool UserCanNotWriteRelative { get; set; }

        public bool UserCanPresent { get; set; }

        public bool UserCanWrite { get; set; }

        public bool UserCanRename { get; set; }

        public string UserFriendlyName { get; set; }

        public string UserPrincipalName { get; set; }

        public bool WebEditingDisabled { get; set; }

        #endregion
    }
}
