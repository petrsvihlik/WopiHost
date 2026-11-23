namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Provides the WOPI HTTP header names.
/// </summary>
public static class WopiHeaders
{
    /// <summary>
    /// A string specifying the WOPI operation the client requests from the host.
    /// </summary>
    public const string WopiOverride = "X-WOPI-Override";

    /// <summary>
    /// A string value identifying the current lock on the file.
    /// The header must always be included in a <c>409 Conflict</c> response to a lock operation.
    /// It should not be included in a <c>200 OK</c> response to one.
    /// </summary>
	public const string Lock = "X-WOPI-Lock";

    /// <summary>
    /// Spec-compliant default for the <c>X-WOPI-Lock</c> response header when the file is unlocked
    /// but the spec requires the header to be present (notably <c>GetLock</c> on an unlocked file
    /// and <c>PutFile</c> on a non-empty unlocked file).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The runtime value is resolved from <see cref="WopiHost.Core.Models.WopiHostOptions.EmptyLockHeaderValue"/>
    /// (default: empty string, matching the WOPI spec). This constant is the fallback used when
    /// no service provider is available.
    /// </para>
    /// <para>
    /// Hosting under IIS in-process strips empty header values before they reach the wire.
    /// Hosts on that path should set <c>WopiHostOptions.EmptyLockHeaderValue = " "</c>
    /// to opt into the single-space workaround.
    /// </para>
    /// </remarks>
    public const string EmptyLockValue = "";

    /// <summary>
    /// A string provided by the WOPI client that is the existing lock on the file, sent on <c>UnlockAndRelock</c>.
    /// When the header is absent, the request is a plain <c>Lock</c> request.
    /// </summary>
    public const string OldLock = "X-WOPI-OldLock";

    /// <summary>
    /// A comma-delimited string of <c>UserId</c> values representing all the users who contributed
    /// changes to the document in this <c>PutFile</c> request. Optional; surfaced via
    /// <see cref="WopiHost.Abstractions.IWopiHostExtensions.OnPutFileAsync"/>.
    /// </summary>
    public const string Editors = "X-WOPI-Editors";

    /// <summary>
    /// Marker request header on <c>PutRelativeFile</c> whose <em>presence</em> indicates the call
    /// is being made in the context of a binary document conversion. The header value itself is
    /// not significant per the spec — only its presence. Surfaced via
    /// <see cref="WopiHost.Abstractions.IWopiHostExtensions.OnPutRelativeFileAsync"/>.
    /// </summary>
    public const string FileConversion = "X-WOPI-FileConversion";

    /// <summary>
    /// Optional integer request header on <c>PutRelativeFile</c> announcing the size in bytes of
    /// the file being uploaded. Surfaced via <see cref="WopiHost.Abstractions.IWopiHostExtensions.OnPutRelativeFileAsync"/>.
    /// </summary>
    public const string Size = "X-WOPI-Size";

    /// <summary>
    /// An optional string value indicating the cause of a lock failure.
    /// The header may be included in a <c>409 Conflict</c> response.
    /// Its format is not standardized, and it must only be used for logging purposes.
    /// </summary>
    public const string LockFailureReason = "X-WOPI-LockFailureReason";

    /// <summary>
    /// A UTF-7 string specifying either a file extension or a full file name.
    /// If only the extension is provided, the name of the initial file without extension SHOULD be combined with the extension to create the proposed name.
    /// The WOPI server MUST modify the proposed name as needed to create a new file that is both legally named and does not overwrite any existing file,
    /// while preserving the file extension.
    /// This header MUST be present if <see cref="RelativeTarget"/> is not present.
    /// </summary>
    public const string SuggestedTarget = "X-WOPI-SuggestedTarget";

    /// <summary>
    /// A UTF-7 string that specifies a full file name. The WOPI server MUST NOT modify the name to fulfill the request.
    /// When a file with the specified name already exists, if the <see cref="OverwriteRelativeTarget"/> request header is set to <see langword="false"/>,
    /// or if it is set to <see langword="true"/> and the file is locked, the host MUST respond with <c>409 Conflict</c>.
    /// </summary>
    public const string RelativeTarget = "X-WOPI-RelativeTarget";

