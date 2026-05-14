using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Core.Controllers;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Results;

namespace WopiHost.Core.Tests.Infrastructure;

/// <summary>
/// Tests for <see cref="WopiTelemetryActionFilter"/>.
/// Each test runs the filter against a synthetic <see cref="ActionExecutingContext"/> with a
/// fake controller and a stubbed <c>next()</c> delegate that returns the desired
/// <see cref="IActionResult"/>. Assertions verify the recorded outcome via the activity tag
/// (the easiest single observable that proves the classification + RecordOutcome path ran).
/// </summary>
[Collection(WopiTelemetryCollection.Name)]
public sealed class WopiTelemetryActionFilterTests : IDisposable
{
    private readonly ActivityListener _listener;

    public WopiTelemetryActionFilterTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WopiTelemetry.Name,
            Sample = (ref _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Theory]
    [InlineData(typeof(OkResult), WopiTelemetry.Outcomes.Success)]
    [InlineData(typeof(NotFoundResult), WopiTelemetry.Outcomes.NotFound)]
    [InlineData(typeof(BadRequestResult), WopiTelemetry.Outcomes.BadRequest)]
    [InlineData(typeof(ConflictResult), WopiTelemetry.Outcomes.Conflict)]
    [InlineData(typeof(PreconditionFailedResult), WopiTelemetry.Outcomes.PreconditionFailed)]
    [InlineData(typeof(NotImplementedResult), WopiTelemetry.Outcomes.NotImplemented)]
    [InlineData(typeof(InternalServerErrorResult), WopiTelemetry.Outcomes.Error)]
    public async Task Outcome_From_Result_Type(Type resultType, string expectedOutcome)
    {
        var result = (IActionResult)Activator.CreateInstance(resultType)!;

        var (_, recordedActivity) = await RunFilterAsync(controller: new FilesController(null!, null!, null!, null!, null!, null!), result);

        Assert.NotNull(recordedActivity);
        Assert.Equal(expectedOutcome, recordedActivity.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    [Fact]
    public async Task LockMismatchResult_ClassifiedAsLockMismatch_NotConflict()
    {
        // LockMismatchResult inherits from ConflictResult — the filter must detect the more specific type first.
        var ctx = new DefaultHttpContext();
        var result = new LockMismatchResult(ctx.Response, existingLock: "abc");

        var (_, activity) = await RunFilterAsync(controller: new FilesController(null!, null!, null!, null!, null!, null!), result, httpContext: ctx);

        Assert.NotNull(activity);
        Assert.Equal(WopiTelemetry.Outcomes.LockMismatch, activity.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    [Fact]
    public async Task ContainerController_TagsContainerId()
    {
        var (_, activity) = await RunFilterAsync(
            controller: new ContainersController(null!, null!, null!, null!),
            new OkResult(),
            actionArguments: new() { ["id"] = "container-7" });

        Assert.NotNull(activity);
        Assert.Equal("container-7", activity.GetTagItem(WopiTelemetry.Tags.ContainerId));
        Assert.Null(activity.GetTagItem(WopiTelemetry.Tags.FileId));
    }

    [Fact]
    public async Task FilesController_TagsFileIdAndOverride()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[WopiHeaders.WOPI_OVERRIDE] = "LOCK";
        ctx.Request.Headers[WopiHeaders.LOCK] = "lock-id-1";

        var (_, activity) = await RunFilterAsync(
            controller: new FilesController(null!, null!, null!, null!, null!, null!),
            new OkResult(),
            actionArguments: new() { ["id"] = "file-7" },
            httpContext: ctx);

        Assert.NotNull(activity);
        Assert.Equal("file-7", activity.GetTagItem(WopiTelemetry.Tags.FileId));
        Assert.Equal("LOCK", activity.GetTagItem(WopiTelemetry.Tags.Override));
    }

    [Fact]
    public async Task UnhandledException_ClassifiedAsError()
    {
        var (_, activity) = await RunFilterAsync(
            controller: new FilesController(null!, null!, null!, null!, null!, null!),
            result: null,
            exception: new InvalidOperationException("boom"));

        Assert.NotNull(activity);
        Assert.Equal(WopiTelemetry.Outcomes.Error, activity.GetTagItem(WopiTelemetry.Tags.Outcome));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }

    [Fact]
    public async Task NoIdRouteValue_DoesNotAddResourceTag()
    {
        var (_, activity) = await RunFilterAsync(
            controller: new FilesController(null!, null!, null!, null!, null!, null!),
            new OkResult());

        Assert.NotNull(activity);
        Assert.Null(activity.GetTagItem(WopiTelemetry.Tags.FileId));
    }

    [Fact]
    public async Task FallsBackToHttpStatusCode_WhenResultIsRawStatusCodeResult()
    {
        var (_, activity) = await RunFilterAsync(
            controller: new FilesController(null!, null!, null!, null!, null!, null!),
            result: new StatusCodeResult(StatusCodes.Status404NotFound));

        Assert.NotNull(activity);
        Assert.Equal(WopiTelemetry.Outcomes.NotFound, activity.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    [Fact]
    public async Task FallsBackToResponseStatusCode_WhenResultIsNullButStatusSet()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.StatusCode = StatusCodes.Status409Conflict;

        var (_, activity) = await RunFilterAsync(
            controller: new FilesController(null!, null!, null!, null!, null!, null!),
            result: null,
            httpContext: ctx);

        Assert.NotNull(activity);
        Assert.Equal(WopiTelemetry.Outcomes.Conflict, activity.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    [Theory]
    [InlineData(StatusCodes.Status200OK, WopiTelemetry.Outcomes.Success)]
    [InlineData(StatusCodes.Status204NoContent, WopiTelemetry.Outcomes.Success)]
    [InlineData(StatusCodes.Status400BadRequest, WopiTelemetry.Outcomes.BadRequest)]
    [InlineData(StatusCodes.Status412PreconditionFailed, WopiTelemetry.Outcomes.PreconditionFailed)]
    [InlineData(StatusCodes.Status501NotImplemented, WopiTelemetry.Outcomes.NotImplemented)]
    [InlineData(StatusCodes.Status500InternalServerError, WopiTelemetry.Outcomes.Error)]
    public async Task MapStatusCode_ClassifiesViaRawStatusCodeResult(int statusCode, string expectedOutcome)
    {
        // Covers the MapStatusCode branches that the typed-result Theory doesn't reach. The
        // controller returns a bare StatusCodeResult — there's no typed *Result MVC wrapper for
        // these status codes (200, 204, 400, 412, 501, 500) — so the filter falls through to
        // MapStatusCode for classification.
        var (_, activity) = await RunFilterAsync(
            controller: new FilesController(null!, null!, null!, null!, null!, null!),
            result: new StatusCodeResult(statusCode));

        Assert.NotNull(activity);
        Assert.Equal(expectedOutcome, activity.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    [Fact]
    public async Task Cancellation_ClassifiedAsCancelled_NotError()
    {
        // Client-side disconnect: next() faults with an OperationCanceledException and the
        // request's CancellationToken has fired. The filter must record "Cancelled" instead of
        // "Error" — a stopped browser tab is not an application failure.
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var ctx = new DefaultHttpContext { RequestAborted = cts.Token };

        var (_, activity) = await RunFilterAsync(
            controller: new FilesController(null!, null!, null!, null!, null!, null!),
            result: null,
            httpContext: ctx,
            exception: new OperationCanceledException(cts.Token));

        Assert.NotNull(activity);
        Assert.Equal(WopiTelemetry.Outcomes.Cancelled, activity.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    [Fact]
    public async Task Cancellation_FromAggregateOfOCEs_AlsoClassifiedAsCancelled()
    {
        // Task.WhenAll wraps an observed OCE into an AggregateException whose only inner is the
        // OCE. The filter unwraps that shape to keep the Cancelled classification stable across
        // both forms.
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var ctx = new DefaultHttpContext { RequestAborted = cts.Token };
        var agg = new AggregateException(new OperationCanceledException(cts.Token), new OperationCanceledException(cts.Token));

        var (_, activity) = await RunFilterAsync(
            controller: new FilesController(null!, null!, null!, null!, null!, null!),
            result: null,
            httpContext: ctx,
            exception: agg);

        Assert.NotNull(activity);
        Assert.Equal(WopiTelemetry.Outcomes.Cancelled, activity.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    [Fact]
    public async Task OperationCanceledException_WithoutTokenFiring_ClassifiedAsError()
    {
        // OCE thrown without the request actually being aborted = real bug (someone canceled
        // an internal CTS by mistake). Don't whitewash that as "client disconnect".
        var ctx = new DefaultHttpContext();
        Assert.False(ctx.RequestAborted.IsCancellationRequested);

        var (_, activity) = await RunFilterAsync(
            controller: new FilesController(null!, null!, null!, null!, null!, null!),
            result: null,
            httpContext: ctx,
            exception: new OperationCanceledException());

        Assert.NotNull(activity);
        Assert.Equal(WopiTelemetry.Outcomes.Error, activity.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    private static async Task<(ActionExecutingContext executing, Activity? activity)> RunFilterAsync(
        ControllerBase controller,
        IActionResult? result,
        Dictionary<string, object?>? actionArguments = null,
        HttpContext? httpContext = null,
        Exception? exception = null,
        string actionName = "TestAction")
    {
        httpContext ??= new DefaultHttpContext();
        var actionDescriptor = new ControllerActionDescriptor
        {
            RouteValues = new Dictionary<string, string?> { ["action"] = actionName },
        };
        var routeData = new RouteData();
        var actionContext = new ActionContext(httpContext, routeData, actionDescriptor);

        var executingContext = new ActionExecutingContext(
            actionContext,
            filters: [],
            actionArguments: actionArguments ?? [],
            controller: controller);

        Activity? captured = null;
        Task<ActionExecutedContext> Next()
        {
            captured = Activity.Current;
            var executed = new ActionExecutedContext(actionContext, [], controller)
            {
                Result = result,
                Exception = exception,
            };
            return Task.FromResult(executed);
        }

        var filter = new WopiTelemetryActionFilter(NullLogger<WopiTelemetryActionFilter>.Instance);
        await filter.OnActionExecutionAsync(executingContext, Next);
        return (executingContext, captured);
    }
}
