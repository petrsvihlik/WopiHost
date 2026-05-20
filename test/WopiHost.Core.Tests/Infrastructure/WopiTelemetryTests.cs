using System.Diagnostics;
using System.Diagnostics.Metrics;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

/// <summary>
/// Tests for the <see cref="WopiTelemetry"/> primitives. The fixture attaches an
/// <see cref="ActivityListener"/> so <see cref="WopiTelemetry.StartActivity"/> actually
/// produces an <see cref="Activity"/> (otherwise it returns <c>null</c> and the helper paths
/// stay uncovered).
/// </summary>
/// <remarks>
/// Pinned to <see cref="WopiTelemetryCollection"/> so this class runs sequentially with any
/// sibling that registers a process-global <see cref="ActivitySource"/> listener for
/// <see cref="WopiTelemetry.Name"/>; if xUnit runs them in parallel, this class's
/// <c>StartActivity_NoListener_ReturnsNull</c> sees the sibling listener and fails
/// non-deterministically.
/// </remarks>
[Collection(WopiTelemetryCollection.Name)]
public sealed class WopiTelemetryTests : IDisposable
{
    private readonly ActivityListener _listener;

    public WopiTelemetryTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WopiTelemetry.Name,
            Sample = (ref _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void StartActivity_NoListener_ReturnsNull()
    {
        // Use a private listener that ignores our source so StartActivity has no listener for it.
        _listener.Dispose();

        using var activity = WopiTelemetry.StartActivity("Lock");

        Assert.Null(activity);
    }

    [Fact]
    public void StartActivity_TagsOperationFileIdAndOverride()
    {
        using var activity = WopiTelemetry.StartActivity(
            operation: "Lock",
            resourceId: "file-1",
            resourceTagKey: WopiTelemetry.Tags.FileId,
            wopiOverride: "LOCK");

        Assert.NotNull(activity);
        Assert.Equal("Lock", activity.OperationName);
        Assert.Equal("Lock", activity.GetTagItem(WopiTelemetry.Tags.Operation));
        Assert.Equal("file-1", activity.GetTagItem(WopiTelemetry.Tags.FileId));
        Assert.Equal("LOCK", activity.GetTagItem(WopiTelemetry.Tags.Override));
    }

    [Fact]
    public void StartActivity_NullOptionalArgs_DoesNotTagThem()
    {
        using var activity = WopiTelemetry.StartActivity(operation: "CheckFileInfo");

        Assert.NotNull(activity);
        Assert.Null(activity.GetTagItem(WopiTelemetry.Tags.FileId));
        Assert.Null(activity.GetTagItem(WopiTelemetry.Tags.Override));
    }

    [Fact]
    public void StartActivity_ContainerTagKey_TagsContainerId()
    {
        using var activity = WopiTelemetry.StartActivity(
            operation: "EnumerateChildren",
            resourceId: "container-1",
            resourceTagKey: WopiTelemetry.Tags.ContainerId);

        Assert.NotNull(activity);
        Assert.Equal("container-1", activity.GetTagItem(WopiTelemetry.Tags.ContainerId));
        Assert.Null(activity.GetTagItem(WopiTelemetry.Tags.FileId));
    }

    [Fact]
    public void RecordOutcome_Success_TagsActivityAndIncrementsRequests()
    {
        // Use a unique operation name so this test's measurements aren't intermixed with
        // other tests publishing to the same static Meter when xUnit runs classes in parallel.
        const string Op = nameof(RecordOutcome_Success_TagsActivityAndIncrementsRequests);
        var measurements = CollectRequests(Op);
        using var activity = WopiTelemetry.StartActivity(Op, "file-1");

        WopiTelemetry.RecordOutcome(activity, Op, WopiTelemetry.Outcomes.Success);

        Assert.NotNull(activity);
        Assert.Equal(WopiTelemetry.Outcomes.Success, activity.GetTagItem(WopiTelemetry.Tags.Outcome));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        var (value, tags) = Assert.Single(measurements);
        Assert.Equal(1, value);
        Assert.Contains(tags, t => t.Key == WopiTelemetry.Tags.Outcome && (string?)t.Value == WopiTelemetry.Outcomes.Success);
    }

    [Fact]
    public void RecordOutcome_NonSuccess_SetsActivityStatusError()
    {
        using var activity = WopiTelemetry.StartActivity("DeleteFile", "file-1");

        WopiTelemetry.RecordOutcome(activity, "DeleteFile", WopiTelemetry.Outcomes.NotFound);

        Assert.NotNull(activity);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(WopiTelemetry.Outcomes.NotFound, activity.StatusDescription);
    }

    [Fact]
    public void RecordOutcome_LockMismatch_AlsoIncrementsLockConflicts()
    {
        const string Op = nameof(RecordOutcome_LockMismatch_AlsoIncrementsLockConflicts);
        var requestMeasurements = CollectRequests(Op);
        var lockMeasurements = CollectLockConflicts(Op);
        using var activity = WopiTelemetry.StartActivity(Op, "file-1");

        WopiTelemetry.RecordOutcome(activity, Op, WopiTelemetry.Outcomes.LockMismatch);

        Assert.Single(requestMeasurements);
        var (lockValue, _) = Assert.Single(lockMeasurements);
        Assert.Equal(1, lockValue);
    }

    [Fact]
    public void RecordOutcome_NullActivity_StillIncrementsRequestsCounter()
    {
        const string Op = nameof(RecordOutcome_NullActivity_StillIncrementsRequestsCounter);
        var measurements = CollectRequests(Op);

        WopiTelemetry.RecordOutcome(activity: null, Op, WopiTelemetry.Outcomes.Success);

        Assert.Single(measurements);
    }

    private static List<(long value, IReadOnlyList<KeyValuePair<string, object?>> tags)> CollectRequests(string operationFilter)
        => CollectFromCounter(WopiTelemetry.Requests.Name, operationFilter);

    private static List<(long value, IReadOnlyList<KeyValuePair<string, object?>> tags)> CollectLockConflicts(string operationFilter)
        => CollectFromCounter(WopiTelemetry.LockConflicts.Name, operationFilter);

    /// <summary>
    /// Collects measurements published to the named WopiTelemetry counter, filtered to those
    /// whose <see cref="WopiTelemetry.Tags.Operation"/> tag equals <paramref name="operationFilter"/>.
    /// The filter prevents cross-test pollution when xUnit runs other test classes concurrently
    /// against the same static <see cref="Meter"/>.
    /// </summary>
    private static List<(long value, IReadOnlyList<KeyValuePair<string, object?>> tags)> CollectFromCounter(string instrumentName, string operationFilter)
    {
        var measurements = new List<(long, IReadOnlyList<KeyValuePair<string, object?>>)>();
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == WopiTelemetry.Name && instrument.Name == instrumentName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var tagsArray = tags.ToArray();
            var matches = tagsArray.Any(t => t.Key == WopiTelemetry.Tags.Operation && (string?)t.Value == operationFilter);
            if (matches)
            {
                measurements.Add((value, tagsArray));
            }
        });
        listener.Start();
        return measurements;
    }
}
