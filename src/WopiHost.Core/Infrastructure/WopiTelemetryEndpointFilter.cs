using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Wraps every WOPI endpoint in the telemetry envelope: an
/// <see cref="System.Diagnostics.Activity"/> span, a log scope keyed on <c>wopi.file_id</c> /
/// <c>wopi.container_id</c> / <c>wopi.lock_id</c>, an outcome log line, and the
/// <see cref="WopiTelemetry.Requests"/> / <see cref="WopiTelemetry.LockConflicts"/> metric
/// increments. Attached to the <c>/wopi</c> route group inside
/// <see cref="Endpoints.WopiEndpointRouteBuilderExtensions.MapWopiEndpoints"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Operation name resolution.</strong> Read from the endpoint's
/// <see cref="Microsoft.AspNetCore.Routing.EndpointNameMetadata"/> when present (set via
/// <c>.WithName("…")</c>), otherwise from <see cref="Endpoint.DisplayName"/>. Endpoints in the
/// Minimal-API topology should call <c>.WithName(WopiRouteNames.CheckFileInfo)</c> on their
/// canonical routes so the metric dimension stays stable across renames.
/// </para>
/// <para>
/// <strong>Resource kind dispatch.</strong> Read from
/// <see cref="WopiResourceKindMetadata"/>; defaults to <see cref="WopiResourceType.File"/> when
/// absent (matches the historic controller behaviour where files are the common case and
/// Containers / Folders explicitly switch the dimension).
/// </para>
/// <para>
/// <strong>Outcome classification.</strong> Result types implementing
/// <see cref="IWopiOutcomeResult"/> win (the lock-mismatch case — 409 is shared with generic
/// conflict but dashboards need them separated). Otherwise we read the response status code,
/// which is set on <c>HttpResponse</c> by the time <c>next()</c> returns since Minimal-API
/// results are executed synchronously inside the pipeline. Unhandled exceptions surface as
/// <see cref="WopiTelemetry.Outcomes.Cancelled"/> when the request was aborted by the client,
/// otherwise <see cref="WopiTelemetry.Outcomes.Error"/>.
/// </para>
/// </remarks>
internal sealed partial class WopiTelemetryEndpointFilter(ILogger<WopiTelemetryEndpointFilter> logger) : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var endpoint = httpContext.GetEndpoint();
        var operation = ResolveOperation(endpoint);
        var resourceTagKey = endpoint?.Metadata.GetMetadata<WopiResourceKindMetadata>()?.Type == WopiResourceType.Container
            ? WopiTelemetry.Tags.ContainerId
            : WopiTelemetry.Tags.FileId;
        var resourceId = NullIfEmpty(httpContext.Request.RouteValues.TryGetValue("id", out var rawId) ? rawId?.ToString() : null);
        var wopiOverride = NullIfEmpty(httpContext.Request.Headers[WopiHeaders.WOPI_OVERRIDE].ToString());
        var lockId = NullIfEmpty(httpContext.Request.Headers[WopiHeaders.LOCK].ToString());
        var userId = NullIfEmpty(httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier));

        using var activity = WopiTelemetry.StartActivity(operation, resourceId, resourceTagKey, wopiOverride);

        var scopeState = new Dictionary<string, object?> { [WopiTelemetry.Tags.Operation] = operation };
        if (resourceId is not null) scopeState[resourceTagKey] = resourceId;
        if (lockId is not null) scopeState[WopiTelemetry.Tags.LockId] = lockId;
        if (userId is not null) scopeState[WopiTelemetry.Tags.UserId] = userId;
        using var scope = logger.BeginScope(scopeState);

        var outcome = WopiTelemetry.Outcomes.Success;
        try
        {
            var result = await next(context).ConfigureAwait(false);
            outcome = ClassifyResult(result, httpContext.Response.StatusCode);
            LogActionCompleted(logger, operation, resourceId ?? string.Empty, wopiOverride, outcome);
            return result;
        }
        catch (Exception ex) when (IsCancellation(ex, httpContext.RequestAborted))
        {
            outcome = WopiTelemetry.Outcomes.Cancelled;
            LogActionCancelled(logger, operation, resourceId ?? string.Empty);
            throw;
        }
        catch (Exception ex)
        {
            outcome = WopiTelemetry.Outcomes.Error;
            LogActionFailed(logger, ex, operation, resourceId ?? string.Empty);
            throw;
        }
        finally
        {
            WopiTelemetry.RecordOutcome(activity, operation, outcome);
        }
    }

    private static string ResolveOperation(Endpoint? endpoint)
    {
        var name = endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.EndpointNameMetadata>()?.EndpointName;
        if (!string.IsNullOrEmpty(name)) return name;
        if (!string.IsNullOrEmpty(endpoint?.DisplayName)) return endpoint.DisplayName;
        return "Unknown";
    }

    private static bool IsCancellation(Exception ex, CancellationToken requestAborted) =>
        (ex is OperationCanceledException && requestAborted.IsCancellationRequested)
        // AggregateException can wrap an OCE when a Task.WhenAll observes one of several faults.
        || (ex is AggregateException agg && agg.InnerExceptions.All(e => e is OperationCanceledException) && requestAborted.IsCancellationRequested);

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string ClassifyResult(object? result, int responseStatusCode)
    {
        // Custom IResult types implementing IWopiOutcomeResult win — needed for the lock-mismatch
        // case (409 shared with generic conflict but counted separately in dashboards).
        if (result is IWopiOutcomeResult wopiOutcome)
        {
            return wopiOutcome.Outcome;
        }
        return MapStatusCode(responseStatusCode);
    }

    private static string MapStatusCode(int statusCode) => statusCode switch
    {
        >= 200 and < 300 => WopiTelemetry.Outcomes.Success,
        StatusCodes.Status404NotFound => WopiTelemetry.Outcomes.NotFound,
        StatusCodes.Status409Conflict => WopiTelemetry.Outcomes.Conflict,
        StatusCodes.Status400BadRequest => WopiTelemetry.Outcomes.BadRequest,
        StatusCodes.Status412PreconditionFailed => WopiTelemetry.Outcomes.PreconditionFailed,
        StatusCodes.Status501NotImplemented => WopiTelemetry.Outcomes.NotImplemented,
        _ => WopiTelemetry.Outcomes.Error,
    };
}
