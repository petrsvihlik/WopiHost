namespace WopiHost.Core.Infrastructure;

/// <summary>
/// WOPI HTTP header names.
/// </summary>
public static class WopiHeaders
{
    /// <summary>
    /// A string specifying the requested operation from the WOPI server.
    /// </summary>
    public const string WOPI_OVERRIDE = "X-WOPI-Override";

    /// <summary>
    /// A string value identifying the current lock on the file. 
    /// This header must always be included when responding to the request with 409 Conflict. 
    /// It should not be included when responding to the request with 200 OK.
    /// </summary>
	public const string LOCK = "X-WOPI-Lock";

    /// <summary>
    /// Value to use when no lock exists but header must be included.
    /// This is a workaround for IIS/Azure Web App behavior that removes headers with empty values.
    /// See: https://github.com/petrsvihlik/WopiHost/issues/208
    /// Reference: https://github.com/dotnet/aspnetcore/blob/df0c4597422b0e7592118cb9c7e40fa820d2ce0a/src/Servers/IIS/IIS/src/Core/IISHttpContext.cs#L608
    /// </summary>
    public const string EMPTY_LOCK_VALUE = " ";

    /// <summary>
    /// A string provided by the WOPI client that is the existing lock on the file. 
    /// Required. Note that if X-WOPI-OldLock is not provided, the request is identical to a Lock request.
    /// </summary>
    public const string OLD_LOCK = "X-WOPI-OldLock";

    /// <summary>
    /// An optional string value indicating the cause of a lock failure. 
    /// This header may be included when responding to the request with 409 Conflict. 
    /// There is no standard for how this string is formatted, and it must only be used for logging purposes.
    /// </summary>
    public const string LOCK_FAILURE_REASON = "X-WOPI-LockFailureReason";

    /// <summary>
    /// A string indicating that the file is currently locked by another client. T
    /// his header SHOULD only be included when responding with the 409 status code.
    /// Note: This header is deprecated and should be ignored by WOPI clients.
    /// </summary>
    [Obsolete("This header is deprecated and should be ignored by WOPI clients.", false)]
    public const string LOCKED_BY_OTHER_INTERFACE = "X-WOPI-LockedByOtherInterface";

    /// <summary>
    /// A UTF-7 string specifying either a file extension or a full file name.
    /// If only the extension is provided, the name of the initial file without extension SHOULD be combined with the extension to create the proposed name.
    /// The WOPI server MUST modify the proposed name as needed to create a new file that is both legally named and does not overwrite any existing file, 
    /// while preserving the file extension.
    /// This header MUST be present if <see cref="RELATIVE_TARGET"/> is not present.
    /// </summary>
    public const string SUGGESTED_TARGET = "X-WOPI-SuggestedTarget";

    /// <summary>
    /// A UTF-7 string that specifies a full file name. The WOPI server MUST NOT modify the name to fulfill the request. 
    /// When a file with the specified name already exists, if the <see cref="OVERWRITE_RELATIVE_TARGET"/> request header is set to false, 
    /// or if the <see cref="OVERWRITE_RELATIVE_TARGET"/> request header is set to true and the file is locked, the host MUST respond with a 409 status code.
    /// </summary>
    public const string RELATIVE_TARGET = "X-WOPI-RelativeTarget";

    /// <summary>
    /// A Boolean value that specifies whether the host MUST overwrite the file name if it exists.
    /// </summary>
    public const string OVERWRITE_RELATIVE_TARGET = "X-WOPI-OverwriteRelativeTarget";

    /// <summary>
    /// The host may include an X-WOPI-ValidRelativeTarget specifying a container/file name that is valid when creating using RelativeTarget that already exists
    /// </summary>
    public const string VALID_RELATIVE_TARGET = "X-WOPI-ValidRelativeTarget";

    /// <summary>
    /// Every WOPI request Office for the web makes to a host will have an ID called the correlation ID. 
    /// This ID will be included in the WOPI request using the X-WOPI-CorrelationId request header.
    /// </summary>
    public const string CORRELATION_ID = "X-WOPI-CorrelationID";

    /// <summary>
    /// Whenever an action URL is navigated to, Office for the web creates a unique session ID. 
    /// This session ID allows Microsoft engineers to quickly retrieve all server logs related to that session, 
    /// including information about the WOPI calls that were made to the host. 
    /// The session ID is passed back in the WOPI action URL HTTP response in the X-UserSessionId response header. 
    /// It is also passed on every subsequent request made by the browser to Office Online in the X-UserSessionId request header, 
    /// and it is included in all PostMessages sent from |wac| to the host page in the wdUserSession value.
    /// </summary>
    public const string SESSION_ID = "X-UserSessionId";

    /// <summary>
    /// An integer specifying the upper bound of the expected size of the file being requested. 
    /// Optional. 
    /// The host should use the maximum value of a 4-byte integer if this value is not set in the request. 
    /// If the file requested is larger than this value, the host must respond with a 412 Precondition Failed.
    /// </summary>
    public const string MAX_EXPECTED_SIZE = "X-WOPI-MaxExpectedSize";

    /// <summary>
    ///  The WopiSrc (a string) for the file or container. Used for:
    ///  POST /wopibootstrapper
    /// </summary>
    public const string WOPI_SRC = "X-WOPI-WopiSrc";

    /// <summary>
    /// The string GET_NEW_ACCESS_TOKEN. Required.
    /// </summary>
    public const string ECOSYSTEM_OPERATION = "X-WOPI-EcosystemOperation";

    /// <summary>
    /// An optional string value indicating the version of the file.
    /// </summary>
    public const string ITEM_VERSION = "X-WOPI-ItemVersion";

    /// <summary>
    /// A string describing the reason the CreateChildContainer operation could not be completed
    /// </summary>
    public const string INVALID_CONTAINER_NAME = "X-WOPI-InvalidContainerNameError";

    /// <summary>
    /// A string describing the reason the rename operation couldn't be completed.
    /// </summary>
    public const string INVALID_FILE_NAME = "X-WOPI-InvalidFileNameError ";

    /// <summary>
    /// A UTF-7 encoded string that is a container name. Required.
    /// </summary>
    public const string REQUESTED_NAME = "X-WOPI-RequestedName";

    /// <summary>
    /// A string value that the host must use to filter the returned child files. 
    /// This header must be a list of comma-separated file extensions with a leading dot (.). 
    /// There must be no whitespace and no trailing comma in the string. 
    /// Wildcard characters are not permitted.
    /// </summary>
    public const string FILE_EXTENSION_FILTER_LIST = "X-WOPI-FileExtensionFilterList";
    
    /// <summary>
    /// A Base64-encoded string indicating a cryptographic signature of the request.
    /// Used to validate that the request originated from Office Online Server.
    /// </summary>
    public const string PROOF = "X-WOPI-Proof";

    /// <summary>
    /// A Base64-encoded string indicating a cryptographic signature of the request
    /// using the old public key. Used when proof keys are being rotated.
    /// </summary>
    public const string PROOF_OLD = "X-WOPI-ProofOld";

    /// <summary>
    /// A 64-bit integer (expressed as a string) that represents the time that the request was signed.
    /// Should not be more than 20 minutes old.
    /// </summary>
    public const string TIMESTAMP = "X-WOPI-TimeStamp";
}
