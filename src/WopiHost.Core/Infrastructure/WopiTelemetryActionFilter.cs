using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using WopiHost.Core.Controllers;
using WopiHost.Core.Results;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Single point that wraps every WOPI controller action with the telemetry envelope:
/// an <see cref="Activity"/> span, a log scope keyed on <c>wopi.file_id</c> / <c>wopi.container_id</c> /
/// <c>wopi.lock_id</c>, an outcome log line, and the <see cref="WopiTelemetry.Requests"/> /
/// <see cref="WopiTelemetry.LockConflicts"/> metric increments.
/// </summary>
/// <remarks>
/// <para>
/// Applied via <c>[ServiceFilter(typeof(WopiTelemetryActionFilter))]</c> on the WOPI controllers in
/// <c>src/WopiHost.Core/Controllers/</c>. Registered as Scoped in <c>AddWopi()</c>.
/// </para>
/// <para>
/// The filter classifies the resulting <see cref="IActionResult"/> into a coarse <c>outcome</c>
/// dimension (see <see cref="WopiTelemetry.Outcomes"/>). Lock conflicts are detected via the
/// <see cref="LockMismatchResult"/> type — checked before the more general <see cref="ConflictResult"/>
/// since the former inherits from the latter. The log scope opens before <c>next()</c> runs so any
/// nested logging (auth handlers, security filters, providers) inherits the WOPI request context.
/// </para>
/// </remarks>
public sealed partial class WopiTelemetryActionFilter(ILogger<WopiTelemetryActionFilter> logger) : IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var operation = context.ActionDescriptor.RouteValues.TryGetValue("action", out var actionName) && actionName is not null
            ? actionName
            : "Unknown";

        var (resourceTagKey, resourceId) = ResolveResource(context);
        var wopiOverride = context.HttpContext.Request.Headers[WopiHeaders.WOPI_OVERRIDE].ToString();
        var lockId = context.HttpContext.Request.Headers[WopiHeaders.LOCK].ToString();
        var userId = context.HttpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        using var activity = WopiTelemetry.StartActivity(
            operation,
            resourceId,
            resourceTagKey,
            string.IsNullOrEmpty(wopiOverride) ? null : wopiOverride);

        var scopeState = BuildScopeState(operation, resourceTagKey, resourceId, lockId, userId);
        using var scope = logger.BeginScope(scopeState);

        var outcome = WopiTelemetry.Outcomes.Success;
        Exception? thrown = null;
        try
        {
            var executed = await next().ConfigureAwait(false);
            thrown = executed.Exception;
            outcome = thrown is null
                ? ClassifyResult(executed.Result, context.HttpContext.Response.StatusCode)
                : WopiTelemetry.Outcomes.Error;
        }
        catch (Exception ex)
        {
            thrown = ex;
            outcome = WopiTelemetry.Outcomes.Error;
            throw;
        }
        finally
        {
            if (thrown is not null)
            {
                LogActionFailed(logger, thrown, operation, resourceId ?? string.Empty);
            }
            else
            {
                LogActionCompleted(
                    logger,
                    operation,
                    resourceId ?? string.Empty,
                    string.IsNullOrEmpty(userId) ? null : userId,
                    string.IsNullOrEmpty(wopiOverride) ? null : wopiOverride,
                    outcome);
            }
            WopiTelemetry.RecordOutcome(activity, operation, outcome);
        }
    }

    private static (string TagKey, string? ResourceId) ResolveResource(ActionExecutingContext context)
    {
        var id = context.ActionArguments.TryGetValue("id", out var raw) ? raw?.ToString() : null;
        var tagKey = context.Controller switch
        {
            FilesController => WopiTelemetry.Tags.FileId,
            ContainersController => WopiTelemetry.Tags.ContainerId,
            FoldersController => WopiTelemetry.Tags.ContainerId,
            _ => WopiTelemetry.Tags.FileId,
        };
        return (tagKey, string.IsNullOrEmpty(id) ? null : id);
    }

    private static Dictionary<string, object?> BuildScopeState(
        string operation,
        string resourceTagKey,
        string? resourceId,
        string lockId,
        string? userId)
    {
        var state = new Dictionary<string, object?>(capacity: 4)
        {
            [WopiTelemetry.Tags.Operation] = operation,
        };
        if (!string.IsNullOrEmpty(resourceId))
        {
            state[resourceTagKey] = resourceId;
        }
        if (!string.IsNullOrEmpty(lockId))
        {
            state[WopiTelemetry.Tags.LockId] = lockId;
        }
        if (!string.IsNullOrEmpty(userId))
        {
            state[WopiTelemetry.Tags.UserId] = userId;
        }
        return state;
    }

    /// <summary>
    /// Classify the action result into a <see cref="WopiTelemetry.Outcomes"/> string.
    /// Order matters: <see cref="LockMismatchResult"/> derives from <see cref="ConflictResult"/>,
    /// so the lock case must be checked first.
    /// </summary>
    private static string ClassifyResult(IActionResult? result, int responseStatusCode) => result switch
    {
        LockMismatchResult => WopiTelemetry.Outcomes.LockMismatch,
        ConflictResult => WopiTelemetry.Outcomes.Conflict,
        NotFoundResult or NotFoundObjectResult => WopiTelemetry.Outcomes.NotFound,
        BadRequestResult or BadRequestObjectResult => WopiTelemetry.Outcomes.BadRequest,
        PreconditionFailedResult => WopiTelemetry.Outcomes.PreconditionFailed,
        NotImplementedResult => WopiTelemetry.Outcomes.NotImplemented,
        InternalServerErrorResult => WopiTelemetry.Outcomes.Error,
        StatusCodeResult sc => MapStatusCode(sc.StatusCode),
        ObjectResult or ContentResult or JsonResult or OkResult or OkObjectResult or Microsoft.AspNetCore.Mvc.FileResult or Results.FileResult => WopiTelemetry.Outcomes.Success,
        null => MapStatusCode(responseStatusCode),
        _ => WopiTelemetry.Outcomes.Success,
    };

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
