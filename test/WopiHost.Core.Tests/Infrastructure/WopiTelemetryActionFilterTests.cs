using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
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
public sealed class WopiTelemetryActionFilterTests : IDisposable
{
    private readonly ActivityListener _listener;

    public WopiTelemetryActionFilterTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WopiTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
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

        var (executing, recordedActivity) = await RunFilterAsync(controller: new FilesController(null!, null!), result);

        Assert.NotNull(recordedActivity);
        Assert.Equal(expectedOutcome, recordedActivity.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    [Fact]
    public async Task LockMismatchResult_ClassifiedAsLockMismatch_NotConflict()
    {
        // LockMismatchResult inherits from ConflictResult — the filter must detect the more specific type first.
        var ctx = new DefaultHttpContext();
        var result = new LockMismatchResult(ctx.Response, existingLock: "abc");

        var (_, activity) = await RunFilterAsync(controller: new FilesController(null!, null!), result, httpContext: ctx);

        Assert.NotNull(activity);
        Assert.Equal(WopiTelemetry.Outcomes.LockMismatch, activity.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    [Fact]
    public async Task ContainerController_TagsContainerId()
    {
        var (_, activity) = await RunFilterAsync(
            controller: new ContainersController(null!),
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
            controller: new FilesController(null!, null!),
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
            controller: new FilesController(null!, null!),
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
            controller: new FilesController(null!, null!),
            new OkResult());

        Assert.NotNull(activity);
        Assert.Null(activity.GetTagItem(WopiTelemetry.Tags.FileId));
    }

    [Fact]
    public async Task FallsBackToHttpStatusCode_WhenResultIsRawStatusCodeResult()
    {
        var (_, activity) = await RunFilterAsync(
            controller: new FilesController(null!, null!),
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
            controller: new FilesController(null!, null!),
            result: null,
            httpContext: ctx);

        Assert.NotNull(activity);
        Assert.Equal(WopiTelemetry.Outcomes.Conflict, activity.GetTagItem(WopiTelemetry.Tags.Outcome));
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
            filters: new List<IFilterMetadata>(),
            actionArguments: actionArguments ?? new Dictionary<string, object?>(),
            controller: controller);

        Activity? captured = null;
        ActionExecutionDelegate next = () =>
        {
            captured = Activity.Current;
            var executed = new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), controller)
            {
                Result = result,
                Exception = exception,
            };
            return Task.FromResult(executed);
        };

        var filter = new WopiTelemetryActionFilter(NullLogger<WopiTelemetryActionFilter>.Instance);
        await filter.OnActionExecutionAsync(executingContext, next);
        return (executingContext, captured);
    }
}
