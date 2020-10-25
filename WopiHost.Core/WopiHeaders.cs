namespace WopiHost.Core
{
    public class WopiHeaders
    {
        /// <summary>
        /// A string specifying the requested operation from the WOPI server.
        /// </summary>
        public const string WOPI_OVERRIDE = "X-WOPI-Override";

        /// <summary>
        /// A string value identifying the current lock on the file. This header must always be included when responding to the request with 409 Conflict. It should not be included when responding to the request with 200 OK.
        /// </summary>
		public const string LOCK = "X-WOPI-Lock";


        public const string OLD_LOCK = "X-WOPI-OldLock";
        public const string LOCK_FAILURE_REASON = "X-WOPI-LockFailureReason";
        public const string LOCKED_BY_OTHER_INTERFACE = "X-WOPI-LockedByOtherInterface";

        /// <summary>
        /// A UTF-7 string specifying either a file extension or a full file name.
        /// If only the extension is provided, the name of the initial file without extension SHOULD be combined with the extension to create the proposed name.
        /// The WOPI server MUST modify the proposed name as needed to create a new file that is both legally named and does not overwrite any existing file, while preserving the file extension.
        /// This header MUST be present if X-WOPI-RelativeTarget is not present.
        /// </summary>
        public const string SUGGESTED_TARGET = "X-WOPI-SuggestedTarget";

        /// <summary>
        /// A UTF-7 string that specifies a full file name. The WOPI server MUST NOT modify the name to fulfill the request. When a file with the specified name already exists, if the X-WOPI-OverwriteRelativeTarget request header is set to false, or if the X-WOPI-OverwriteRelativeTarget request header is set to true and the file is locked, the host MUST respond with a 409 status code.
        /// </summary>
        public const string RELATIVE_TARGET = "X-WOPI-RelativeTarget";

        /// <summary>
        /// A Boolean value that specifies whether the host MUST overwrite the file name if it exists.
        /// </summary>
        public const string OVERWRITE_RELATIVE_TARGET = "X-WOPI-OverwriteRelativeTarget";

        /// <summary>
        /// Every WOPI request Office for the web makes to a host will have an ID called the correlation ID. This ID will be included in the WOPI request using the X-WOPI-CorrelationId request header.
        /// </summary>
        public const string CORRELATION_ID = "X-WOPI-CorrelationID";

        /// <summary>
        /// Whenever an action URL is navigated to, Office for the web creates a unique session ID. This session ID allows Microsoft engineers to quickly retrieve all server logs related to that session, including information about the WOPI calls that were made to the host. The session ID is passed back in the WOPI action URL HTTP response in the X-UserSessionId response header. It is also passed on every subsequent request made by the browser to Office Online in the X-UserSessionId request header, and it is included in all PostMessages sent from |wac| to the host page in the wdUserSession value.
        /// </summary>
        public const string SESSION_ID = "X-UserSessionId";

        public const string MAX_EXPECTED_SIZE = "X-WOPI-MaxExpectedSize";

        public const string WOPI_SRC = "X-WOPI-WopiSrc";

        public const string ECOSYSTEM_OPERATION = "X-WOPI-EcosystemOperation";
    }
}
