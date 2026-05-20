using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

public class WopiTelemetryEndpointFilterTests
{
    [Theory]
    [InlineData(200, WopiTelemetry.Outcomes.Success)]
    [InlineData(201, WopiTelemetry.Outcomes.Success)]
    [InlineData(204, WopiTelemetry.Outcomes.Success)]
    [InlineData(400, WopiTelemetry.Outcomes.BadRequest)]
    [InlineData(404, WopiTelemetry.Outcomes.NotFound)]
    [InlineData(409, WopiTelemetry.Outcomes.Conflict)]
    [InlineData(412, WopiTelemetry.Outcomes.PreconditionFailed)]
    [InlineData(500, WopiTelemetry.Outcomes.Error)]
    [InlineData(501, WopiTelemetry.Outcomes.NotImplemented)]
    public async Task Classifies_Outcome_From_Response_Status_Code(int status, string expectedOutcome)
    {
        var opName = $"Op_Status_{status}";
        using var captured = CaptureActivity(opName);
        var filter = new WopiTelemetryEndpointFilter(NullLogger<WopiTelemetryEndpointFilter>.Instance);
        var ctx = CreateContext(opName);

        await filter.InvokeAsync(ctx, c => { c.HttpContext.Response.StatusCode = status; return ValueTask.FromResult<object?>(null); });

        Assert.Equal(expectedOutcome, captured.Activity?.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    [Fact]
    public async Task IWopiOutcomeResult_Overrides_Status_Code_Classification()
    {
        const string opName = "Op_OutcomeOverride";
        using var captured = CaptureActivity(opName);
        var filter = new WopiTelemetryEndpointFilter(NullLogger<WopiTelemetryEndpointFilter>.Instance);
        var ctx = CreateContext(opName);
        // 409 alone would classify as Conflict; the marker forces LockMismatch.
        var customResult = new LockMismatchOutcome();

        await filter.InvokeAsync(ctx, c => { c.HttpContext.Response.StatusCode = 409; return ValueTask.FromResult<object?>(customResult); });

        Assert.Equal(WopiTelemetry.Outcomes.LockMismatch, captured.Activity?.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    [Fact]
    public async Task Classifies_Cancellation_When_RequestAborted()
    {
        const string opName = "Op_Cancellation";
        using var captured = CaptureActivity(opName);
        var filter = new WopiTelemetryEndpointFilter(NullLogger<WopiTelemetryEndpointFilter>.Instance);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var ctx = CreateContext(opName, c => c.RequestAborted = cts.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => filter.InvokeAsync(ctx, _ => throw new OperationCanceledException(cts.Token)).AsTask());

        Assert.Equal(WopiTelemetry.Outcomes.Cancelled, captured.Activity?.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    [Fact]
    public async Task Classifies_Error_When_Exception_Not_Cancellation()
    {
        const string opName = "Op_Error";
        using var captured = CaptureActivity(opName);
        var filter = new WopiTelemetryEndpointFilter(NullLogger<WopiTelemetryEndpointFilter>.Instance);
        var ctx = CreateContext(opName);

        await Assert.ThrowsAsync<InvalidOperationException>(() => filter.InvokeAsync(ctx, _ => throw new InvalidOperationException("boom")).AsTask());

        Assert.Equal(WopiTelemetry.Outcomes.Error, captured.Activity?.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    [Fact]
    public async Task Container_Resource_Kind_Switches_Dimension_Tag()
    {
        const string opName = "Op_ContainerKind";
        using var captured = CaptureActivity(opName);
        var filter = new WopiTelemetryEndpointFilter(NullLogger<WopiTelemetryEndpointFilter>.Instance);
        var ctx = CreateContext(opName,
            metadata: [new WopiResourceKindMetadata(WopiResourceType.Container)],
            httpContext: c => c.Request.RouteValues["id"] = "abc");

        await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>(null));

        Assert.Equal("abc", captured.Activity?.GetTagItem(WopiTelemetry.Tags.ContainerId));
        Assert.Null(captured.Activity?.GetTagItem(WopiTelemetry.Tags.FileId));
    }

    [Fact]
    public async Task Defaults_To_File_Dimension_Without_ResourceKindMetadata()
    {
        const string opName = "Op_FileKindDefault";
        using var captured = CaptureActivity(opName);
        var filter = new WopiTelemetryEndpointFilter(NullLogger<WopiTelemetryEndpointFilter>.Instance);
        var ctx = CreateContext(opName, httpContext: c => c.Request.RouteValues["id"] = "xyz");

        await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>(null));

        Assert.Equal("xyz", captured.Activity?.GetTagItem(WopiTelemetry.Tags.FileId));
    }

    [Fact]
    public async Task Operation_Name_Read_From_EndpointNameMetadata()
    {
        const string opName = "Op_NameFromMetadata";
        using var captured = CaptureActivity(opName);
        var filter = new WopiTelemetryEndpointFilter(NullLogger<WopiTelemetryEndpointFilter>.Instance);
        var ctx = CreateContext(opName);

        await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>(null));

        Assert.Equal(opName, captured.Activity?.OperationName);
        Assert.Equal(opName, captured.Activity?.GetTagItem(WopiTelemetry.Tags.Operation));
    }

    [Fact]
    public async Task Without_Endpoint_Operation_Defaults_To_Unknown()
    {
        const string opName = "Unknown";
        using var captured = CaptureActivity(opName);
        var filter = new WopiTelemetryEndpointFilter(NullLogger<WopiTelemetryEndpointFilter>.Instance);
        // No endpoint set on HttpContext.
        var httpContext = new DefaultHttpContext();
        var ctx = new DefaultEndpointFilterInvocationContext(httpContext);

        await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>(null));

        Assert.Equal(opName, captured.Activity?.OperationName);
    }

    [Fact]
    public async Task Without_DisplayName_Or_Name_Falls_Back_To_Unknown()
    {
        const string opName = "Unknown";
        using var captured = CaptureActivity(opName);
        var filter = new WopiTelemetryEndpointFilter(NullLogger<WopiTelemetryEndpointFilter>.Instance);
        // Endpoint with metadata that doesn't include EndpointNameMetadata, no display name either.
        var endpoint = new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/test"),
            order: 0,
            new EndpointMetadataCollection(),
            displayName: null);
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(endpoint);
        var ctx = new DefaultEndpointFilterInvocationContext(httpContext);

        await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>(null));

        Assert.Equal(opName, captured.Activity?.OperationName);
    }

    [Fact]
    public async Task DisplayName_Used_When_EndpointName_Absent()
    {
        const string opName = "Op_FromDisplayName";
        using var captured = CaptureActivity(opName);
        var filter = new WopiTelemetryEndpointFilter(NullLogger<WopiTelemetryEndpointFilter>.Instance);
        var endpoint = new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/test"),
            order: 0,
            new EndpointMetadataCollection(),
            displayName: opName);
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(endpoint);
        var ctx = new DefaultEndpointFilterInvocationContext(httpContext);

        await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>(null));

        Assert.Equal(opName, captured.Activity?.OperationName);
    }

