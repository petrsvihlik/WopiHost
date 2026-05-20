namespace WopiHost.Abstractions;

/// <summary>
/// Outcome of <see cref="IWopiNewChildFileNegotiator.NegotiateAsync"/>. Pure POCO — no
/// HTTP types — so the negotiator stays testable without a request context. The controller
/// translates this into an <c>IActionResult</c> at the HTTP boundary.
/// </summary>
public sealed class WopiNewChildFileResult
{
    /// <summary>
    /// What happened. Drives the response-mapping switch at the controller.
    /// </summary>
    public required WopiNewChildFileOutcome Outcome { get; init; }

    /// <summary>
    /// The file the caller should proceed with — newly created or an existing file the
    /// caller asked to overwrite. Non-null exactly when <see cref="Outcome"/> is
    /// <see cref="WopiNewChildFileOutcome.Success"/>. Typed as <see cref="IWopiWritableFile"/>
    /// because callers always proceed by either writing contents (PutRelativeFile) or returning
    /// metadata for a file that will accept a subsequent PutFile (CreateChildFile).
    /// </summary>
    public IWopiWritableFile? File { get; init; }

    /// <summary>
    /// The host-suggested alternative name to advertise via the
    /// <c>X-WOPI-ValidRelativeTarget</c> response header. Non-null on
    /// <see cref="WopiNewChildFileOutcome.Conflict"/>; optionally non-null on
    /// <see cref="WopiNewChildFileOutcome.BadRequest"/> when the host can suggest a
    /// sanitised alternative for the invalid name (per the spec: "X-WOPI-ValidRelativeTarget
    /// might be used when responding with a 400 Bad Request, because the requested name
    /// contained invalid characters").
    /// </summary>
    public string? ValidRelativeTargetSuggestion { get; init; }

    /// <summary>
    /// Existing lock id to surface on the 409-lock-mismatch path. Non-null exactly when
    /// <see cref="Outcome"/> is <see cref="WopiNewChildFileOutcome.Locked"/>.
    /// </summary>
    public string? ExistingLockId { get; init; }

    /// <summary>Shorthand factory for the success path.</summary>
    public static WopiNewChildFileResult Success(IWopiWritableFile file) =>
        new() { Outcome = WopiNewChildFileOutcome.Success, File = file };

    /// <summary>
    /// Shorthand factory for the 400-bad-request path. Optionally carries a sanitised
    /// alternative the controller will surface via <c>X-WOPI-ValidRelativeTarget</c>.
    /// </summary>
    public static WopiNewChildFileResult BadRequest(string? validRelativeTargetSuggestion = null) =>
        new() { Outcome = WopiNewChildFileOutcome.BadRequest, ValidRelativeTargetSuggestion = validRelativeTargetSuggestion };

    /// <summary>Shorthand factory for the 409-name-conflict path.</summary>
    public static WopiNewChildFileResult Conflict(string validRelativeTargetSuggestion) =>
        new() { Outcome = WopiNewChildFileOutcome.Conflict, ValidRelativeTargetSuggestion = validRelativeTargetSuggestion };

    /// <summary>Shorthand factory for the 409-lock-mismatch path.</summary>
    public static WopiNewChildFileResult Locked(string existingLockId) =>
        new() { Outcome = WopiNewChildFileOutcome.Locked, ExistingLockId = existingLockId };

    /// <summary>Shorthand factory for the 500-internal-server-error path (provider returned null
    /// from a create call that's contractually expected to succeed).</summary>
    public static WopiNewChildFileResult InternalError() =>
        new() { Outcome = WopiNewChildFileOutcome.InternalError };
}

/// <summary>
/// Categorical outcome of <see cref="IWopiNewChildFileNegotiator.NegotiateAsync"/>.
/// </summary>
public enum WopiNewChildFileOutcome
{
    /// <summary>A file is ready for the caller — newly created or an existing file under overwrite.</summary>
    Success,

    /// <summary>The proposed name failed validation, or both headers were missing.</summary>
    BadRequest,

    /// <summary>The target exists and the caller did not opt into overwrite.
    /// <see cref="WopiNewChildFileResult.ValidRelativeTargetSuggestion"/> holds the deduplicated name.</summary>
    Conflict,

    /// <summary>The target exists, the caller opted into overwrite, but the existing file is
    /// WOPI-locked. <see cref="WopiNewChildFileResult.ExistingLockId"/> holds the lock id.</summary>
    Locked,

    /// <summary>The storage provider returned <see langword="null"/> from a create call. Shouldn't
    /// happen with the in-tree providers but guarded defensively.</summary>
    InternalError,
}
