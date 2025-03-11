using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WopiHost.Core.Results;

/// <summary>
/// The server encountered an unexpected condition that prevented it from fulfilling the request.
/// </summary>
public class InternalServerErrorResult : StatusCodeResult
{
    /// <summary>
    /// Creates an instance of <see cref="InternalServerErrorResult"/>
    /// </summary>
    public InternalServerErrorResult() : base(StatusCodes.Status500InternalServerError)
    {
    }
}