    [Fact]
    public async Task Headers_And_User_Claims_Populate_Scope_Tags()
    {
        const string opName = "Op_HeadersAndClaims";
        using var captured = CaptureActivity(opName);
        var filter = new WopiTelemetryEndpointFilter(NullLogger<WopiTelemetryEndpointFilter>.Instance);
        var ctx = CreateContext(opName, c =>
        {
            c.Request.RouteValues["id"] = "abc";
            c.Request.Headers[WopiHeaders.WOPI_OVERRIDE] = "LOCK";
            c.Request.Headers[WopiHeaders.LOCK] = "lock-token";
            c.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "alice")],
                    "test"));
        });

        await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>(null));

        // Override / lock id flow into the activity tags via StartActivity.
        Assert.Equal("LOCK", captured.Activity?.GetTagItem(WopiTelemetry.Tags.Override));
        // Lock id and user id only land on the log scope, not the activity tag. The fact that the
        // call returned successfully without throwing (e.g., a null-reference reading the claim)
        // is the assertion we care about — exercises the `lockId/userId is not null` branches.
        Assert.Equal("abc", captured.Activity?.GetTagItem(WopiTelemetry.Tags.FileId));
    }

    [Fact]
    public async Task Missing_Headers_And_Anonymous_User_Skip_Scope_Adds()
    {
        const string opName = "Op_Missing";
        using var captured = CaptureActivity(opName);
        var filter = new WopiTelemetryEndpointFilter(NullLogger<WopiTelemetryEndpointFilter>.Instance);
        // No headers, no route id, no user — exercises the `is null` branches that skip the
        // conditional scope adds and the override/resource tags on the activity.
        var ctx = CreateContext(opName);

        await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>(null));

        Assert.Null(captured.Activity?.GetTagItem(WopiTelemetry.Tags.Override));
        Assert.Null(captured.Activity?.GetTagItem(WopiTelemetry.Tags.FileId));
        Assert.Null(captured.Activity?.GetTagItem(WopiTelemetry.Tags.ContainerId));
    }

    [Fact]
    public async Task AggregateException_With_Cancellation_Classified_As_Cancelled()
    {
        const string opName = "Op_AggregateCancel";
        using var captured = CaptureActivity(opName);
        var filter = new WopiTelemetryEndpointFilter(NullLogger<WopiTelemetryEndpointFilter>.Instance);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var ctx = CreateContext(opName, c => c.RequestAborted = cts.Token);
        var aggregate = new AggregateException(new OperationCanceledException(cts.Token), new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<AggregateException>(() => filter.InvokeAsync(ctx, _ => throw aggregate).AsTask());

        Assert.Equal(WopiTelemetry.Outcomes.Cancelled, captured.Activity?.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    [Fact]
    public async Task OperationCanceled_Without_RequestAborted_Classified_As_Error()
    {
        const string opName = "Op_LooseOCE";
        using var captured = CaptureActivity(opName);
        var filter = new WopiTelemetryEndpointFilter(NullLogger<WopiTelemetryEndpointFilter>.Instance);
        // RequestAborted not cancelled → OperationCanceledException is treated as a real error.
        var ctx = CreateContext(opName);

        await Assert.ThrowsAsync<OperationCanceledException>(() => filter.InvokeAsync(ctx, _ => throw new OperationCanceledException()).AsTask());

        Assert.Equal(WopiTelemetry.Outcomes.Error, captured.Activity?.GetTagItem(WopiTelemetry.Tags.Outcome));
    }

    private static DefaultEndpointFilterInvocationContext CreateContext(
        string operationName,
        Action<HttpContext>? httpContext = null,
        object[]? metadata = null)
    {
        var ctx = new DefaultHttpContext();
        var allMetadata = new List<object> { new EndpointNameMetadata(operationName) };
        if (metadata is not null) allMetadata.AddRange(metadata);
        var endpoint = new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/test"),
            order: 0,
            new EndpointMetadataCollection(allMetadata),
            displayName: null);
        ctx.SetEndpoint(endpoint);
        httpContext?.Invoke(ctx);
        return new DefaultEndpointFilterInvocationContext(ctx);
    }

    /// <summary>
    /// Spins up an <see cref="ActivityListener"/> against WopiTelemetry's source, scoped to a
    /// specific operation name so tests running in parallel don't capture each other's activities.
    /// Disposed via <c>using</c> in the test to remove the global listener registration.
    /// </summary>
    private static ActivityCapture CaptureActivity(string operationName)
    {
        var capture = new ActivityCapture();
        var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == WopiTelemetry.Name,
            Sample = static (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = a => { if (a.OperationName == operationName) capture.Activity ??= a; },
        };
        ActivitySource.AddActivityListener(listener);
        capture.Listener = listener;
        return capture;
    }

    private sealed class ActivityCapture : IDisposable
    {
        public Activity? Activity { get; set; }
        public ActivityListener? Listener { get; set; }
        public void Dispose() => Listener?.Dispose();
    }

    private sealed class LockMismatchOutcome : IResult, IWopiOutcomeResult
    {
        public string Outcome => WopiTelemetry.Outcomes.LockMismatch;
        public Task ExecuteAsync(HttpContext httpContext) => Task.CompletedTask;
    }
}
