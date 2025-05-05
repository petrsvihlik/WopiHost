using System.ComponentModel;
using System.Text.Json.Serialization;

namespace WopiHost.Abstractions;

/// <summary>
/// Model according to <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo">CheckFileInfo documentation</see> and <see href="https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/71e66fb4-144b-4369-b597-f425f0b700b9">Microsoft WOPI documentation</see>
/// </summary>
public class WopiCheckFileInfo : IWopiHostCapabilities
{
    #region "Required properties"
    /// <summary>
    /// The string name of the file, including extension, without a path. Used for display in user interface (UI), and determining the extension of the file.
    /// </summary>
    public required string BaseFileName { get; set; }

    /// <summary>
    /// A string that uniquely identifies the owner of the file. In most cases, the user who uploaded or created the file should be considered the owner. This ID is subject to uniqueness and consistency requirements. See <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#requirements-for-user-identity-properties">Requirements for user identity properties</see> for more information.
    /// </summary>
    public required string OwnerId { get; set; }

    /// <summary>
    /// The size of the file in bytes, expressed as a long, a 64-bit signed integer.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// A string value uniquely identifying the user currently accessing the file. This ID is subject to uniqueness and consistency requirements. See <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#requirements-for-user-identity-properties">Requirements for user identity properties</see> for more information.
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// The current version of the file based on the server’s file version schema, as a string. This value must change when the file changes, and version values must never repeat for a given file.
    /// This value must be a string, even if numbers are used to represent versions.
    /// </summary>
    public required string Version { get; set; }

    #endregion

    #region "Optional properties"

    /// <summary>
    /// A string that represents the last time that the file was modified. This time must always be a must be a UTC time, and must be formatted in ISO 8601 round-trip format. For example, <c>"2009-06-15T13:45:30.0000000Z"</c>.
    /// </summary>
    public string? LastModifiedTime { get; set; }

    /// <summary>
    /// A string value representing the file extension for the file. This value must begin with a <c>.</c>. If provided, WOPI clients will use this value as the file extension. Otherwise the extension will be parsed from the <see cref="BaseFileName"/>.
    /// </summary>
    public string? FileExtension { get; set; }

    /// <summary>
    /// An integer value that indicates the maximum length for file names that the WOPI host supports, excluding the file extension. 
    /// The default value is 250. Note that WOPI clients will use this default value if the property is omitted or if it is explicitly set to <c>0</c>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault), DefaultValue(250)]
    public int FileNameMaxLength { get; set; } = 250;

    /// <summary>
    /// A string that indicates the breadcrumb name of the host.
    /// </summary>
    public string? BreadcrumbBrandName { get; set; }

    /// <summary>
    /// A URI to a web page that the WOPI client should navigate to when the user clicks on UI that displays <see cref="BreadcrumbBrandName"/>.
    /// </summary>
    public Uri? BreadcrumbBrandUrl { get; set; }

    /// <summary>
    /// A string that indicates the name of the file. If this is not provided, WOPI clients may use the <see cref="BaseFileName"/> value.
    /// </summary>
    public string? BreadcrumbDocName { get; set; }

    /// <summary>
    /// MAY specifies a URI to a web page that the WOPI client navigates to when the user clicks on UI that displays <see cref="BreadcrumbDocName"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Obsolete("Deprecated", false, UrlFormat = "https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-other#deprecated-properties</remarks>")]
    public Uri? BreadcrumbDocUrl { get; set; }

    /// <summary>
    /// A string that indicates the name of the container that contains the file.
    /// </summary>
    public string? BreadcrumbFolderName { get; set; }

    /// <summary>
    /// A URI to a web page that the WOPI client should navigate to when the user clicks on UI that displays <see cref="BreadcrumbFolderName"/>.
    /// </summary>
    public Uri? BreadcrumbFolderUrl { get; set; }

    /// <summary>
    /// A user-accessible URI directly to the file intended for opening the file through a client.
    /// </summary>
    [JsonIgnore]
    public Uri? ClientUrl { get; set; }

    /// <summary>
    /// A Boolean value that indicates the WOPI client should close the window or tab when the user activates any <c>Close</c> UI in the WOPI client.
    /// </summary>
    /// <remarks>do not enable for M365: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/faq/close-button-closes-window</remarks>
    [JsonIgnore]
    public bool CloseButtonClosesWindow { get; set; }

