using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// OpenTelemetry primitives for the WOPI request pipeline. The <see cref="ActivitySource"/> and
/// <see cref="Meter"/> share the name <c>"WopiHost.Core"</c>, which is pre-registered in
/// <c>WopiHost.ServiceDefaults</c> — apps calling <c>AddServiceDefaults()</c> export these
/// automatically.
/// </summary>
public static class WopiTelemetry
{
    /// <summary>Shared activity source / meter name. Pre-registered in <c>WopiHost.ServiceDefaults</c>.</summary>
    public const string Name = "WopiHost.Core";

    /// <summary>Activity source used by the WOPI request pipeline.</summary>
    public static readonly ActivitySource ActivitySource = new(Name);

    private static readonly Meter Meter = new(Name);

    /// <summary>Total WOPI operations processed, tagged by <see cref="Tags.Operation"/> and <see cref="Tags.Outcome"/>.</summary>
    public static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        "wopi.requests", unit: "{request}",
        description: "WOPI operations processed by the host, tagged by operation and outcome.");

    /// <summary>Lock-conflict outcomes (HTTP 409 with <c>X-WOPI-Lock</c>). Charted separately so dashboards don't need filter math.</summary>
    public static readonly Counter<long> LockConflicts = Meter.CreateCounter<long>(
        "wopi.lock_conflicts", unit: "{conflict}",
        description: "WOPI lock conflicts (409 with X-WOPI-Lock) observed by the request pipeline.");

    /// <summary>WOPI proof-key signature validation failures.</summary>
    public static readonly Counter<long> ProofValidationFailures = Meter.CreateCounter<long>(
        "wopi.proof_validation_failures", unit: "{failure}",
        description: "WOPI proof-key signature validation failures.");

    /// <summary>Tag keys shared across spans, log scopes, and metrics.</summary>
    public static class Tags
    {
        public const string Operation = "wopi.operation";
        public const string FileId = "wopi.file_id";
        public const string ContainerId = "wopi.container_id";
        public const string LockId = "wopi.lock_id";
        public const string Outcome = "wopi.outcome";
        public const string Override = "wopi.override";
        public const string UserId = "enduser.id";
    }

    /// <summary>Canonical strings for the <see cref="Tags.Outcome"/> dimension.</summary>
    public static class Outcomes
    {
        public const string Success = "success";
        public const string NotFound = "not_found";
        public const string Conflict = "conflict";
        public const string LockMismatch = "lock_mismatch";
        public const string BadRequest = "bad_request";
        public const string PreconditionFailed = "precondition_failed";
        public const string NotImplemented = "not_implemented";
        public const string ProofValidationFailed = "proof_validation_failed";
        public const string Error = "error";
    }

    /// <summary>
    /// Starts an Activity for a WOPI operation and tags it. Returns <c>null</c> when no listener is attached.
    /// </summary>
    public static Activity? StartActivity(
        string operation,
        string? resourceId = null,
        string resourceTagKey = Tags.FileId,
        string? wopiOverride = null)
    {
        var activity = ActivitySource.StartActivity(operation, ActivityKind.Server);
        if (activity is null) return null;
        activity.SetTag(Tags.Operation, operation);
        if (!string.IsNullOrEmpty(resourceId)) activity.SetTag(resourceTagKey, resourceId);
        if (!string.IsNullOrEmpty(wopiOverride)) activity.SetTag(Tags.Override, wopiOverride);
        return activity;
    }

    /// <summary>
    /// Tags the activity with the outcome, increments <see cref="Requests"/>, and (for
    /// <see cref="Outcomes.LockMismatch"/>) increments <see cref="LockConflicts"/>.
    /// </summary>
    public static void RecordOutcome(Activity? activity, string operation, string outcome)
    {
        activity?.SetTag(Tags.Outcome, outcome);
        if (outcome != Outcomes.Success)
        {
            activity?.SetStatus(ActivityStatusCode.Error, outcome);
        }
        Requests.Add(1,
            new KeyValuePair<string, object?>(Tags.Operation, operation),
            new KeyValuePair<string, object?>(Tags.Outcome, outcome));
        if (outcome == Outcomes.LockMismatch)
        {
            LockConflicts.Add(1, new KeyValuePair<string, object?>(Tags.Operation, operation));
        }
    }
}
