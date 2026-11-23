namespace WopiHost.Abstractions;

/// <summary>
/// Implements the WOPI <c>X-WOPI-SuggestedTarget</c> / <c>X-WOPI-RelativeTarget</c> /
/// <c>X-WOPI-OverwriteRelativeTarget</c> name negotiation shared by
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile">PutRelativeFile</see>
/// and
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/createchildfile">CreateChildFile</see>.
/// </summary>
/// <remarks>
/// <para>
/// Both operations propose a child-file name (suggested or specific); the host either creates
/// the new file, returns an existing file the caller asked to overwrite, or reports a
/// <c>400</c>, <c>409</c>, or lock-conflict outcome.
/// </para>
/// <para>
/// The default implementation in <c>WopiHost.Core</c> follows the spec verbatim. Replace
/// via DI to customize naming policy (e.g. enforce a per-user prefix, normalize Unicode,
/// snap to a quota).
/// </para>
/// </remarks>
public interface IWopiNewChildFileNegotiator
{
    /// <summary>
    /// Runs the suggested/relative-target negotiation for a child file under
    /// <see cref="WopiNewChildFileRequest.ContainerId"/>.
    /// </summary>
    /// <param name="request">The target container, the requesting user, and the parsed
    /// target headers.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The negotiated file, or the reason no file can be produced.</returns>
    Task<WopiNewChildFileResult> NegotiateAsync(WopiNewChildFileRequest request, CancellationToken cancellationToken = default);
}
