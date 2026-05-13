using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Action filter that validates the WOPI proof-key signature on incoming requests, asserting
/// that the request originated from a legitimate WOPI client (Microsoft 365 for the web, Office
/// Online Server, etc.).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Pipeline ordering — load-bearing.</strong> The proof signature is computed over the
/// validated <c>access_token</c>, so this filter <em>must</em> run after the authentication
/// middleware (which materializes the <see cref="System.Security.Claims.ClaimsPrincipal"/>) and
/// after <c>[Authorize]</c>. MVC's filter pipeline guarantees this by construction —
/// authentication is middleware, authorization filters run before action filters, and this
/// filter is an action filter. The first defensive check below makes the dependency explicit
/// so a future refactor that moves things around fails loudly instead of silently letting
/// proof-validated-but-unauthenticated requests through.
/// </para>
/// <para>
/// <strong>Response codes.</strong> The WOPI proof-keys spec mandates 500 Internal Server Error
/// when a signature fails verification (see
/// <see href="https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/online/scenarios/proofkeys">
/// Verify that requests originate from Microsoft 365 for the web by using proof keys</see>).
/// That 500 is specifically for "the signature is invalid" — pipeline-misconfiguration paths
/// (unauthenticated request reaching this filter) return 401 because there is no validated
/// principal at all, which is a different failure mode than "valid principal, bad signature."
/// </para>
/// </remarks>
/// <param name="proofValidator">The service used to validate WOPI proof keys.</param>
/// <param name="logger">Logger instance.</param>
[AttributeUsage(AttributeTargets.Class)]
public partial class WopiOriginValidationActionFilter(IWopiProofValidator proofValidator, ILogger<WopiOriginValidationActionFilter> logger) : Attribute, IAsyncActionFilter
{
    /// <summary>
    /// Validates the WOPI proof-key signature before letting the request reach the controller.
    /// </summary>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        // Defense-in-depth against pipeline misconfiguration: reaching this filter without an
        // authenticated principal means [Authorize] was removed, the auth scheme is misregistered,
        // or the filter was attached to a controller that doesn't authenticate at all. The 500
        // mandated by the proof-keys spec only applies to invalid signatures — this is a
        // different failure (no validated principal), so 401 is correct.
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            LogUnauthenticatedRequestReached(logger);
            context.Result = new UnauthorizedResult();
            return;
        }

        string accessToken = context.HttpContext.Request.GetAccessToken();
        if (string.IsNullOrEmpty(accessToken))
        {
            LogAccessTokenMissing(logger);
            WopiTelemetry.ProofValidationFailures.Add(1,
                new KeyValuePair<string, object?>("reason", "access_token_missing"));
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        var validated = await proofValidator.ValidateProofAsync(context.HttpContext, accessToken).ConfigureAwait(false);
        if (!validated)
        {
            LogProofRejected(logger);
            // Note: ProofValidationFailures counter is incremented inside WopiProofValidator
            // with a more specific reason tag; we don't double-count here.
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        await next().ConfigureAwait(false);
    }
}
