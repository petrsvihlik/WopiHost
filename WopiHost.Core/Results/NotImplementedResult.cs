using Microsoft.AspNetCore.Mvc;

namespace WopiHost.Core.Results
{
    /// <summary>
    /// The server either does not recognize the request method, or it lacks the ability to fulfill the request.
    /// </summary>
    public class NotImplementedResult : StatusCodeResult
    {
        public NotImplementedResult() : base(501)
        {
        }
    }
}
