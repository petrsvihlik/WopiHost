using System.ComponentModel;
using System.Text.Json.Serialization;

namespace WopiHost.Abstractions;

/// <summary>
/// Represents the response of the <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo">CheckFileInfo</see> operation, as defined by the
/// Cloud Storage Partner Program documentation and the
/// <see href="https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/71e66fb4-144b-4369-b597-f425f0b700b9">MS-WOPI specification</see>.
/// </summary>
public class WopiCheckFileInfo : IWopiHostCapabilities
{
    #region "Required properties"
    /// <summary>
    /// Gets or sets the name of the file, including extension, without a path. Used for display in user interface (UI), and to determine the extension of the file.
    /// </summary>
    public required string BaseFileName { get; set; }

    /// <summary>
    /// Gets or sets a string that uniquely identifies the owner of the file. In most cases, the user who uploaded or created the file is considered the owner.
    /// </summary>
    /// <remarks>
    /// This ID is subject to uniqueness and consistency requirements; see <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#requirements-for-user-identity-properties">Requirements for user identity properties</see>.
    /// </remarks>
    public required string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the size of the file in bytes, expressed as a 64-bit signed integer.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets a string that uniquely identifies the user currently accessing the file.
    /// </summary>
    /// <remarks>
    /// This ID is subject to uniqueness and consistency requirements; see <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#requirements-for-user-identity-properties">Requirements for user identity properties</see>.
    /// </remarks>
    public required string UserId { get; set; }

    /// <summary>
    /// Gets or sets the current version of the file based on the server's file version schema. This value must change when the file changes, and version values must never repeat for a given file.
    /// This value must be a string, even if numbers are used to represent versions.
    /// </summary>
    public required string Version { get; set; }

    #endregion

    #region "Optional properties"

    /// <summary>
    /// Gets or sets the last time that the file was modified. This time must be a UTC time formatted in ISO 8601 round-trip format, for example <c>"2009-06-15T13:45:30.0000000Z"</c>.
    /// </summary>
    public string? LastModifiedTime { get; set; }

    /// <summary>
    /// Gets or sets the file extension for the file. This value must begin with a <c>.</c>. If provided, WOPI clients use this value as the file extension; otherwise the extension is parsed from <see cref="BaseFileName"/>.
    /// </summary>
    public string? FileExtension { get; set; }

    /// <summary>
    /// Gets or sets the maximum length for file names that the WOPI host supports, excluding the file extension.
    /// The default value is 250. WOPI clients use this default value if the property is omitted or explicitly set to <c>0</c>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault), DefaultValue(250)]
    public int FileNameMaxLength { get; set; } = 250;

    /// <summary>
    /// Gets or sets the brand name of the host.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BreadcrumbBrandName { get; set; }

    /// <summary>
    /// Gets or sets a URI to a web page that the WOPI client should navigate to when the user clicks UI that displays <see cref="BreadcrumbBrandName"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? BreadcrumbBrandUrl { get; set; }

    /// <summary>
    /// Gets or sets the name of the file. If this is not provided, WOPI clients may use the <see cref="BaseFileName"/> value.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BreadcrumbDocName { get; set; }

    /// <summary>
    /// Gets or sets the name of the container that contains the file.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BreadcrumbFolderName { get; set; }

    /// <summary>
    /// Gets or sets a URI to a web page that the WOPI client should navigate to when the user clicks UI that displays <see cref="BreadcrumbFolderName"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? BreadcrumbFolderUrl { get; set; }

    /// <summary>
    /// Gets or sets the Cobalt capabilities supported by the host.
    /// If <see cref="SupportsCobalt"/> is <see langword="false"/>, the WOPI client must ignore this property.
    /// <para>Possible values:
    /// <list type="bullet">
    /// <item><description><c>DownloadStreaming</c> - The host supports Cobalt streaming for download.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    /// <remarks>Listed by the WOPI spec as an unused/future property.</remarks>
    [JsonIgnore]
    public IEnumerable<string>? CobaltCapabilities { get; set; }

    /// <summary>
    /// Gets or sets a user-accessible URI directly to the file, intended for opening the file through a client.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? ClientUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the WOPI client should close the window or tab when the user activates any <c>Close</c> UI in the WOPI client.
    /// </summary>
    /// <remarks>
    /// Not recommended for Microsoft 365 for the web; see <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/faq/close-button-closes-window">Why should I avoid using the CloseButtonClosesWindow property</see>.
    /// </remarks>
    [JsonIgnore]
    public bool CloseButtonClosesWindow { get; set; }

