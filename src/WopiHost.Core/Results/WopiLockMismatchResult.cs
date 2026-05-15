using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;

namespace WopiHost.Core.Results;

/// <summary>
/// <see cref="IResult"/> for WOPI lock-mismatch responses. Writes the spec-mandated
/// <c>X-WOPI-Lock</c> (and optional <c>X-WOPI-LockFailureReason</c>) response headers and
/// responds with <c>409 Conflict</c>. Implements <see cref="IWopiOutcomeResult"/> so the
/// telemetry endpoint filter classifies the outcome as
/// <see cref="WopiTelemetry.Outcomes.LockMismatch"/> rather than generic <c>Conflict</c>.
/// </summary>
/// <remarks>
/// When <paramref name="existingLock"/> is null, the <c>X-WOPI-Lock</c> header is set to the
/// empty-lock placeholder (see <see cref="WopiHostOptions.EmptyLockHeaderValue"/>). The value
/// is resolved from <see cref="HttpContext.RequestServices"/> at execution time so hosts
/// running under IIS in-process can opt back into the historic single-space workaround without
/// recompiling. Falls back to <see cref="WopiHeaders.EMPTY_LOCK_VALUE"/> (empty string, spec
/// compliant) when no service provider is available.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Phase 3 of #430 migration; HTTP parity tests land in phase 5 (test relocation into WopiHost.IntegrationTests)")]
public sealed class WopiLockMismatchResult(string? existingLock = null, string? reason = null) : IResult, IWopiOutcomeResult
{
    /// <inheritdoc />
    public string Outcome => WopiTelemetry.Outcomes.LockMismatch;

    /// <inheritdoc />
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var emptyValue = httpContext.RequestServices?
            .GetService<IOptions<WopiHostOptions>>()?.Value.EmptyLockHeaderValue
            ?? WopiHeaders.EMPTY_LOCK_VALUE;
        httpContext.Response.Headers[WopiHeaders.LOCK] = existingLock ?? emptyValue;
        if (!string.IsNullOrEmpty(reason))
        {
            httpContext.Response.Headers[WopiHeaders.LOCK_FAILURE_REASON] = reason;
        }
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return Task.CompletedTask;
    }
}
