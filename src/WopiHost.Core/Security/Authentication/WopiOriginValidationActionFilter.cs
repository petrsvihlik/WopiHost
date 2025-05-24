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
public class WopiOriginValidationActionFilter : Attribute, IAsyncActionFilter
{
    private readonly IWopiProofValidator _proofValidator;
    private readonly ILogger<WopiOriginValidationActionFilter> _logger;
    
    /// <summary>
    /// Creates a new instance of the <see cref="WopiOriginValidationActionFilter"/> class.
    /// </summary>
    /// <param name="proofValidator">The service used to validate WOPI proof keys.</param>
    /// <param name="logger">Logger instance.</param>
    public WopiOriginValidationActionFilter(IWopiProofValidator proofValidator, ILogger<WopiOriginValidationActionFilter> logger)
    {
        _proofValidator = proofValidator;
        _logger = logger;
    }
    
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
            _logger.LogWarning("Access token is missing from the request");
            context.HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return;
        }
        
        var validated = await _proofValidator.ValidateProofAsync(context.HttpContext, accessToken); 
        if (!validated)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        await next();
    }
    
}