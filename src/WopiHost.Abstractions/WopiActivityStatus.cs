namespace WopiHost.Abstractions;

/// <summary>
/// Per-activity result status returned for each entry of an
/// <see href="https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/fdc52ab9-b359-4465-a8c7-6aa98aa12e06">AddActivities</see>
/// request. Serialized on the wire as its integer value.
/// </summary>
public enum WopiActivityStatus
{
    /// <summary>The activity was accepted by the host.</summary>
    Success = 0,

    /// <summary>The host does not support this activity type (e.g. an unrecognized <c>Type</c>).</summary>
    NotSupported = 3,
}
