using Microsoft.AspNetCore.Http;
using WopiHost.Abstractions;
using WopiHost.Core.Results;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Translates a <see cref="WopiNewChildFileResult"/> from <see cref="IWopiNewChildFileNegotiator"/>
/// into an <see cref="IResult"/> + response-header writes for the failure outcomes. Lives in
/// <c>WopiHost.Core</c> so the negotiator abstraction stays free of any web-framework type.
/// </summary>
internal static class WopiNewChildFileResultExtensions
{
    /// <summary>
    /// Returns the <see cref="IResult"/> the handler should return for non-success outcomes,
    /// writing any spec-mandated response headers on <paramref name="response"/> as a side
    /// effect. Returns <see langword="null"/> when the negotiation succeeded — caller reads
    /// <see cref="WopiNewChildFileResult.File"/> and proceeds.
    /// </summary>
    public static IResult? ToErrorResult(this WopiNewChildFileResult result, HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(response);

        switch (result.Outcome)
        {
            case WopiNewChildFileOutcome.Success:
                return null;
            case WopiNewChildFileOutcome.BadRequest:
                return TypedResults.BadRequest();
            case WopiNewChildFileOutcome.Conflict:
                response.Headers[WopiHeaders.VALID_RELATIVE_TARGET] = UtfString.FromDecoded(result.ValidRelativeTargetSuggestion!).ToString(true);
                return TypedResults.Conflict();
            case WopiNewChildFileOutcome.Locked:
                return new WopiLockMismatchResult(result.ExistingLockId!, reason: "File already exists and is currently locked");
            case WopiNewChildFileOutcome.InternalError:
                return TypedResults.StatusCode(StatusCodes.Status500InternalServerError);
            default:
                throw new InvalidOperationException($"Unknown {nameof(WopiNewChildFileOutcome)}: {result.Outcome}");
        }
    }
}
