using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using WopiHost.Core.Controllers;
using WopiHost.Core.Results;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Wraps every WOPI controller action in the telemetry envelope: an <see cref="System.Diagnostics.Activity"/>
/// span, a log scope keyed on <c>wopi.file_id</c> / <c>wopi.container_id</c> / <c>wopi.lock_id</c>, an
/// outcome log line, and the <see cref="WopiTelemetry.Requests"/> / <see cref="WopiTelemetry.LockConflicts"/>
/// metric increments. Applied via <c>[ServiceFilter]</c> on the WOPI controllers; registered in <c>AddWopi()</c>.
/// </summary>
public sealed partial class WopiTelemetryActionFilter(ILogger<WopiTelemetryActionFilter> logger) : IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var operation = context.ActionDescriptor.RouteValues.TryGetValue("action", out var actionName) && actionName is not null
            ? actionName
            : "Unknown";
        var rawId = context.ActionArguments.TryGetValue("id", out var arg) ? arg?.ToString() : null;
        var resourceId = string.IsNullOrEmpty(rawId) ? null : rawId;
        var resourceTagKey = context.Controller is ContainersController or FoldersController
            ? WopiTelemetry.Tags.ContainerId
            : WopiTelemetry.Tags.FileId;
        var wopiOverride = NullIfEmpty(context.HttpContext.Request.Headers[WopiHeaders.WOPI_OVERRIDE].ToString());
        var lockId = NullIfEmpty(context.HttpContext.Request.Headers[WopiHeaders.LOCK].ToString());
        var userId = NullIfEmpty(context.HttpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier));

        using var activity = WopiTelemetry.StartActivity(operation, resourceId, resourceTagKey, wopiOverride);

        var scopeState = new Dictionary<string, object?> { [WopiTelemetry.Tags.Operation] = operation };
        if (resourceId is not null) scopeState[resourceTagKey] = resourceId;
        if (lockId is not null) scopeState[WopiTelemetry.Tags.LockId] = lockId;
        if (userId is not null) scopeState[WopiTelemetry.Tags.UserId] = userId;
        using var scope = logger.BeginScope(scopeState);

        var outcome = WopiTelemetry.Outcomes.Success;
        try
        {
            var executed = await next().ConfigureAwait(false);
            if (executed.Exception is { } ex)
            {
                outcome = WopiTelemetry.Outcomes.Error;
                LogActionFailed(logger, ex, operation, resourceId ?? string.Empty);
            }
            else
            {
                outcome = ClassifyResult(executed.Result, context.HttpContext.Response.StatusCode);
                LogActionCompleted(logger, operation, resourceId ?? string.Empty, userId, wopiOverride, outcome);
            }
        }
        finally
        {
            WopiTelemetry.RecordOutcome(activity, operation, outcome);
        }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    // Order matters: LockMismatchResult derives from ConflictResult, so the lock case must be checked first.
    private static string ClassifyResult(IActionResult? result, int responseStatusCode) => result switch
    {
        LockMismatchResult => WopiTelemetry.Outcomes.LockMismatch,
        ConflictResult => WopiTelemetry.Outcomes.Conflict,
        NotFoundResult or NotFoundObjectResult => WopiTelemetry.Outcomes.NotFound,
        BadRequestResult or BadRequestObjectResult => WopiTelemetry.Outcomes.BadRequest,
        PreconditionFailedResult => WopiTelemetry.Outcomes.PreconditionFailed,
        NotImplementedResult => WopiTelemetry.Outcomes.NotImplemented,
        InternalServerErrorResult => WopiTelemetry.Outcomes.Error,
        OkResult or OkObjectResult or ObjectResult or ContentResult or JsonResult or Microsoft.AspNetCore.Mvc.FileResult or Results.FileResult => WopiTelemetry.Outcomes.Success,
        StatusCodeResult sc => MapStatusCode(sc.StatusCode),
        _ => MapStatusCode(responseStatusCode),
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
