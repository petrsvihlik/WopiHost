using Microsoft.AspNetCore.Mvc;

namespace WopiHost.Core.Results
{
    /// <summary>
    /// Indicates that the request could not be processed because of conflict in the request, such as an edit conflict between multiple simultaneous updates.
    /// </summary>
    public class ConflictResult : StatusCodeResult
    {
        public ConflictResult() : base(409)
        {
        }
    }
}
