using Microsoft.AspNetCore.Mvc;

namespace WopiHost.Core.Results;

/// <summary>
/// The server either does not recognize the request method, or it lacks the ability to fulfill the request.
/// </summary>
public class NotImplementedResult : StatusCodeResult
{
    /// <summary>
    /// Creates an instance of <see cref="NotImplementedResult"/>
    /// </summary>
    public NotImplementedResult() : base(501)
    {
    }
}
