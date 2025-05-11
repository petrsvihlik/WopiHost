using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Results;

/// <summary>
/// Lock mismatch or locked by another interface. 
/// You must always include an X-WOPI-Lock response header containing the value of the current lock on the file when using this response code.
/// </summary>
public class LockMismatchResult : ConflictResult
{
    /// <summary>
    /// Reason message to return.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Creates an instance of <see cref="LockMismatchResult"/>.
    /// </summary>
    public LockMismatchResult(HttpResponse response, string? existingLock = null, string? reason = null) : base()
    {
        response.Headers[WopiHeaders.LOCK] = existingLock ?? WopiHeaders.EMPTY_LOCK_VALUE;
        if (!string.IsNullOrEmpty(reason))
        {
            Reason = reason;
            response.Headers[WopiHeaders.LOCK_FAILURE_REASON] = reason;
        }
    }
}
