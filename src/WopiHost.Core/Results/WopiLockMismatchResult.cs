using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;

namespace WopiHost.Core.Results;

/// <summary>
/// Represents a WOPI lock-mismatch response: <c>409 Conflict</c> with the spec-mandated
/// <c>X-WOPI-Lock</c> (and optional <c>X-WOPI-LockFailureReason</c>) response headers.
/// </summary>
/// <param name="existingLock">The lock currently held on the resource, or <see langword="null"/> if it is unlocked.</param>
/// <param name="reason">The optional <c>X-WOPI-LockFailureReason</c> value, or <see langword="null"/> to omit the header.</param>
/// <remarks>
/// <para>
/// Implements <see cref="IWopiOutcomeResult"/> so the telemetry endpoint filter classifies the
/// outcome as <see cref="WopiTelemetry.Outcomes.LockMismatch"/> rather than generic <c>Conflict</c>.
/// Implements <see cref="IStatusCodeHttpResult"/> and <see cref="IEndpointMetadataProvider"/> so it
/// composes into <c>Results&lt;...&gt;</c> typed-union return types and contributes a 409 response
/// to the generated OpenAPI document without an explicit <c>.Produces(409)</c> call.
/// </para>
/// <para>
/// When <paramref name="existingLock"/> is <see langword="null"/>, the <c>X-WOPI-Lock</c> header is
/// set to <see cref="WopiHostOptions.EmptyLockHeaderValue"/>, resolved from
/// <see cref="HttpContext.RequestServices"/> at execution time so hosts running under IIS in-process
/// can configure the single-space value without recompiling. Falls back to
/// <see cref="WopiHeaders.EmptyLockValue"/> (the spec-compliant empty string) when no service
/// provider is available.
/// </para>
/// </remarks>
public sealed class WopiLockMismatchResult(string? existingLock = null, string? reason = null)
    : IResult, IWopiOutcomeResult, IStatusCodeHttpResult, IEndpointMetadataProvider
{
    /// <inheritdoc />
    public string Outcome => WopiTelemetry.Outcomes.LockMismatch;

    /// <inheritdoc />
    public int? StatusCode => StatusCodes.Status409Conflict;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="httpContext"/> is <see langword="null"/>.</exception>
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var emptyValue = httpContext.RequestServices?
            .GetService<IOptions<WopiHostOptions>>()?.Value.EmptyLockHeaderValue
            ?? WopiHeaders.EmptyLockValue;
        httpContext.Response.Headers[WopiHeaders.Lock] = existingLock ?? emptyValue;
        if (!string.IsNullOrEmpty(reason))
        {
            httpContext.Response.Headers[WopiHeaders.LockFailureReason] = reason;
        }
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status409Conflict, type: null));
    }
}
