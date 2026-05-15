using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Validates the WOPI proof-key signature on incoming requests, asserting that the request
/// originated from a legitimate WOPI client (Microsoft 365 for the web, Office Online Server, …).
/// Minimal-API equivalent of <see cref="WopiOriginValidationActionFilter"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Pipeline ordering — load-bearing.</strong> The proof signature is computed over the
/// validated <c>access_token</c>, so this filter <em>must</em> run after authentication has
/// materialised the <see cref="System.Security.Claims.ClaimsPrincipal"/>. In the Minimal-API
/// topology this means the WOPI route group calls <c>RequireAuthorization()</c> before
/// attaching this filter, so authorization (and therefore authentication) is resolved by the
/// time <see cref="InvokeAsync"/> runs.
/// </para>
/// <para>
/// <strong>Response codes.</strong> The WOPI proof-keys spec mandates 500 Internal Server Error
/// when a signature fails verification. That 500 is specifically for "the signature is invalid"
/// — pipeline-misconfiguration paths (unauthenticated request reaching this filter) return 401
/// because there is no validated principal at all, which is a different failure mode than
/// "valid principal, bad signature."
/// </para>
/// </remarks>
internal sealed partial class WopiOriginValidationEndpointFilter(
    IWopiProofValidator proofValidator,
    ILogger<WopiOriginValidationEndpointFilter> logger) : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            LogUnauthenticatedRequestReached(logger);
            return TypedResults.Unauthorized();
        }

        var accessToken = httpContext.Request.GetAccessToken();
        if (string.IsNullOrEmpty(accessToken))
        {
            LogAccessTokenMissing(logger);
            WopiTelemetry.ProofValidationFailures.Add(1,
                new KeyValuePair<string, object?>("reason", "access_token_missing"));
            return TypedResults.StatusCode(StatusCodes.Status500InternalServerError);
        }

        var validated = await proofValidator.ValidateProofAsync(httpContext, accessToken).ConfigureAwait(false);
        if (!validated)
        {
            LogProofRejected(logger);
            return TypedResults.StatusCode(StatusCodes.Status500InternalServerError);
        }

        return await next(context).ConfigureAwait(false);
    }
}
