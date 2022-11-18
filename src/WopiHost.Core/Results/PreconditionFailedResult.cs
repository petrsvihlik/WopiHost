using Microsoft.AspNetCore.Mvc;

namespace WopiHost.Core.Results;

/// <summary>
/// The precondition given in one or more of the request-header fields evaluated to false when it was tested on the server. This response code allows the client to place preconditions on the current resource metainformation (header field data) and thus prevent the requested method from being applied to a resource other than the one intended.
/// </summary>
public class PreconditionFailedResult : StatusCodeResult
{
    /// <summary>
    /// Creates an instance of <see cref="PreconditionFailedResult"/>.
    /// </summary>
    public PreconditionFailedResult() : base(412)
    {
    }
}