namespace WopiHost.Core
{
    public class WopiHeaders
    {

        public const string WOPI_OVERRIDE = "X-WOPI-Override";

        /// <summary>
        /// A string value identifying the current lock on the file. This header must always be included when responding to the request with 409 Conflict. It should not be included when responding to the request with 200 OK.
        /// </summary>
		public const string LOCK = "X-WOPI-Lock";
        public const string OLD_LOCK = "X-WOPI-OldLock";
        public const string LOCK_FAILURE_REASON = "X-WOPI-LockFailureReason";
        public const string LOCKED_BY_OTHER_INTERFACE = "X-WOPI-LockedByOtherInterface";

        public const string SUGGESTED_TARGET = "X-WOPI-SuggestedTarget";
        public const string RELATIVE_TARGET = "X-WOPI-RelativeTarget";
        public const string OVERWRITE_RELATIVE_TARGET = "X-WOPI-OverwriteRelativeTarget";

        public const string CORRELATION_ID = "X-WOPI-CorrelationID";

        public const string MAX_EXPECTED_SIZE = "X-WOPI-MaxExpectedSize";

        public const string WOPI_SRC = "X-WOPI-WopiSrc";

        public const string ECOSYSTEM_OPERATION = "X-WOPI-EcosystemOperation";
    }
}
