using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Results;

/// <summary>
/// A string describing the reason the CreateChildContainer operation could not be completed. 
/// This header should only be included when the response code is 400 Bad Request. 
/// This string is only used for logging purposes.
/// </summary>
public class InvalidContainerNameResult : ConflictResult
{
    /// <summary>
    /// Reason message to return.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Creates an instance of <see cref="LockMismatchResult"/>.
    /// </summary>
    public InvalidContainerNameResult(HttpResponse response, string? reason = null) : base()
    {
        if (!string.IsNullOrEmpty(reason))
        {
            Reason = reason;
            response.Headers[WopiHeaders.INVALID_CONTAINER_NAME] = reason;
        }
    }
}
