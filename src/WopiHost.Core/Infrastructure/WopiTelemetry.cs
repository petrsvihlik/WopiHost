using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Centralized OpenTelemetry primitives for WopiHost: a shared <see cref="System.Diagnostics.ActivitySource"/>
/// for spans across the WOPI request pipeline and a <see cref="System.Diagnostics.Metrics.Meter"/>
/// with counters for the outcomes called out in issue #331 (lock conflicts, proof-validation failures,
/// per-operation request totals tagged with operation/outcome).
/// </summary>
/// <remarks>
/// The <see cref="ActivitySourceName"/> and <see cref="MeterName"/> are pre-registered in
/// <c>WopiHost.ServiceDefaults</c>, so any application that calls <c>AddServiceDefaults()</c> will
/// automatically export the spans and metrics produced through this class.
/// </remarks>
public static class WopiTelemetry
{
    /// <summary>Shared activity source name. Pre-registered in <c>WopiHost.ServiceDefaults</c>.</summary>
    public const string ActivitySourceName = "WopiHost.Core";

    /// <summary>Shared meter name. Pre-registered in <c>WopiHost.ServiceDefaults</c>.</summary>
    public const string MeterName = "WopiHost.Core";

    /// <summary>Activity source used by the WOPI request pipeline.</summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter Meter = new(MeterName);

    /// <summary>
    /// Total WOPI requests processed, tagged with <see cref="Tags.Operation"/> and <see cref="Tags.Outcome"/>.
    /// One increment per controller action invocation.
    /// </summary>
    public static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        name: "wopi.requests",
        unit: "{request}",
        description: "WOPI operations processed by the host, tagged by operation and outcome.");

    /// <summary>
    /// Number of lock-conflict outcomes (HTTP 409 with <c>X-WOPI-Lock</c>) observed by the controllers.
    /// Distinct from <see cref="Requests"/> so dashboards can chart it without filter math.
    /// </summary>
    public static readonly Counter<long> LockConflicts = Meter.CreateCounter<long>(
        name: "wopi.lock_conflicts",
        unit: "{conflict}",
        description: "WOPI lock conflicts (409 with X-WOPI-Lock) observed by the request pipeline.");

    /// <summary>
    /// Number of WOPI proof-key validation failures (signed-request validation rejected the request).
    /// </summary>
    public static readonly Counter<long> ProofValidationFailures = Meter.CreateCounter<long>(
        name: "wopi.proof_validation_failures",
        unit: "{failure}",
        description: "WOPI proof-key signature validation failures.");

    /// <summary>Tag keys shared across spans, log scopes, and metrics.</summary>
    public static class Tags
    {
        /// <summary>WOPI operation name (e.g. <c>CheckFileInfo</c>, <c>Lock</c>, <c>PutFile</c>).</summary>
        public const string Operation = "wopi.operation";

        /// <summary>WOPI file identifier from the route.</summary>
        public const string FileId = "wopi.file_id";

        /// <summary>WOPI container identifier from the route.</summary>
        public const string ContainerId = "wopi.container_id";

        /// <summary>The lock identifier carried in <c>X-WOPI-Lock</c>, when present.</summary>
        public const string LockId = "wopi.lock_id";

        /// <summary>Outcome category — see <see cref="Outcomes"/> for the canonical values.</summary>
        public const string Outcome = "wopi.outcome";

        /// <summary>The <c>X-WOPI-Override</c> header value, when present.</summary>
        public const string Override = "wopi.override";

        /// <summary>The end-user id (NameIdentifier claim) issuing the request.</summary>
        public const string UserId = "enduser.id";
    }

    /// <summary>Canonical outcome strings used for the <see cref="Tags.Outcome"/> dimension.</summary>
    public static class Outcomes
    {
        /// <summary>Operation completed successfully (HTTP 200).</summary>
        public const string Success = "success";

        /// <summary>Resource not found (HTTP 404).</summary>
        public const string NotFound = "not_found";

        /// <summary>Resource conflict — typically an existing-name collision (HTTP 409 without lock header).</summary>
        public const string Conflict = "conflict";

        /// <summary>Lock-mismatch / lock-conflict (HTTP 409 with X-WOPI-Lock).</summary>
        public const string LockMismatch = "lock_mismatch";

        /// <summary>Client-side error — invalid request shape or arguments (HTTP 400).</summary>
        public const string BadRequest = "bad_request";

        /// <summary>Precondition failed (HTTP 412) — e.g. file larger than X-WOPI-MaxExpectedSize.</summary>
        public const string PreconditionFailed = "precondition_failed";

        /// <summary>Server has no implementation for the requested operation (HTTP 501).</summary>
        public const string NotImplemented = "not_implemented";

        /// <summary>Proof-key validation failed.</summary>
        public const string ProofValidationFailed = "proof_validation_failed";

        /// <summary>Unhandled error / 500.</summary>
        public const string Error = "error";
    }

    /// <summary>
    /// Starts an activity for a WOPI operation, tagging it with the operation name and (if present)
    /// the file/container id and X-WOPI-Override header. Returns <c>null</c> when no listener is attached.
    /// </summary>
    /// <param name="operation">WOPI operation name (e.g. <c>CheckFileInfo</c>, <c>Lock</c>).</param>
    /// <param name="resourceId">File or container identifier from the route.</param>
    /// <param name="resourceTagKey">Either <see cref="Tags.FileId"/> or <see cref="Tags.ContainerId"/>.</param>
    /// <param name="wopiOverride">The X-WOPI-Override header value, if any.</param>
    public static Activity? StartActivity(
        string operation,
        string? resourceId = null,
        string resourceTagKey = Tags.FileId,
        string? wopiOverride = null)
    {
        var activity = ActivitySource.StartActivity(operation, ActivityKind.Server);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag(Tags.Operation, operation);
        if (!string.IsNullOrEmpty(resourceId))
        {
            activity.SetTag(resourceTagKey, resourceId);
        }
        if (!string.IsNullOrEmpty(wopiOverride))
        {
            activity.SetTag(Tags.Override, wopiOverride);
        }
        return activity;
    }

    /// <summary>
    /// Records the outcome of a WOPI operation: tags the activity, increments <see cref="Requests"/>,
    /// and (when the outcome is <see cref="Outcomes.LockMismatch"/>) increments <see cref="LockConflicts"/>.
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
