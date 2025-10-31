using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using WopiHost.Core.Extensions;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Action Filter running on Core /wopi Controllers, that validates the origin of WOPI requests by checking proof keys.
/// </summary>
/// <remarks>
/// Creates a new instance of the <see cref="WopiOriginValidationActionFilter"/> class.
/// </remarks>
/// <param name="proofValidator">The service used to validate WOPI proof keys.</param>
/// <param name="logger">Logger instance.</param>
[AttributeUsage(AttributeTargets.Class)]
public class WopiOriginValidationActionFilter(IWopiProofValidator proofValidator, ILogger<WopiOriginValidationActionFilter> logger) : Attribute, IAsyncActionFilter
{
    /// <summary>
    /// Execute pipeline before any Controller for /wopi endpoints
    /// </summary>
    /// <param name="context"></param>
    /// <param name="next"></param>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        string accessToken = context.HttpContext.Request.GetAccessToken();
        if (string.IsNullOrEmpty(accessToken))
        {
            logger.LogWarning("Access token is missing from the request");
            context.HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return;
        }
        
        var validated = await proofValidator.ValidateProofAsync(context.HttpContext, accessToken); 
        if (!validated)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        await next();
    }
    
}