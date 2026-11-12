// Response union of the endpoints that create a file through IWopiNewChildFileNegotiator
// (PutRelativeFile, CreateChildFile): 404, 200 ChildFile, 400/409 from name negotiation,
// lock-mismatch when the target exists and is locked, and 412/413/500/501 status codes.
global using NewChildFileEndpointResult = Microsoft.AspNetCore.Http.HttpResults.Results<
    Microsoft.AspNetCore.Http.HttpResults.NotFound,
    Microsoft.AspNetCore.Http.HttpResults.JsonHttpResult<WopiHost.Core.Models.ChildFile>,
    Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult,
    Microsoft.AspNetCore.Http.HttpResults.BadRequest,
    Microsoft.AspNetCore.Http.HttpResults.Conflict,
    WopiHost.Core.Results.WopiLockMismatchResult>;
