namespace WopiHost.Core.Tests.Infrastructure;

/// <summary>
/// xUnit collection that serialises classes which install process-global
/// <see cref="System.Diagnostics.ActivityListener"/>s for the
/// <see cref="WopiHost.Core.Infrastructure.WopiTelemetry"/> source.
/// </summary>
/// <remarks>
/// Without this, xUnit runs the telemetry test classes in parallel and one class's listener
/// leaks into another's assertions about &quot;no listener&quot;.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class WopiTelemetryCollection
{
    public const string Name = "WopiTelemetry";
}
