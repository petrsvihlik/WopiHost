namespace WopiHost.Abstractions;

/// <summary>
/// Implements the WOPI <c>X-WOPI-SuggestedTarget</c> / <c>X-WOPI-RelativeTarget</c> /
/// <c>X-WOPI-OverwriteRelativeTarget</c> name-negotiation protocol that
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile">PutRelativeFile</see>
/// and
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/createchildfile">CreateChildFile</see>
/// share. Both operations propose a child-file name (suggested or specific), and the host
/// either creates the new file, returns an existing file the caller asked to overwrite,
/// or surfaces a 400 / 409 / lock-conflict outcome.
/// </summary>
/// <remarks>
/// The default implementation in <c>WopiHost.Core</c> follows the spec verbatim. Replace
/// via DI to customize naming policy (e.g. enforce a per-user prefix, normalize Unicode,
/// snap to a quota).
/// </remarks>
public interface IWopiNewChildFileNegotiator
{
    /// <summary>
    /// Runs the suggested/relative-target negotiation for a child file under
    /// <see cref="WopiNewChildFileRequest.ContainerId"/>.
    /// </summary>
    Task<WopiNewChildFileResult> NegotiateAsync(WopiNewChildFileRequest request, CancellationToken cancellationToken = default);
}
