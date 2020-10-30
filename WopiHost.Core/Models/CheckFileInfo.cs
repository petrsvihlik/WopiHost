namespace WopiHost.Core.Models
{
    /// <summary>
    /// Model according to <see href="https://wopirest.readthedocs.io/en/latest/files/CheckFileInfo.html#checkfileinfo">CheckFileInfo documentation</see> and <see href="https://msdn.microsoft.com/en-us/library/hh622920.aspx">Microsoft WOPI documentation</see>
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

        /// <summary>
        /// A URI to a web page that the WOPI client should navigate to when the application closes, or in the event of an unrecoverable error.
        /// </summary>
        public string CloseUrl { get; set; }

        public bool DisableBrowserCachingOfUserContent { get; set; }

        public bool AllowAdditionalMicrosoftServices { get; set; }

        public bool AllowExternalMarketplace { get; set; }

        public bool DisablePrint { get; set; }

        public bool DisableTranslation { get; set; }

        /// <summary>
        /// A user-accessible URI to the file intended to allow the user to download a copy of the file. This URI should directly download the file and it should always provide the most recent version of the file.
        /// </summary>
        public string DownloadUrl { get; set; }

        /// <summary>
        /// A URI to a location that allows the user to create an embeddable URI to the file.
        /// </summary>
        public string FileEmbedCommandUrl { get; set; }

        /// <summary>
        /// A URI to a location that allows the user to share the file.
        /// </summary>
        public string FileSharingUrl { get; set; }

        /// <summary>
        /// A URI to the file location that the WOPI client uses to get the file.
        /// </summary>
        public string FileUrl { get; set; }

        /// <summary>
        /// A URI to a location that allows the user to view the version history for the file.
        /// </summary>
        public string FileVersionUrl { get; set; }

        public string HostAuthenticationId { get; set; }

        /// <summary>
        /// A URI to a <see href="https://wopi.readthedocs.io/en/latest/glossary.html#term-host-page">host page</see> that loads the <c>edit</c> WOPI action.
        /// </summary>
        public string HostEditUrl { get; set; }

        public string HostEmbeddedEditUrl { get; set; }

        /// <summary>
        /// A URI to a web page that provides access to a viewing experience for the file that can be embedded in another HTML page. This is typically a URI to a <see href="https://wopi.readthedocs.io/en/latest/glossary.html#term-host-page">host page</see> that loads the <c>embedview</c> WOPI action.
        /// </summary>
        public string HostEmbeddedViewUrl { get; set; }

        public string HostName { get; set; }

        public string HostNotes { get; set; }

        public string HostRestUrl { get; set; }

        /// <summary>
        /// A URI to a <see href="https://wopi.readthedocs.io/en/latest/glossary.html#term-host-page">host page</see> that loads the <c>view</c> WOPI action. This URL is used by Office Online to navigate between view and edit mode.
        /// </summary>
        public string HostViewUrl { get; set; }

        public string IrmPolicyDescription { get; set; }

        public string IrmPolicyTitle { get; set; }

        public string PresenceProvider { get; set; }

        public string PresenceUserId { get; set; }

        public string PrivacyUrl { get; set; }

        public bool ProtectInClient { get; set; }

        public string SignInUrl { get; set; }

        /// <summary>
        /// A Boolean value that indicates that, for this user, the file cannot be changed.
        /// </summary>
        public bool ReadOnly { get; set; }

        /// <summary>
        /// A Boolean value that indicates that the WOPI client should restrict what actions the user can perform on the file. The behavior of this property is dependent on the WOPI client.
        /// </summary>
        public bool RestrictedWebViewOnly { get; set; }

        public string Sha256 { get; set; }

        public string UniqueContentId { get; set; }

        /// <summary>
        /// A URI that will sign the current user out of the host’s authentication system.
        /// </summary>
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

        /// <summary>
        /// A string value containing information about the user. This string can be passed from a WOPI client to the host by means of a <see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/PutUserInfo.html#putuserinfo">PutUserInfo</see> operation. If the host has a UserInfo string for the user, they must include it in this property.
        /// </summary>
        public string UserInfo { get; set; }

        public string TenantId { get; set; }

        public string TermsOfUseUrl { get; set; }

        public string TimeZone { get; set; }

        /// <summary>
        /// A Boolean value indicating whether the user is authenticated with the host or not. Hosts should always set this to <c>true</c> for unauthenticated users, so that clients are aware that the user is anonymous.
        /// </summary>
        public bool IsAnonymousUser { get; set; }

        /// <summary>
        /// A Boolean value indicating whether the user is an education user or not.
        /// </summary>
        public bool IsEduUser { get; set; }

        /// <summary>
        /// A Boolean value indicating whether the user is a business user or not.
        /// <para>
        /// See also <seealso href="https://wopi.readthedocs.io/en/latest/scenarios/business.html#business-editing">Supporting document editing for business users</seealso>
        /// </para>
        /// </summary>
        public bool LicenseCheckForEditIsEnabled { get; set; }

        /// <summary>
        /// A Boolean value that indicates that the user has permission to view a <see href="https://wopi.readthedocs.io/en/latest/glossary.html#term-broadcast">broadcast</see> of this file.
        /// </summary>
        public bool UserCanAttend { get; set; }

        /// <summary>
        /// A Boolean value that indicates the user does not have sufficient permission to create new files on the WOPI server. Setting this to <c>true</c> tells the WOPI client that calls to <see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/PutRelativeFile.html#putrelativefile">PutRelativeFile</see> will fail for this user on the current file.
        /// </summary>
        public bool UserCanNotWriteRelative { get; set; }

        /// <summary>
        /// A Boolean value that indicates that the user has permission to <see href="https://wopi.readthedocs.io/en/latest/glossary.html#term-broadcast">broadcast</see> this file to a set of users who have permission to broadcast or view a broadcast of the current file.
        /// </summary>
        public bool UserCanPresent { get; set; }

        /// <summary>
        /// A Boolean value that indicates that the user has permission to alter the file. Setting this to <c>true</c> tells the WOPI client that it can call <see href="https://wopi.readthedocs.io/projects/wopirest/en/latest/files/PutFile.html#putfile">PutFile</see> on behalf of the user.
        /// </summary>
        public bool UserCanWrite { get; set; }

        /// <summary>
        /// A Boolean value that indicates the user has permission to rename the current file.
        /// </summary>
        public bool UserCanRename { get; set; }

        /// <summary>
        /// A string that is the name of the user, suitable for displaying in UI.
        /// </summary>
        public string UserFriendlyName { get; set; }

        public string UserPrincipalName { get; set; }

        public bool WebEditingDisabled { get; set; }

        #endregion
    }
}
