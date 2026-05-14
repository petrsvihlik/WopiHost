using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WopiHost.Abstractions;
using WopiHost.Core.Results;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Shared <c>X-WOPI-SuggestedTarget</c> / <c>X-WOPI-RelativeTarget</c> /
/// <c>X-WOPI-OverwriteRelativeTarget</c> resolution dance for the two WOPI operations that
/// implement it identically per spec:
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile">PutRelativeFile</see>
/// and
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/createchildfile">CreateChildFile</see>.
/// </summary>
/// <remarks>
/// The only spec-mandated difference between the two flows is the suggested-target "starts with
/// dot" fallback: PutRelativeFile combines the dotted extension with the original file's name
/// stem (so <c>.docx</c> on <c>memo.pdf</c> becomes <c>memo.docx</c>); CreateChildFile has no
/// source file in scope and must invent a stem (the original code used <c>Guid.NewGuid("N")</c>).
/// The <c>suggestedExtensionFallbackStem</c> parameter on <see cref="ResolveAsync"/> carries
/// that distinction so the rest of the dance — name validation, existing-target lock probe,
/// suggested-name dedup — is implemented once.
/// </remarks>
internal static class WopiChildFileResolver
{
    /// <summary>
    /// Resolves the target for a PutRelativeFile- or CreateChildFile-shaped request and either
    /// returns the file the controller should proceed with (newly created, or an existing one
    /// the caller asked to overwrite) or an <see cref="IActionResult"/> short-circuit.
    /// </summary>
    /// <param name="storage">Read-side storage provider.</param>
    /// <param name="writable">Writable storage provider.</param>
    /// <param name="lockProvider">Lock provider (optional — overwrite probes skip the lock check
    /// when null).</param>
    /// <param name="response">Current HTTP response. The helper sets the
    /// <see cref="WopiHeaders.VALID_RELATIVE_TARGET"/> response header on the 409-conflict path.</param>
    /// <param name="containerId">Parent container id. For PutRelativeFile pass the resolved
    /// parent-of-file container id; for CreateChildFile pass the route container id.</param>
    /// <param name="suggestedTarget">Value of the <c>X-WOPI-SuggestedTarget</c> header.</param>
    /// <param name="relativeTarget">Value of the <c>X-WOPI-RelativeTarget</c> header.</param>
    /// <param name="overwriteRelativeTarget">Value of the <c>X-WOPI-OverwriteRelativeTarget</c> header.</param>
    /// <param name="suggestedExtensionFallbackStem">Stem to prepend when
    /// <paramref name="suggestedTarget"/> begins with a dot. Pass the original file's name on
    /// PutRelativeFile; a fresh GUID on CreateChildFile.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<WopiChildFileResolution> ResolveAsync(
        IWopiStorageProvider storage,
        IWopiWritableStorageProvider writable,
        IWopiLockProvider? lockProvider,
        HttpResponse response,
        string containerId,
        UtfString? suggestedTarget,
        UtfString? relativeTarget,
        bool overwriteRelativeTarget,
        string suggestedExtensionFallbackStem,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(writable);
        ArgumentNullException.ThrowIfNull(response);

        // "specific mode" — the host must not modify the name to fulfill the request.
        if (!string.IsNullOrWhiteSpace(relativeTarget))
        {
            return await ResolveRelativeAsync(
                storage, writable, lockProvider, response,
                containerId, relativeTarget, overwriteRelativeTarget, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(suggestedTarget))
        {
            return await ResolveSuggestedAsync(
                writable,
                containerId, suggestedTarget, suggestedExtensionFallbackStem, cancellationToken).ConfigureAwait(false);
        }

        // Both headers missing. The two callers in the controller layer already short-circuit
        // this case (and the "both present" case) with 501 NotImplemented before invoking the
        // helper, so this branch is defense-in-depth for any direct caller that skips the
        // pre-check; matches the original 400 fallback shape.
        return WopiChildFileResolution.Fail(new BadRequestResult());
    }

