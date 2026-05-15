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
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
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