    /// <summary>
    /// Gets or sets a URI to a web page that the WOPI client should navigate to when the application closes, or in the event of an unrecoverable error.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? CloseUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the WOPI client should disable caching of file contents in the browser cache. This has important performance implications for web browser-based WOPI clients.
    /// </summary>
    [JsonIgnore]
    public bool DisableBrowserCachingOfUserContent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a WOPI client may connect to Microsoft services to provide end-user functionality.
    /// </summary>
    public bool AllowAdditionalMicrosoftServices { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether, in the event of an error, the WOPI client is permitted to prompt the user for permission to collect a detailed report about their specific error. The information gathered could include the user's file and other session-specific state.
    /// </summary>
    [JsonIgnore]
    public bool AllowErrorReportPrompt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a WOPI client may allow connections to external services referenced in the file (for example, a marketplace of embeddable JavaScript apps).
    /// </summary>
    public bool AllowExternalMarketplace { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the WOPI client should disable all print functionality.
    /// </summary>
    public bool DisablePrint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the WOPI client should disable all machine translation functionality.
    /// </summary>
    public bool DisableTranslation { get; set; }

    /// <summary>
    /// Gets or sets a user-accessible URI to the file intended to allow the user to download a copy of the file. This URI should directly download the file and should always provide the most recent version of the file.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? DownloadUrl { get; set; }

    /// <summary>
    /// Gets or sets the edit-and-reply URI. The WOPI spec does not document this property yet.
    /// </summary>
    /// <remarks>Listed by the WOPI spec as an unused/future property.</remarks>
    [JsonIgnore]
    public Uri? EditAndReplyUrl { get; set; }

    /// <summary>
    /// Gets or sets a URI to a location that allows the user to create an embeddable URI to the file.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? FileEmbedCommandUrl { get; set; }

    /// <summary>
    /// Gets or sets a URI to a location that allows the user to share the file.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? FileSharingUrl { get; set; }

    /// <summary>
    /// Gets or sets a URI to the file location that the WOPI client uses to get the file instead of a GetFile request.
    /// </summary>
    /// <remarks>
    /// Per the WOPI proof-keys spec, clients fetch this URL <b>without</b> proof signing, so it must be served
    /// outside any proof-validated endpoint (a CDN, a pre-signed URL, or an unsigned
    /// token-checked download route). See <see cref="IWopiHostExtensions.OnCheckFileInfoAsync"/>
    /// for an example of setting it.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? FileUrl { get; set; }

    /// <summary>
    /// Gets or sets a URI to a location that allows the user to view the version history for the file.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? FileVersionUrl { get; set; }

    /// <summary>
    /// Gets or sets a string that uniquely identifies the user currently accessing the file.
    /// </summary>
    /// <remarks>The WOPI spec recommends <see cref="UserId"/> instead of this property.</remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HostAuthenticationId { get; set; }

    /// <summary>
    /// Gets or sets a URI to a <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/glossary#host-page">host page</see> that loads the <c>edit</c> WOPI action.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? HostEditUrl { get; set; }

    /// <summary>
    /// Gets or sets a URI to a web page that provides access to an editing experience for the file that can be embedded in another HTML page.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? HostEmbeddedEditUrl { get; set; }

    /// <summary>
    /// Gets or sets a URI to a web page that provides access to a viewing experience for the file that can be embedded in another HTML page. This is typically a URI to a <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/glossary#host-page">host page</see> that loads the <c>embedview</c> WOPI action.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? HostEmbeddedViewUrl { get; set; }

    /// <summary>
    /// Gets or sets a string that the host uses to pass arbitrary information to the WOPI client.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HostNotes { get; set; }

    /// <summary>
    /// Gets or sets the base URI for REST operations for the file.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? HostRestUrl { get; set; }

    /// <summary>
    /// Gets or sets a URI to a <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/glossary#host-page">host page</see> that loads the <c>view</c> WOPI action. Office for the web uses this URL to navigate between view and edit mode.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? HostViewUrl { get; set; }

    /// <summary>
    /// Gets or sets a string that the WOPI client should display to the user indicating the IRM policy for the file. This value should be combined with <see cref="IrmPolicyTitle"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IrmPolicyDescription { get; set; }

    /// <summary>
    /// Gets or sets a string that the WOPI client should display to the user indicating the IRM policy for the file. This value should be combined with <see cref="IrmPolicyDescription"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IrmPolicyTitle { get; set; }

    /// <summary>
    /// Gets or sets a string that identifies the provider of information that a WOPI client may use to discover the user's online status (for example, whether a user is available via instant messenger).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PresenceProvider { get; set; }

    /// <summary>
    /// Gets or sets a string that identifies the user in the context of the <see cref="PresenceProvider"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PresenceUserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the WOPI client should take measures to prevent copying and printing of the file.
    /// </summary>
    [JsonIgnore]
    public bool ProtectInClient { get; set; }

    /// <summary>
    /// Gets or sets a URI that allows the user to sign in using the host's authentication system. This property can be used when supporting anonymous users. If this property is not provided, no sign-in UI is shown in Office for the web.
    /// </summary>
    /// <seealso cref="SignoutUrl"/>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? SignInUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether, for this user, the file cannot be changed.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the WOPI client should restrict what actions the user can perform on the file. The resulting behavior depends on the WOPI client.
    /// </summary>
    public bool RestrictedWebViewOnly { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the WOPI host is experiencing capacity problems and would like to reduce the frequency at which the WOPI clients make calls to the host.
    /// <para>Possible values:
    /// <list type="bullet">
    /// <item><description><c>Normal</c> - The WOPI host is healthy and does not want any additional request throttling.</description></item>
    /// <item><description><c>Minor</c> - The WOPI host is requesting a small amount of throttling from the WOPI client.</description></item>
    /// <item><description><c>Medium</c> - The WOPI host is requesting a medium amount of throttling from the WOPI client.</description></item>
    /// <item><description><c>Major</c> - The WOPI host is requesting a large amount of throttling from the WOPI client.</description></item>
    /// <item><description><c>Critical</c> - The WOPI host is requesting the WOPI client to apply the largest amount of throttling possible.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequestedCallThrottling { get; set; }

    /// <summary>
    /// Gets or sets a 256-bit SHA-2-encoded [<see href="http://csrc.nist.gov/publications/fips/fips180-2/fips180-2.pdf">FIPS 180-2</see>] hash of the file contents, as a Base64-encoded string. Used for caching purposes in WOPI clients.
    /// </summary>
    [JsonPropertyName("SHA256")]
    public string? Sha256 { get; set; }

    /// <summary>
    /// Gets or sets a content identifier that can be provided instead of <see cref="Sha256"/> if and only if the host can guarantee that two different files with the same content have the same <see cref="UniqueContentId"/> value.
    /// </summary>
    public string UniqueContentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a URI that signs the current user out of the host's authentication system.
    /// </summary>
    /// <seealso cref="SignInUrl"/>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? SignoutUrl { get; set; }

    /// <summary>
    /// Gets or sets a string containing information about the user. A WOPI client can pass this string to the host by means of a <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putuserinfo">PutUserInfo</see> operation. If the host has a UserInfo string for the user, it must include it in this property.
    /// </summary>
    public string UserInfo { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a string that uniquely identifies the user's tenant, that is, the group or organization to which they belong.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets a string that passes time zone information to the WOPI client. The host determines the format of this value.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TimeZone { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is anonymous, that is, not authenticated with the host.
    /// </summary>
    /// <remarks>
    /// Hosts should always set this to <see langword="true"/> for unauthenticated users. When it is <see langword="true"/>,
    /// hosts can choose to omit <see cref="UserId"/>, but must still set <see cref="OwnerId"/>.
    /// </remarks>
    public bool IsAnonymousUser { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is an education user.
    /// </summary>
    public bool IsEduUser { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is a business user.
    /// </summary>
    /// <seealso href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/scenarios/business">Supporting document editing for business users</seealso>
    public bool LicenseCheckForEditIsEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has permission to view a <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/glossary#broadcast">broadcast</see> of this file.
    /// </summary>
    public bool UserCanAttend { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user lacks sufficient permission to create new files on the WOPI server. Setting this to <see langword="true"/> tells the WOPI client that calls to <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile">PutRelativeFile</see> will fail for this user on the current file.
    /// </summary>
    public bool UserCanNotWriteRelative { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has permission to <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/glossary#broadcast">broadcast</see> this file to a set of users who have permission to broadcast or view a broadcast of the current file.
    /// </summary>
    public bool UserCanPresent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has permission to alter the file. Setting this to <see langword="true"/> tells the WOPI client that it can call <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putfile">PutFile</see> on behalf of the user.
    /// </summary>
    public bool UserCanWrite { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has permission to rename the current file.
    /// </summary>
    public bool UserCanRename { get; set; }

    /// <summary>
    /// Gets or sets the name of the user, suitable for display in UI.
    /// </summary>
    public string? UserFriendlyName { get; set; }

    /// <summary>
    /// Gets or sets a string that uniquely identifies the user currently accessing the file.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the WOPI client must not allow the user to edit the file.
    /// </summary>
    /// <remarks>This does not mean that the user lacks rights to edit the file; <see cref="UserCanWrite"/> expresses those.</remarks>
    [JsonIgnore]
    public bool WebEditingDisabled { get; set; }

    /// <summary>
    /// Gets or sets the Microsoft Entra ID (formerly Azure AD) object ID of the current user.
    /// </summary>
    [JsonPropertyName("AADUserObjectId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AadUserObjectId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports batching of <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/addactivities">AddActivities</see> calls for a user.
    /// </summary>
    public bool AllowAddActivitiesUserBatching { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the WOPI client may enable features that have not yet reached general availability.
    /// </summary>
    public bool AllowEarlyFeatures { get; set; }

    /// <summary>
    /// Gets or sets a URI to the app catalog the WOPI client should use when offering add-ins to the user.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? AppCatalogUrl { get; set; }

    /// <summary>
    /// Gets or sets a string identifying the policy the host applied to this file, used by the WOPI client for policy reporting.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AppliedPolicyId { get; set; }

    /// <summary>
    /// Gets or sets a value offering guidance on how much the WOPI client should protect this user and document combination when it applies client-side throttling.
    /// <para>Possible values: <c>MostProtected</c>, <c>Protected</c>, <c>Normal</c>, <c>LessProtected</c>, <c>LeastProtected</c>.</para>
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientThrottlingProtection { get; set; }

    /// <summary>
    /// Gets or sets a string prefix identifying the compliance domain the file belongs to.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ComplianceDomainPrefix { get; set; }

    /// <summary>
    /// Gets or sets a value restricting what the user may copy out of the document.
    /// <para>Possible values: <c>BlockAll</c>, <c>CurrentDocumentOnly</c>.</para>
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CopyPasteRestrictions { get; set; }

    /// <summary>
    /// Gets or sets a URI to a WebDAV endpoint for the file, used to open it in the desktop client via Direct Invoke.
    /// </summary>
    [JsonPropertyName("DirectInvokeDAVUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? DirectInvokeDavUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may edit the file even though the host cannot persist the changes.
    /// </summary>
    /// <remarks>Deprecated by the WOPI spec.</remarks>
    public bool EditingCannotSave { get; set; }

    /// <summary>
    /// Gets or sets a URI identifying the origin of the page the WOPI client is embedded in.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? EmbeddingPageOrigin { get; set; }

    /// <summary>
    /// Gets or sets a string the host passes through to the embedding page for the duration of the session.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmbeddingPageSessionInfo { get; set; }

    /// <summary>
    /// Gets or sets the application feature names the WOPI client should enable for this session.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<string>? EnabledApplicationFeatures { get; set; }

    /// <summary>
    /// Gets or sets a string describing the kind of identifier carried in <see cref="HostAuthenticationId"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HostAuthenticationIdType { get; set; }

    /// <summary>
    /// Gets or sets a URI to a page that renders the file for syndication in an HTML <c>div</c>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? HostDivSyndicationViewUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Yammer integration is available for this file.
    /// </summary>
    public bool IsYammerEnabled { get; set; }

    /// <summary>
    /// Gets or sets the name of the organization that licensed the current user.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LicensedOrganization { get; set; }

    /// <summary>
    /// Gets or sets a URI the WOPI client invokes to open the file in the corresponding desktop client.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? OpenInClientCommandUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the file is protected by an information-protection policy.
    /// </summary>
    public bool ProtectedFile { get; set; }

    /// <summary>
    /// Gets or sets a URI to a page where the user can report the file as abusive.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? ReportAbuseUrl { get; set; }

    /// <summary>
    /// Gets or sets a string describing the Safe Links scanning state that applies to links in the file.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SafeLinksStatus { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports policy checks for the current user and file.
    /// </summary>
    public bool SupportsCheckPolicy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports checking whether a user has access to the file.
    /// </summary>
    public bool SupportsCheckUserAccess { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host can resolve contacts on behalf of the WOPI client.
    /// </summary>
    public bool SupportsContactsResolution { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports storing per-user values against the file.
    /// </summary>
    public bool SupportsFileUserValue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports retrieving the file's activity feed.
    /// </summary>
    public bool SupportsGetActivities { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports granting a user access to the file.
    /// </summary>
    public bool SupportsGrantUserAccess { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports returning information-protection policies for the file.
    /// </summary>
    public bool SupportsPolicies { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host supports the reviewing mode of the WOPI client.
    /// </summary>
    public bool SupportsReviewing { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host is temporarily unable to process writes on the file.
    /// </summary>
    public bool TemporarilyNotWritable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may review the file, that is, add and resolve comments and tracked changes.
    /// </summary>
    public bool UserCanReview { get; set; }

    /// <summary>
    /// Gets or sets the workflow types available for this file. Must be set whenever <see cref="WorkflowUrl"/> or <see cref="WorkflowPostMessage"/> is provided; otherwise the WOPI client ignores both.
    /// <para>Possible values: <c>Assign</c>, <c>Submit</c>.</para>
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<string>? WorkflowType { get; set; }

    /// <summary>
    /// Gets or sets a URI the WOPI client navigates to when the user starts a workflow named in <see cref="WorkflowType"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? WorkflowUrl { get; set; }
    #endregion

    #region "PostMessage properties"
    // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/scenarios/customization#postmessageorigin

    /// <summary>
    /// Gets or sets a value indicating whether the host outer frame supports HTML5 session history, so it receives <c>App_PushState</c> PostMessages and propagates <c>onpopstate</c> events back through the <c>App_PopState</c> PostMessage.
    /// </summary>
    /// <remarks>Used only by OneNote for the web.</remarks>
    public bool AppStateHistoryPostMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host expects to receive the <c>UI_Close</c> PostMessage when the Close UI in Microsoft 365 for the web is activated.
    /// </summary>
    public bool ClosePostMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host expects to receive the <c>UI_Edit</c> PostMessage when the Edit UI in Microsoft 365 for the web is activated.
    /// </summary>
    public bool EditModePostMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host expects to receive the <c>Edit_Notification</c> PostMessage.
    /// </summary>
    public bool EditNotificationPostMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host expects to receive the <c>UI_FileEmbed</c> PostMessage when the Embed UI in Microsoft 365 for the web is activated.
    /// </summary>
    [JsonIgnore]
    public bool FileEmbedCommandPostMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host expects to receive the <c>UI_Sharing</c> PostMessage when the Share UI in Microsoft 365 for the web is activated.
    /// </summary>
    public bool FileSharingPostMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host expects to receive the <c>UI_FileVersions</c> PostMessage when the Previous Versions UI (File ‣ Info ‣ Previous Versions) in Microsoft 365 for the web is activated.
    /// </summary>
    public bool FileVersionPostMessage { get; set; }

    /// <summary>
    /// Gets or sets the domain that the host page sends and receives PostMessages to and from.
    /// Microsoft 365 for the web only sends outgoing PostMessages to this domain, and only listens to PostMessages from this domain.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PostMessageOrigin { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host expects to receive the <c>UI_Workflow</c> PostMessage when the Workflow UI in Microsoft 365 for the web is activated.
    /// </summary>
    /// <remarks>Pre-release property, not yet used by any WOPI client. Ignored unless <see cref="WorkflowType"/> is provided.</remarks>
    public bool WorkflowPostMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host expects to receive the <c>UI_InsertImage</c> PostMessage when the Insert Image UI in Microsoft 365 for the web is activated.
    /// </summary>
    public bool InsertImagePostMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host expects to receive the <c>UI_OpenInClient</c> PostMessage when the Open in Client UI in Microsoft 365 for the web is activated.
    /// </summary>
    public bool OpenInClientPostMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host expects to receive the Permissions PostMessage when the file's permissions change during the session.
    /// </summary>
    public bool PermissionsPostMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host expects to receive the PolicyCheck PostMessage when the WOPI client evaluates a policy.
    /// </summary>
    public bool PolicyCheckPostMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host expects to receive the <c>UI_ReportAbuse</c> PostMessage when the Report Abuse UI in Microsoft 365 for the web is activated.
    /// </summary>
    public bool ReportAbusePostMessage { get; set; }
    #endregion

    #region "IWopiHostCapabilities"
    /// <inheritdoc/>
    [JsonIgnore]
    public bool SupportsCoauth { get; set; }

    /// <inheritdoc/>
    public bool SupportsAddActivities { get; set; }

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