    /// <summary>
    /// A Boolean value that specifies whether the host MUST overwrite the file name if it exists.
    /// </summary>
    public const string OverwriteRelativeTarget = "X-WOPI-OverwriteRelativeTarget";

    /// <summary>
    /// A valid file name the host may return on a <c>400 Bad Request</c> or <c>409 Conflict</c> response to a
    /// <see cref="RelativeTarget"/> request, so the client can retry with it. UTF-7 encoded.
    /// </summary>
    public const string ValidRelativeTarget = "X-WOPI-ValidRelativeTarget";

    /// <summary>
    /// The correlation ID that Office for the web attaches to every WOPI request it makes to a host.
    /// </summary>
    public const string CorrelationId = "X-WOPI-CorrelationID";

    /// <summary>
    /// The unique session ID Office for the web creates whenever an action URL is navigated to.
    /// It is returned in the action URL response, sent on every subsequent browser request to Office for the web,
    /// and included as <c>wdUserSession</c> in every PostMessage sent to the host page.
    /// </summary>
    public const string SessionId = "X-UserSessionId";

    /// <summary>
    /// An optional integer specifying the upper bound of the expected size of the file being requested.
    /// The host should use the maximum value of a 4-byte integer if this value is not set in the request.
    /// If the file requested is larger than this value, the host must respond with <c>412 Precondition Failed</c>.
    /// </summary>
    public const string MaxExpectedSize = "X-WOPI-MaxExpectedSize";

    /// <summary>
    /// The WopiSrc of the file or container, sent on <c>POST /wopibootstrapper</c>.
    /// </summary>
    public const string WopiSrc = "X-WOPI-WopiSrc";

    /// <summary>
    /// The bootstrapper operation requested on <c>POST /wopibootstrapper</c>, such as <c>GET_NEW_ACCESS_TOKEN</c>.
    /// </summary>
    public const string EcosystemOperation = "X-WOPI-EcosystemOperation";

    /// <summary>
    /// A string representing the host-specific file identifier for a file. Used by
    /// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getfilewopisrc">GetFileWopiSrc</see>.
    /// </summary>
    public const string HostNativeFileName = "X-WOPI-HostNativeFileName";

    /// <summary>
    /// An optional string value indicating the version of the file.
    /// </summary>
    public const string ItemVersion = "X-WOPI-ItemVersion";

    /// <summary>
    /// A string describing the reason a <c>CreateChildContainer</c> or <c>RenameContainer</c> operation could not be completed.
    /// </summary>
    public const string InvalidContainerName = "X-WOPI-InvalidContainerNameError";

    /// <summary>
    /// A string describing the reason the rename operation couldn't be completed.
    /// </summary>
    public const string InvalidFileName = "X-WOPI-InvalidFileNameError";

    /// <summary>
    /// A UTF-7 encoded string that is the requested new name on <c>RenameFile</c> (without the file extension) and <c>RenameContainer</c>.
    /// </summary>
    public const string RequestedName = "X-WOPI-RequestedName";

    /// <summary>
    /// A string value that the host must use to filter the returned child files.
    /// The header is a list of comma-separated file extensions with a leading dot (<c>.</c>),
    /// with no whitespace, no trailing comma, and no wildcard characters.
    /// </summary>
    public const string FileExtensionFilterList = "X-WOPI-FileExtensionFilterList";

    /// <summary>
    /// The share URL type requested on the <c>GetShareUrl</c> operation — one of
    /// <see cref="WopiHost.Abstractions.WopiShareUrlTypes"/> (e.g. <c>ReadOnly</c>, <c>ReadWrite</c>).
    /// </summary>
    public const string UrlType = "X-WOPI-UrlType";

    /// <summary>
    /// A Base64-encoded string indicating a cryptographic signature of the request.
    /// Used to validate that the request originated from the WOPI client.
    /// </summary>
    public const string Proof = "X-WOPI-Proof";

    /// <summary>
    /// A Base64-encoded string indicating a cryptographic signature of the request
    /// using the old public key. Used when proof keys are being rotated.
    /// </summary>
    public const string ProofOld = "X-WOPI-ProofOld";

    /// <summary>
    /// A 64-bit integer (expressed as a string) that represents the time that the request was signed.
    /// The default proof validator rejects requests signed more than 20 minutes ago.
    /// </summary>
    public const string Timestamp = "X-WOPI-TimeStamp";
}
