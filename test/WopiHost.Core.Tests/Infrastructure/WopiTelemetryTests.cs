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
public sealed class WopiTelemetryTests : IDisposable
{
    private readonly ActivityListener _listener;

    public WopiTelemetryTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WopiTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
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
        var measurements = CollectRequests();
        using var activity = WopiTelemetry.StartActivity("PutFile", "file-1");

        WopiTelemetry.RecordOutcome(activity, "PutFile", WopiTelemetry.Outcomes.Success);

        Assert.NotNull(activity);
        Assert.Equal(WopiTelemetry.Outcomes.Success, activity.GetTagItem(WopiTelemetry.Tags.Outcome));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        var m = Assert.Single(measurements);
        Assert.Equal(1, m.value);
        Assert.Contains(m.tags, t => t.Key == WopiTelemetry.Tags.Operation && (string?)t.Value == "PutFile");
        Assert.Contains(m.tags, t => t.Key == WopiTelemetry.Tags.Outcome && (string?)t.Value == WopiTelemetry.Outcomes.Success);
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
        var requestMeasurements = CollectRequests();
        var lockMeasurements = CollectLockConflicts();
        using var activity = WopiTelemetry.StartActivity("Lock", "file-1");

        WopiTelemetry.RecordOutcome(activity, "Lock", WopiTelemetry.Outcomes.LockMismatch);

        Assert.Single(requestMeasurements);
        var lockHit = Assert.Single(lockMeasurements);
        Assert.Equal(1, lockHit.value);
        Assert.Contains(lockHit.tags, t => t.Key == WopiTelemetry.Tags.Operation && (string?)t.Value == "Lock");
    }

    [Fact]
    public void RecordOutcome_NullActivity_StillIncrementsRequestsCounter()
    {
        var measurements = CollectRequests();

        WopiTelemetry.RecordOutcome(activity: null, "GetFile", WopiTelemetry.Outcomes.Success);

        Assert.Single(measurements);
    }

    private static List<(long value, IReadOnlyList<KeyValuePair<string, object?>> tags)> CollectRequests()
        => CollectFromCounter(WopiTelemetry.Requests.Name);

    private static List<(long value, IReadOnlyList<KeyValuePair<string, object?>> tags)> CollectLockConflicts()
        => CollectFromCounter(WopiTelemetry.LockConflicts.Name);

    private static List<(long value, IReadOnlyList<KeyValuePair<string, object?>> tags)> CollectFromCounter(string instrumentName)
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
            measurements.Add((value, tags.ToArray()));
        });
        listener.Start();
        return measurements;
    }
}
