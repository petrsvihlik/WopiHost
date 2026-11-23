using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Provides the OpenTelemetry primitives of the WOPI request pipeline. The <see cref="ActivitySource"/> and
/// <see cref="Meter"/> share the name <c>"WopiHost.Core"</c>, which is pre-registered in
/// <c>WopiHost.ServiceDefaults</c> — apps calling <c>AddServiceDefaults()</c> export these
/// automatically.
/// </summary>
public static class WopiTelemetry
{
    /// <summary>The shared activity source and meter name. Pre-registered in <c>WopiHost.ServiceDefaults</c>.</summary>
    public const string Name = "WopiHost.Core";

    /// <summary>The activity source used by the WOPI request pipeline.</summary>
    public static readonly ActivitySource ActivitySource = new(Name);

    private static readonly Meter s_meter = new(Name);

    /// <summary>Total WOPI operations processed, tagged by <see cref="Tags.Operation"/> and <see cref="Tags.Outcome"/>.</summary>
    public static readonly Counter<long> Requests = s_meter.CreateCounter<long>(
        "wopi.requests", unit: "{request}",
        description: "WOPI operations processed by the host, tagged by operation and outcome.");

    /// <summary>Lock-conflict outcomes (HTTP 409 with <c>X-WOPI-Lock</c>). Charted separately so dashboards don't need filter math.</summary>
    public static readonly Counter<long> LockConflicts = s_meter.CreateCounter<long>(
        "wopi.lock_conflicts", unit: "{conflict}",
        description: "WOPI lock conflicts (409 with X-WOPI-Lock) observed by the request pipeline.");

    /// <summary>WOPI proof-key signature validation failures.</summary>
    public static readonly Counter<long> ProofValidationFailures = s_meter.CreateCounter<long>(
        "wopi.proof_validation_failures", unit: "{failure}",
        description: "WOPI proof-key signature validation failures.");

    /// <summary>Provides the tag keys shared across spans, log scopes, and metrics.</summary>
    public static class Tags
    {
        /// <summary>WOPI operation name (e.g. <c>CheckFileInfo</c>, <c>Lock</c>).</summary>
        public const string Operation = "wopi.operation";

        /// <summary>File identifier of the targeted resource.</summary>
        public const string FileId = "wopi.file_id";

        /// <summary>Container identifier of the targeted resource.</summary>
        public const string ContainerId = "wopi.container_id";

        /// <summary>Lock identifier involved in the operation.</summary>
        public const string LockId = "wopi.lock_id";

        /// <summary>Operation outcome; values come from <see cref="Outcomes"/>.</summary>
        public const string Outcome = "wopi.outcome";

        /// <summary>The request's <c>X-WOPI-Override</c> header value.</summary>
        public const string Override = "wopi.override";

        /// <summary>Authenticated user id (OpenTelemetry's standard <c>enduser.id</c> key).</summary>
        public const string UserId = "enduser.id";
    }

    /// <summary>Provides the canonical values of the <see cref="Tags.Outcome"/> dimension.</summary>
    public static class Outcomes
    {
        /// <summary>Operation completed successfully (2xx).</summary>
        public const string Success = "success";

        /// <summary>Target resource not found (404).</summary>
        public const string NotFound = "not_found";

        /// <summary>Generic conflict (409) other than a lock mismatch.</summary>
        public const string Conflict = "conflict";

        /// <summary>Lock conflict (409 with <c>X-WOPI-Lock</c>).</summary>
        public const string LockMismatch = "lock_mismatch";

        /// <summary>Malformed or invalid request (400).</summary>
        public const string BadRequest = "bad_request";

        /// <summary>Precondition failed (412).</summary>
        public const string PreconditionFailed = "precondition_failed";

        /// <summary>Operation not supported by this host (501).</summary>
        public const string NotImplemented = "not_implemented";

        /// <summary>
        /// Reserved for requests rejected by WOPI proof-key validation. The built-in pipeline does not
        /// emit it; proof failures are counted on <see cref="ProofValidationFailures"/>.
        /// </summary>
        public const string ProofValidationFailed = "proof_validation_failed";

        /// <summary>Client disconnected / request aborted before completion. Not a server error.</summary>
        public const string Cancelled = "cancelled";

        /// <summary>
        /// Unhandled exception, or any status code not mapped to another outcome (for example 5xx,
        /// 401, 403 or 413).
        /// </summary>
        public const string Error = "error";
    }

    /// <summary>
    /// Starts an <see cref="Activity"/> for a WOPI operation and tags it.
    /// </summary>
    /// <param name="operation">The WOPI operation name, used as the activity name and the <see cref="Tags.Operation"/> tag.</param>
    /// <param name="resourceId">The identifier of the targeted resource, if any.</param>
    /// <param name="resourceTagKey">The tag key for <paramref name="resourceId"/>: <see cref="Tags.FileId"/> or <see cref="Tags.ContainerId"/>.</param>
    /// <param name="wopiOverride">The request's <c>X-WOPI-Override</c> header value, if any.</param>
    /// <returns>The started activity, or <see langword="null"/> when no listener is attached.</returns>
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
    /// <remarks>
    /// Every outcome other than <see cref="Outcomes.Success"/> and <see cref="Outcomes.Cancelled"/>
    /// sets the activity status to <see cref="ActivityStatusCode.Error"/>.
    /// </remarks>
    /// <param name="activity">The activity to tag, or <see langword="null"/> when none was started.</param>
    /// <param name="operation">The WOPI operation name.</param>
    /// <param name="outcome">The outcome: one of the constants on <see cref="Outcomes"/>.</param>
    public static void RecordOutcome(Activity? activity, string operation, string outcome)
    {
        activity?.SetTag(Tags.Outcome, outcome);
        // Success and Cancelled are non-Error from a tracing perspective — Cancelled means the
        // client closed the connection, which isn't a server-side fault.
        if (outcome != Outcomes.Success && outcome != Outcomes.Cancelled)
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
