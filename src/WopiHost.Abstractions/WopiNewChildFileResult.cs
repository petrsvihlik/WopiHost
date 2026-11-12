namespace WopiHost.Abstractions;

/// <summary>
/// Outcome of <see cref="IWopiNewChildFileNegotiator.NegotiateAsync"/>. Pure POCO — no
/// HTTP types — so the negotiator stays testable without a request context; the endpoint
/// maps each case to its WOPI response at the HTTP boundary.
/// </summary>
/// <remarks>
/// A closed hierarchy: the nested records are the only possible cases, so a <c>switch</c>
/// over them is exhaustive without a discard arm.
/// </remarks>
public closed record WopiNewChildFileResult
{
    /// <summary>
    /// A file is ready for the caller — newly created, or an existing file the caller asked to
    /// overwrite. Typed as <see cref="IWopiWritableFile"/> because callers always proceed by
    /// writing contents (PutRelativeFile) or by returning metadata for a file that will accept a
    /// subsequent PutFile (CreateChildFile).
    /// </summary>
    /// <param name="File">The file to proceed with.</param>
    public sealed record Success(IWopiWritableFile File) : WopiNewChildFileResult;

    /// <summary>The proposed name failed validation, or both target headers were missing.</summary>
    /// <param name="ValidRelativeTargetSuggestion">
    /// A sanitised alternative to advertise via <c>X-WOPI-ValidRelativeTarget</c>, when the host
    /// can suggest one. The spec allows the header on a 400 when the requested name contained
    /// invalid characters.
    /// </param>
    public sealed record BadRequest(string? ValidRelativeTargetSuggestion = null) : WopiNewChildFileResult;

    /// <summary>The target exists and the caller did not opt into overwrite.</summary>
    /// <param name="ValidRelativeTargetSuggestion">The deduplicated name to advertise via <c>X-WOPI-ValidRelativeTarget</c>.</param>
    public sealed record Conflict(string ValidRelativeTargetSuggestion) : WopiNewChildFileResult;

    /// <summary>The target exists and the caller opted into overwrite, but the existing file is WOPI-locked.</summary>
    /// <param name="ExistingLockId">The current lock id, surfaced on the 409 lock-mismatch response.</param>
    public sealed record Locked(string ExistingLockId) : WopiNewChildFileResult;

    /// <summary>
    /// The storage provider returned <see langword="null"/> from a create call that is
    /// contractually expected to succeed.
    /// </summary>
    public sealed record InternalError : WopiNewChildFileResult;

    /// <summary>
    /// The caller may invoke <c>PutRelativeFile</c> but may not overwrite the existing target
    /// file. Maps to <c>501 Not Implemented</c> per the
    /// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile">PutRelativeFile spec</see>;
    /// see <see cref="IWopiPermissionProvider.CanOverwriteFileAsync"/> for the gating decision.
    /// </summary>
    public sealed record NotImplemented : WopiNewChildFileResult;
}
