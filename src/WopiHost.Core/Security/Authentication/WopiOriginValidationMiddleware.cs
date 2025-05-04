using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Middleware that validates the origin of WOPI requests by checking proof keys.
/// </summary>
public class WopiOriginValidationMiddleware : IMiddleware
{
    private readonly IWopiProofValidator _proofValidator;
    private readonly ILogger<WopiOriginValidationMiddleware> _logger;

    /// <summary>
    /// Creates a new instance of the <see cref="WopiOriginValidationMiddleware"/> class.
    /// </summary>
    /// <param name="proofValidator">The service used to validate WOPI proof keys.</param>
    /// <param name="logger">Logger instance.</param>
    public WopiOriginValidationMiddleware(IWopiProofValidator proofValidator, ILogger<WopiOriginValidationMiddleware> logger)
    {
        _proofValidator = proofValidator;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Check if the request has proof headers
        if (!context.Request.Headers.ContainsKey(WopiHeaders.PROOF) || 
            !context.Request.Headers.ContainsKey(WopiHeaders.TIMESTAMP))
        {
            _logger.LogWarning("Request is missing required proof headers and will be rejected");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return;
        }

        string accessToken = context.Request.GetAccessToken();
        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("Access token is missing from the request");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return;
        }

        // Validate the proof headers
        bool isValid = await _proofValidator.ValidateProofAsync(context.Request, accessToken);
        
        if (!isValid)
        {
            _logger.LogWarning("WOPI proof validation failed, request will be rejected");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return;
        }
        
        _logger.LogInformation("WOPI proof validation succeeded");
        await next(context);
    }
} 