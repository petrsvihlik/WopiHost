using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WopiHost.Abstractions;
using WopiHost.Core.Results;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Translates a <see cref="WopiNewChildFileResult"/> from <see cref="IWopiNewChildFileNegotiator"/>
/// into the WOPI-spec <see cref="IActionResult"/> + response-header writes for the failure
/// outcomes. Lives in <c>WopiHost.Core</c> so the negotiator abstraction stays free of
/// <c>Microsoft.AspNetCore.Mvc</c>.
/// </summary>
internal static class WopiNewChildFileResultExtensions
{
    /// <summary>
    /// Returns the <see cref="IActionResult"/> the controller should return for non-success
    /// outcomes, writing any spec-mandated response headers on <paramref name="response"/> as
    /// a side effect. Returns <see langword="null"/> when the negotiation succeeded — caller
    /// reads <see cref="WopiNewChildFileResult.File"/> and proceeds.
    /// </summary>
    public static IActionResult? ToErrorActionResult(this WopiNewChildFileResult result, HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(response);

        switch (result.Outcome)
        {
            case WopiNewChildFileOutcome.Success:
                return null;

            case WopiNewChildFileOutcome.BadRequest:
                // 400 Bad Request – the proposed name failed validation, or both headers missing.
                return new BadRequestResult();

            case WopiNewChildFileOutcome.Conflict:
                // 409 Conflict — host advertises the deduplicated alternative via X-WOPI-
                // ValidRelativeTarget so the WOPI client can retry with the new name.
                response.Headers[WopiHeaders.VALID_RELATIVE_TARGET] = UtfString.FromDecoded(result.ValidRelativeTargetSuggestion!).ToString(true);
                return new ConflictResult();

            case WopiNewChildFileOutcome.Locked:
                // 409 Conflict + X-WOPI-Lock — existing target is currently WOPI-locked.
                return new LockMismatchResult(response, result.ExistingLockId!, reason: "File already exists and is currently locked");

            case WopiNewChildFileOutcome.InternalError:
                // Provider returned null from a create call that's contractually expected to
                // succeed. Defensive — the in-tree providers don't trip this path.
                return new InternalServerErrorResult();

            default:
                throw new InvalidOperationException($"Unknown {nameof(WopiNewChildFileOutcome)}: {result.Outcome}");
        }
    }
}
