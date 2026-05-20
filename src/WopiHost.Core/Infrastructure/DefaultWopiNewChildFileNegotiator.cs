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
            // Spec: 400 MAY include X-WOPI-ValidRelativeTarget so the WOPI client can auto-retry.
            // Compute a sanitised stem + original extension, then dedupe via GetSuggestedFileName.
            // The suggestion is best-effort — when the sanitised candidate still fails validation
            // (provider rules beyond forbidden-char swap, e.g. reserved names like CON on Windows),
            // we omit the header rather than emit a name we know is invalid.
            var sanitised = await TryBuildValidSuggestionAsync(request, relativeTarget, cancellationToken).ConfigureAwait(false);
            return WopiNewChildFileResult.BadRequest(sanitised);
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
            // Spec: "The response to a request including [X-WOPI-SuggestedTarget] must never
            // result in a 400 Bad Request or 409 Conflict. Rather, the host must modify the
            // proposed name as needed to create a new file that's both legally named and doesn't
            // overwrite any existing file, while preserving the file extension."
            // Try sanitising the requested name first; if that still fails, fall back to the
            // caller-supplied stem + original extension (the same shape we use for
            // extension-only inputs).
            suggestedTarget = await TryBuildValidSuggestionAsync(request, suggestedTarget, cancellationToken).ConfigureAwait(false)
                ?? request.SuggestedExtensionFallbackStem + ExtractExtension(suggestedTarget);
        }

        var newName = await writable.GetSuggestedFileName(request.ContainerId, suggestedTarget, cancellationToken).ConfigureAwait(false);
        var created = await writable.CreateWopiChildFile(request.ContainerId, newName, cancellationToken).ConfigureAwait(false);
        return created is not null
            ? WopiNewChildFileResult.Success(created)
            : WopiNewChildFileResult.InternalError();
    }

    /// <summary>
    /// Attempts to sanitise <paramref name="invalidName"/> into a name that passes
    /// <see cref="IWopiWritableStorageProvider.CheckValidFileName"/>, preserving the original
    /// extension. Replaces forbidden filesystem characters with <c>_</c>; if the sanitised stem
    /// is empty or path-nav (<c>.</c>/<c>..</c>), substitutes the request's fallback stem.
    /// Returns the dedup-suggested name on success, or <see langword="null"/> when the sanitised
    /// candidate still fails validation (caller decides whether to swallow or surface the failure).
    /// </summary>
    private async Task<string?> TryBuildValidSuggestionAsync(WopiNewChildFileRequest request, string invalidName, CancellationToken cancellationToken)
    {
        var ext = ExtractExtension(invalidName);
        var stem = ext.Length == 0 ? invalidName : invalidName[..^ext.Length];

        // Mirrors ToSafeIdentity's forbidden-char set plus the cross-platform filesystem
        // unsafe characters. Providers may apply stricter rules (Windows reserved names, etc.),
        // which we re-check via CheckValidFileName below.
        const string forbiddenChars = "<>:\"/\\|?* ";
        var sanitisedStem = forbiddenChars.Aggregate(stem, (cur, c) => cur.Replace(c, '_')).Trim();
        if (string.IsNullOrWhiteSpace(sanitisedStem) || sanitisedStem is "." or "..")
        {
            sanitisedStem = request.SuggestedExtensionFallbackStem;
        }

        var candidate = sanitisedStem + ext;
        if (!await writable.CheckValidFileName(candidate, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }
        return await writable.GetSuggestedFileName(request.ContainerId, candidate, cancellationToken).ConfigureAwait(false);
    }

    private static string ExtractExtension(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[dot..] : string.Empty;
    }
}
