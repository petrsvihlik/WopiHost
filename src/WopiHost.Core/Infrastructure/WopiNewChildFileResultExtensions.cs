using Microsoft.AspNetCore.Http;
using WopiHost.Abstractions;
using WopiHost.Core.Results;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Maps a failed <see cref="WopiNewChildFileResult"/> to its WOPI response. Lives in
/// <c>WopiHost.Core</c> so the negotiator abstraction stays free of any web-framework type.
/// </summary>
internal static class WopiNewChildFileResultExtensions
{
    /// <summary>
    /// Maps a failed negotiation to its endpoint result, writing any spec-mandated response headers
    /// on <paramref name="response"/>.
    /// </summary>
    /// <param name="result">The failed negotiation result.</param>
    /// <param name="response">The HTTP response to write headers on.</param>
    /// <returns>The endpoint result for the failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="response"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="result"/> is a <see cref="WopiNewChildFileResult.Success"/>.</exception>
    public static NewChildFileEndpointResult ToFailureResult(this WopiNewChildFileResult result, HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(response);

        return result switch
        {
            WopiNewChildFileResult.Success => throw new ArgumentException("A successful negotiation has no failure response.", nameof(result)),
            // Spec: 400 MAY carry X-WOPI-ValidRelativeTarget so the client can auto-retry with a
            // sanitised name.
            WopiNewChildFileResult.BadRequest(var suggestion) => WithValidRelativeTarget(response, suggestion, TypedResults.BadRequest()),
            WopiNewChildFileResult.Conflict(var suggestion) => WithValidRelativeTarget(response, suggestion, TypedResults.Conflict()),
            WopiNewChildFileResult.Locked(var lockId) => new WopiLockMismatchResult(lockId, reason: "File already exists and is currently locked"),
            WopiNewChildFileResult.InternalError => TypedResults.StatusCode(StatusCodes.Status500InternalServerError),
            // Spec: 501 when the caller may invoke PutRelativeFile but not overwrite the existing
            // target. No spec-defined response headers; the status code carries the signal.
            WopiNewChildFileResult.NotImplemented => TypedResults.StatusCode(StatusCodes.Status501NotImplemented),
        };
    }

    private static TResult WithValidRelativeTarget<TResult>(HttpResponse response, string? suggestion, TResult result)
    {
        if (suggestion is not null)
        {
            response.Headers[WopiHeaders.ValidRelativeTarget] = UtfString.FromDecoded(suggestion).ToString(true);
        }
        return result;
    }
}