    private static async Task<WopiChildFileResolution> ResolveRelativeAsync(
        IWopiStorageProvider storage,
        IWopiWritableStorageProvider writable,
        IWopiLockProvider? lockProvider,
        HttpResponse response,
        string containerId,
        string relativeTarget,
        bool overwriteRelativeTarget,
        CancellationToken cancellationToken)
    {
        if (!await writable.CheckValidFileName(relativeTarget, cancellationToken).ConfigureAwait(false))
        {
            // 400 Bad Request – Specified name is illegal.
            return WopiChildFileResolution.Fail(new BadRequestResult());
        }

        var existing = await storage.GetWopiFileByName(containerId, relativeTarget, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            // No collision — create the new file.
            var created = await writable.CreateWopiChildFile(containerId, relativeTarget, cancellationToken).ConfigureAwait(false);
            return created is not null
                ? WopiChildFileResolution.Success(created)
                : WopiChildFileResolution.Fail(new InternalServerErrorResult());
        }

        if (!overwriteRelativeTarget)
        {
            // The host might include an X-WOPI-ValidRelativeTarget specifying a file name that's valid.
            // Pre-#420 #1.1: FilesController.PutRelativeFile was passing the file id here instead of
            // the parent container id, which made GetSuggestedFileName resolve against the wrong
            // location and degenerate to echoing the requested name back. The typed-API split + this
            // extraction pin the correct containerId in one place.
            var suggestedName = await writable.GetSuggestedFileName(containerId, relativeTarget, cancellationToken).ConfigureAwait(false);
            response.Headers[WopiHeaders.VALID_RELATIVE_TARGET] = UtfString.FromDecoded(suggestedName).ToString(true);
            return WopiChildFileResolution.Fail(new ConflictResult());
        }

        // Overwrite is allowed — a file matching the target name might still be locked.
        var existingLock = lockProvider is null
            ? null
            : await lockProvider.GetLockAsync(existing.Identifier, cancellationToken).ConfigureAwait(false);
        if (existingLock is not null)
        {
            return WopiChildFileResolution.Fail(
                new LockMismatchResult(response, existingLock.LockId, reason: "File already exists and is currently locked"));
        }
        // Overwrite-allowed + unlocked — caller proceeds with the existing file (PutRelativeFile
        // writes the request body over it; CreateChildFile just returns its metadata).
        return WopiChildFileResolution.Success(existing);
    }

    private static async Task<WopiChildFileResolution> ResolveSuggestedAsync(
        IWopiWritableStorageProvider writable,
        string containerId,
        string suggestedTarget,
        string suggestedExtensionFallbackStem,
        CancellationToken cancellationToken)
    {
        var suggestedTargetString = suggestedTarget;
        if (suggestedTargetString.StartsWith('.'))
        {
            // Extension-only fallback — combine with the caller-supplied stem so the resulting
            // file name is well-formed. PutRelativeFile uses the original file's name; CreateChildFile
            // uses a fresh GUID (it has no source file in scope).
            suggestedTargetString = suggestedExtensionFallbackStem + suggestedTargetString;
        }
        else if (!await writable.CheckValidFileName(suggestedTargetString, cancellationToken).ConfigureAwait(false))
        {
            return WopiChildFileResolution.Fail(new BadRequestResult());
        }

        var newName = await writable.GetSuggestedFileName(containerId, suggestedTargetString, cancellationToken).ConfigureAwait(false);
        var created = await writable.CreateWopiChildFile(containerId, newName, cancellationToken).ConfigureAwait(false);
        return created is not null
            ? WopiChildFileResolution.Success(created)
            : WopiChildFileResolution.Fail(new InternalServerErrorResult());
    }
}

/// <summary>
/// Outcome of <see cref="WopiChildFileResolver.ResolveAsync"/>. Exactly one of
/// <see cref="NewFile"/> and <see cref="Error"/> is non-null.
/// </summary>
internal readonly struct WopiChildFileResolution
{
    /// <summary>File the caller should proceed with — either freshly created or an existing
    /// one the caller asked to overwrite. <see langword="null"/> when the helper short-
    /// circuited with an <see cref="Error"/>.</summary>
    public IWopiFile? NewFile { get; }

    /// <summary>Short-circuit result to return from the controller. <see langword="null"/>
    /// on the success path.</summary>
    public IActionResult? Error { get; }

    private WopiChildFileResolution(IWopiFile? newFile, IActionResult? error)
    {
        NewFile = newFile;
        Error = error;
    }

    public static WopiChildFileResolution Success(IWopiFile file) => new(file, null);
    public static WopiChildFileResolution Fail(IActionResult error) => new(null, error);
}
