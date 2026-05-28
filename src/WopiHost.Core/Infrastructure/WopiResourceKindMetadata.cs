using WopiHost.Abstractions;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Endpoint metadata identifying the kind of WOPI resource (file or container) the endpoint
/// operates on. Read by the telemetry endpoint filter to pick the correct dimension tag
/// (<see cref="WopiTelemetry.Tags.FileId"/> vs <see cref="WopiTelemetry.Tags.ContainerId"/>).
/// Endpoints without this metadata are treated as files — the most common case.
/// </summary>
public sealed class WopiResourceKindMetadata(WopiResourceType type)
{
    /// <summary>The kind of WOPI resource the endpoint operates on.</summary>
    public WopiResourceType Type { get; } = type;
}
