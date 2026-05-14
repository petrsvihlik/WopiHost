using WopiHost.Abstractions;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Default <see cref="IWopiNewChildFileNegotiator"/>. Implements the WOPI suggested-target /
/// relative-target / overwrite-relative-target protocol verbatim from the spec — same logic
/// for both <c>PutRelativeFile</c> and <c>CreateChildFile</c>; the spec defines the dance
/// identically across both operations.
/// </summary>
public sealed class DefaultWopiNewChildFileNegotiator(
    IWopiStorageProvider storage,
    IWopiWritableStorageProvider writable,
    IWopiLockProvider? lockProvider = null) : IWopiNewChildFileNegotiator
{
    /// <inheritdoc />
    public async Task<WopiNewChildFileResult> NegotiateAsync(WopiNewChildFileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // "specific mode" — the host must not modify the name to fulfill the request.
        if (!string.IsNullOrWhiteSpace(request.RelativeTarget))
        {
            return await NegotiateRelativeAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(request.SuggestedTarget))
        {
            return await NegotiateSuggestedAsync(request, cancellationToken).ConfigureAwait(false);
        }

        // Both headers missing. Controllers already short-circuit this case (and the "both
        // present" case) with 501 NotImplemented before invoking the negotiator, so this
        // branch is defense-in-depth for direct callers that skip the pre-check.
        return WopiNewChildFileResult.BadRequest();
    }

    private async Task<WopiNewChildFileResult> NegotiateRelativeAsync(WopiNewChildFileRequest request, CancellationToken cancellationToken)
    {
        var relativeTarget = request.RelativeTarget!;
        if (!await writable.CheckValidFileName(relativeTarget, cancellationToken).ConfigureAwait(false))
        {
            return WopiNewChildFileResult.BadRequest();
        }

        var existing = await storage.GetWopiFileByName(request.ContainerId, relativeTarget, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            // No collision — create the new file.
            var created = await writable.CreateWopiChildFile(request.ContainerId, relativeTarget, cancellationToken).ConfigureAwait(false);
            return created is not null
                ? WopiNewChildFileResult.Success(created)
                : WopiNewChildFileResult.InternalError();
        }

        if (!request.OverwriteRelativeTarget)
        {
            // Suggest a deduplicated alternative for X-WOPI-ValidRelativeTarget.
            // Pre-#420 #1.1: PutRelativeFile was passing the file id here instead of the
            // parent container id, so GetSuggestedFileName resolved against the wrong location
            // and degenerated to echoing the requested name back. Centralizing here pins the
            // correct containerId in one place.
            var suggestedName = await writable.GetSuggestedFileName(request.ContainerId, relativeTarget, cancellationToken).ConfigureAwait(false);
            return WopiNewChildFileResult.Conflict(suggestedName);
        }

        // Overwrite is allowed — a file matching the target name might still be locked.
        // Lock probe inlined (no helper method) so Infer# can see through it — cross-method
        // async helpers trip its null-deref FP, see PR #424 review feedback and #363 / #412.
        if (lockProvider is not null)
        {
            var existingLock = await lockProvider.GetLockAsync(existing.Identifier, cancellationToken).ConfigureAwait(false);
            if (existingLock is not null)
            {
                return WopiNewChildFileResult.Locked(existingLock.LockId);
            }
        }

        // Overwrite-allowed + unlocked — upgrade the existing file (fetched via the read-side
        // interface as IWopiFile) to IWopiWritableFile so the caller can mutate it. After
        // #420 item 1.2 the write seam is gated by the writable interface; the read-side
        // GetWopiFileByName intentionally returns the narrower IWopiFile.
        var writableExisting = await writable.GetWritableFile(existing.Identifier, cancellationToken).ConfigureAwait(false);
        return writableExisting is not null
            ? WopiNewChildFileResult.Success(writableExisting)
            : WopiNewChildFileResult.InternalError();
    }

    private async Task<WopiNewChildFileResult> NegotiateSuggestedAsync(WopiNewChildFileRequest request, CancellationToken cancellationToken)
    {
        var suggestedTarget = request.SuggestedTarget!;
        if (suggestedTarget.StartsWith('.'))
        {
            // Extension-only fallback — combine with the caller-supplied stem so the resulting
            // file name is well-formed. PutRelativeFile uses the original file's name;
            // CreateChildFile uses a fresh GUID (it has no source file in scope).
            suggestedTarget = request.SuggestedExtensionFallbackStem + suggestedTarget;
        }
        else if (!await writable.CheckValidFileName(suggestedTarget, cancellationToken).ConfigureAwait(false))
        {
            return WopiNewChildFileResult.BadRequest();
        }

        var newName = await writable.GetSuggestedFileName(request.ContainerId, suggestedTarget, cancellationToken).ConfigureAwait(false);
        var created = await writable.CreateWopiChildFile(request.ContainerId, newName, cancellationToken).ConfigureAwait(false);
        return created is not null
            ? WopiNewChildFileResult.Success(created)
            : WopiNewChildFileResult.InternalError();
    }
}