    /// <summary>
    /// A URI to a web page that the WOPI client should navigate to when the application closes, or in the event of an unrecoverable error.
    /// </summary>
    public Uri? CloseUrl { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the WOPI client should disable caching of file contents in the browser cache. Note that this has important performance implications for web browser-based WOPI clients.
    /// </summary>
    [JsonIgnore]
    public bool DisableBrowserCachingOfUserContent { get; set; }

    /// <summary>
    /// A Boolean value that indicates a WOPI client may connect to Microsoft services to provide end-user functionality.
    /// </summary>
    public bool AllowAdditionalMicrosoftServices { get; set; }

    /// <summary>
    /// A Boolean value that indicates that in the event of an error, the WOPI client is permitted to prompt the user for permission to collect a detailed report about their specific error. The information gathered could include the user’s file and other session-specific state.
    /// </summary>
    [JsonIgnore]
    public bool AllowErrorReportPrompt { get; set; }

    /// <summary>
    /// A Boolean value that indicates a WOPI client may allow connections to external services referenced in the file (for example, a marketplace of embeddable JavaScript apps).
    /// </summary>
    public bool AllowExternalMarketplace { get; set; }

    /// <summary>
    /// A Boolean value that indicates the WOPI client should disable all print functionality.
    /// </summary>
    public bool DisablePrint { get; set; }

    /// <summary>
    /// A Boolean value that indicates the WOPI client should disable all machine translation functionality.
    /// </summary>
    public bool DisableTranslation { get; set; }

    /// <summary>
    /// A user-accessible URI to the file intended to allow the user to download a copy of the file. This URI should directly download the file and it should always provide the most recent version of the file.
    /// </summary>
    public Uri? DownloadUrl { get; set; }

    /// <summary>
    /// A URI to a location that allows the user to create an embeddable URI to the file.
    /// </summary>
    [JsonIgnore]
    public Uri? FileEmbedCommandUrl { get; set; }

    /// <summary>
    /// A URI to a location that allows the user to share the file.
    /// </summary>
    public Uri? FileSharingUrl { get; set; }

    /// <summary>
    /// A URI to the file location that the WOPI client uses to get the file.
    /// </summary>
    public Uri? FileUrl { get; set; }

    /// <summary>
    /// A URI to a location that allows the user to view the version history for the file.
    /// </summary>
    public Uri? FileVersionUrl { get; set; }

    /// <summary>
    /// A string value uniquely identifying the user currently accessing the file.
    /// </summary>
    [JsonIgnore]
    public string? HostAuthenticationId { get; set; }

    /// <summary>
    /// A URI to a <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/glossary#host-page">host page</see> that loads the <c>edit</c> WOPI action.
    /// </summary>
    public Uri? HostEditUrl { get; set; }

    /// <summary>
    /// A URI to a web page that provides access to an editing experience for the file that can be embedded in another HTML page.
    /// </summary>
    [JsonIgnore]
    public Uri? HostEmbeddedEditUrl { get; set; }

    /// <summary>
    /// A URI to a web page that provides access to a viewing experience for the file that can be embedded in another HTML page. This is typically a URI to a <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/glossary#host-page">host page</see> that loads the <c>embedview</c> WOPI action.
    /// </summary>
    public Uri? HostEmbeddedViewUrl { get; set; }

    /// <summary>
    /// A string that is the name provided by the WOPI server used to identify it for logging and other informational purposes.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Obsolete("Deprecated", false, UrlFormat = "https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-other#deprecated-properties</remarks>")]
    public string? HostName { get; set; }

    /// <summary>
    /// A string that is used by the host to pass arbitrary information to the WOPI client.
    /// </summary>
    [JsonIgnore]
    public string HostNotes { get; set; } = string.Empty;

    /// <summary>
    /// A URI that is the base URI for REST operations for the file.
    /// </summary>
    [JsonIgnore]
    public Uri? HostRestUrl { get; set; }

    /// <summary>
    /// A URI to a <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/glossary#host-page">host page</see> that loads the <c>view</c> WOPI action. This URL is used by Office Online to navigate between view and edit mode.
    /// </summary>
    public Uri? HostViewUrl { get; set; }

    /// <summary>
    /// A string that the WOPI client should display to the user indicating the IRM policy for the file. This value should be combined with <see cref="IrmPolicyTitle"/>.
    /// </summary>
    [JsonIgnore]
    public string IrmPolicyDescription { get; set; } = string.Empty;

    /// <summary>
    /// A string that the WOPI client should display to the user indicating the IRM policy for the file. This value should be combined with <see cref="IrmPolicyDescription"/>.
    /// </summary>
    [JsonIgnore]
    public string IrmPolicyTitle { get; set; } = string.Empty;

    /// <summary>
    /// A string that identifies the provider of information that a WOPI client may use to discover information about the user’s online status (for example, whether a user is available via instant messenger).
    /// </summary>
    [JsonIgnore]
    public string PresenceProvider { get; set; } = string.Empty;

    /// <summary>
    /// A string that identifies the user in the context of the <see cref="PresenceProvider"/>.
    /// </summary>
    [JsonIgnore]
    public string PresenceUserId { get; set; } = string.Empty;

    /// <summary>
    /// A URI to a webpage that explains the privacy policy of the WOPI server.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Obsolete("Deprecated", false, UrlFormat = "https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-other#deprecated-properties</remarks>")]
    public Uri? PrivacyUrl { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the WOPI client should take measures to prevent copying and printing of the file. 
    /// </summary>
    [JsonIgnore]
    public bool ProtectInClient { get; set; }

    /// <summary>
    /// A URI that will allow the user to sign in using the host’s authentication system. This property can be used when supporting anonymous users. If this property is not provided, no sign in UI will be shown in Office Online.
    /// <para>See also <seealso cref="SignoutUrl"/></para>
    /// </summary>
    public Uri? SignInUrl { get; set; }

    /// <summary>
    /// A Boolean value that indicates that, for this user, the file cannot be changed.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the WOPI client should restrict what actions the user can perform on the file. The behavior of this property is dependent on the WOPI client.
    /// </summary>
    public bool RestrictedWebViewOnly { get; set; }

    /// <summary>
    /// A 256 bit SHA-2-encoded [<see href="http://csrc.nist.gov/publications/fips/fips180-2/fips180-2.pdf">FIPS 180-2</see>] hash of the file contents, as a Base64-encoded string. Used for caching purposes in WOPI clients.
    /// </summary>
    [JsonPropertyName("SHA256")]
    public string? Sha256 { get; set; }

    /// <summary>
    /// This string value can be provided rather than a SHA256 value if and only if the host can guarantee that two different files with the same content will have the same UniqueContentId value.
    /// </summary>
    public string UniqueContentId { get; set; } = string.Empty;

    /// <summary>
    /// A URI that will sign the current user out of the host’s authentication system.
    /// </summary>
    public Uri? SignoutUrl { get; set; }

    /// <summary>
    /// A string value containing information about the user. This string can be passed from a WOPI client to the host by means of a <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putuserinfo">PutUserInfo</see> operation. If the host has a UserInfo string for the user, they must include it in this property.
    /// </summary>
    public string UserInfo { get; set; } = string.Empty;

    /// <summary>
    /// A string value uniquely identifying the user’s ‘tenant,’ or group/organization to which they belong.
    /// </summary>
    [JsonIgnore]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// A Boolean value that indicates that the host supports the following WOPI operations:
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
    ///  <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/renamecontainer">RenameContainer</see></description>
    /// </item>
    /// </list>
    /// </summary>
    [JsonIgnore]
    public string TimeZone { get; set; } = string.Empty;

    /// <summary>
    /// A Boolean value that indicates that the host supports the following WOPI operations:
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
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/unlockandrelock">UnlockAndRelock</see> operations for this file.</description>
    /// </item>
    /// </list>
    /// </summary>
    public bool IsAnonymousUser { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getlock">GetLock</see> operation.
    /// </summary>
    public bool IsEduUser { get; set; }

    /// <summary>
    /// A Boolean value indicating whether the user is a business user or not.
    /// <para>
    /// See also <seealso href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/scenarios/business">Supporting document editing for business users</seealso>
    /// </para>
    /// </summary>
    public bool LicenseCheckForEditIsEnabled { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the following WOPI operations:
    /// <list type="bullet">
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/checkecosystem">CheckEcosystem</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/getecosystem">GetEcosystem (containers)</see></description>
    /// </item>
    ///  <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getecosystem">GetEcosystem (files)</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getrootcontainer">GetRootContainer (ecosystem)</see></description>
    /// </item>
    /// </list>
    /// </summary>
    public bool UserCanAttend { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getfilewopisrc">GetFileWopiSrc (ecosystem)</see> operation.
    /// </summary>
    public bool UserCanNotWriteRelative { get; set; }

    /// <summary>
    /// An array of strings containing the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/concepts#share-url">Share URL</see> types supported by the host.
    /// These types can be passed in the X-WOPI-UrlType request header to signify which Share URL type to return for the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getshareurl">GetShareUrl (files)</see> operation.
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
    public bool UserCanPresent { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the user has permission to alter the file. Setting this to <c>true</c> tells the WOPI client that it can call <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putfile">PutFile</see> on behalf of the user.
    /// </summary>
    public bool UserCanWrite { get; set; }

    /// <summary>
    /// A Boolean value that indicates the user has permission to rename the current file.
    /// </summary>
    public bool UserCanRename { get; set; }

    /// <summary>
    /// A string that is the name of the user, suitable for displaying in UI.
    /// </summary>
    public string? UserFriendlyName { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the following WOPI operations:
    /// <list type="bullet">
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putfile">PutFile</see></description>
    /// </item>
    /// <item>
    /// <description><see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile">PutRelativeFile</see></description>
    /// </item>
    /// </list>
    /// </summary>
    [JsonIgnore]
    public string UserPrincipalName { get; set; } = string.Empty;

    /// <summary>
    /// A Boolean value that indicates that the host supports the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/renamefile">RenameFile</see> operation.
    /// </summary>
    [JsonIgnore]
    public bool WebEditingDisabled { get; set; }
    #endregion

    #region "PostMessage properties"
    // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/scenarios/customization#postmessageorigin

    /// <summary>
    /// A Boolean value that indicates that the host supports the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/deletefile">DeleteFile</see> operation.
    /// </summary>
    public bool AppStateHistoryPostMessage { get; set; }

    /// <summary>
    /// A Boolean value that indicates that the host supports the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putuserinfo">PutUserInfo</see> operation.
    /// </summary>
    public bool ClosePostMessage { get; set; }

    /// <summary>
    /// A string value containing information about the user. This string can be passed from a WOPI client to the host by means of a <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putuserinfo">PutUserInfo</see> operation. If the host has a UserInfo string for the user, they must include it in this property.
    /// </summary>
    public bool EditModePostMessage { get; set; }

    /// <summary>
    /// A Boolean value that, when set to true, indicates the host expects to receive the Edit_Notification PostMessage.
    /// </summary>
    public bool EditNotificationPostMessage { get; set; }

    /// <summary>
    /// A Boolean value that, when set to true, indicates the host expects to receive the UI_FileEmbed PostMessage when the Embed UI in Microsoft 365 for the web is activated.
    /// </summary>
    [JsonIgnore]
    public bool FileEmbedCommandPostMessage { get; set; }

    /// <summary>
    /// A Boolean value that, when set to true, indicates the host expects to receive the UI_Sharing PostMessage when the Share UI in Microsoft 365 for the web is activated.
    /// </summary>
    public bool FileSharingPostMessage { get; set; }

    /// <summary>
    /// A Boolean value that, when set to true, indicates the host expects to receive the UI_FileVersions PostMessage when the Previous Versions UI (File ‣ Info ‣ Previous Versions) in Microsoft 365 for the web is activated.
    /// </summary>
    public bool FileVersionPostMessage { get; set; }

    /// <summary>
    /// A string value indicating the domain that the host page is sending and receiving PostMessages to and from. 
    /// Microsoft 365 for the web only sends outgoing PostMessages to this domain, and only listens to PostMessages from this domain.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PostMessageOrigin { get; set; }

    /// <summary>
    /// A Boolean value indicating whether the user is a business user or not.
    /// <para>
    /// See also <seealso href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/scenarios/business">Supporting document editing for business users</seealso>
    /// </para>
    /// </summary>
    /// <remarks>Pre-release property - not yet used by any WOPI client</remarks>
    public bool WorkflowPostMessage { get; set; }
    #endregion

    #region "IWopiHostCapabilities"
    /// <inheritdoc/>
    [JsonIgnore]
    public bool SupportsCoauth { get; set; }

    /// <inheritdoc/>
    public bool SupportsCobalt { get; set; }

    /// <inheritdoc/>
    public bool SupportsFolders { get; set; }

    /// <inheritdoc/>
    public bool SupportsContainers { get; set; }

    /// <inheritdoc/>
    public bool SupportsLocks { get; set; }

    /// <inheritdoc/>
    public bool SupportsGetLock { get; set; }

    /// <inheritdoc/>
    public bool SupportsExtendedLockLength { get; set; }

    /// <inheritdoc/>
    public bool SupportsEcosystem { get; set; }

    /// <inheritdoc/>
    [JsonIgnore]
    public bool SupportsFileCreation { get; set; }

    /// <inheritdoc/>
    public bool SupportsUpdate { get; set; }

    /// <inheritdoc/>
    public bool SupportsRename { get; set; }

    /// <inheritdoc/>
    public bool SupportsDeleteFile { get; set; }

    /// <inheritdoc/>    
    public bool SupportsUserInfo { get; set; }

    /// <inheritdoc/>
    [JsonIgnore]
    public bool SupportsGetFileWopiSrc { get; set; }

    /// <inheritdoc/>
    public IEnumerable<string> SupportedShareUrlTypes { get; set; } = [];

    /// <inheritdoc/>
    [JsonIgnore]
    public bool SupportsScenarioLinks { get; set; }

    /// <inheritdoc/>
    [JsonIgnore]
    public bool SupportsSecureStore { get; set; }
    #endregion
}
