using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;

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
    /// <remarks>
    /// When <paramref name="existingLock"/> is null, the X-WOPI-Lock header is set to the
    /// empty-lock placeholder (see <see cref="WopiHostOptions.EmptyLockHeaderValue"/>). The value
    /// is resolved from <see cref="HttpContext.RequestServices"/> at construction time so hosts
    /// running under IIS in-process can opt back into the historic " " (single-space) workaround
    /// without recompiling. Falls back to <see cref="WopiHeaders.EMPTY_LOCK_VALUE"/> (empty
    /// string, spec-compliant) when no service provider is available.
    /// </remarks>
    public LockMismatchResult(HttpResponse response, string? existingLock = null, string? reason = null) : base()
    {
        var emptyValue = response.HttpContext.RequestServices?
            .GetService<IOptions<WopiHostOptions>>()?.Value.EmptyLockHeaderValue
            ?? WopiHeaders.EMPTY_LOCK_VALUE;
        response.Headers[WopiHeaders.LOCK] = existingLock ?? emptyValue;
        if (!string.IsNullOrEmpty(reason))
        {
            Reason = reason;
            response.Headers[WopiHeaders.LOCK_FAILURE_REASON] = reason;
        }
    }
}
