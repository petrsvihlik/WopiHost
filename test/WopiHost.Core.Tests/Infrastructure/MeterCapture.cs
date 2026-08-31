using System.Diagnostics.Metrics;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

/// <summary>
/// Disposable capture of the measurements published to one <see cref="WopiTelemetry"/> counter.
/// A <see cref="MeterListener"/> registration is process-global, so the capture must be disposed
/// when the owning test completes — otherwise the callback keeps firing for every later
/// measurement in the process.
/// </summary>
internal sealed class MeterCapture : IDisposable
{
    private readonly MeterListener _listener;
    private readonly List<(long Value, KeyValuePair<string, object?>[] Tags)> _measurements = [];

    public MeterCapture(string instrumentName)
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == WopiTelemetry.Name && instrument.Name == instrumentName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var tagsArray = tags.ToArray();
            lock (_measurements)
            {
                _measurements.Add((value, tagsArray));
            }
        });
        _listener.Start();
    }

    public IReadOnlyList<(long Value, KeyValuePair<string, object?>[] Tags)> Measurements
    {
        get
        {
            lock (_measurements)
            {
                return [.. _measurements];
            }
        }
    }

    public void Dispose() => _listener.Dispose();
}
