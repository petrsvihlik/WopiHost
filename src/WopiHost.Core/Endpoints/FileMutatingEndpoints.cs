using System.Collections.ObjectModel;
using System.Net.Mime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Results;

// Shared typed-union for ProcessLock / ProcessLockCore and the Lock / UNLOCK / REFRESH_LOCK /
// GET_LOCK / UNLOCK_AND_RELOCK sub-helpers. Declared as a file-scoped alias so every call site
// in the lock dispatch agrees on the same union shape — Results<T...> doesn't widen narrower
// unions automatically.
using ProcessLockResult = Microsoft.AspNetCore.Http.HttpResults.Results<
    Microsoft.AspNetCore.Http.HttpResults.NotFound,
    Microsoft.AspNetCore.Http.HttpResults.Ok,
    Microsoft.AspNetCore.Http.HttpResults.BadRequest,
    WopiHost.Core.Results.WopiLockMismatchResult,
    Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult>;

// Shared typed-union for PutRelativeFile. Spec branches: 404 (missing), 200 ChildFile json,
// 501 (mutex headers), 412/413 (size), 400/409 from name negotiation, lock-mismatch when the
// target exists and is locked, 500 on negotiator internal error.
using PutRelativeFileResult = Microsoft.AspNetCore.Http.HttpResults.Results<
    Microsoft.AspNetCore.Http.HttpResults.NotFound,
    Microsoft.AspNetCore.Http.HttpResults.JsonHttpResult<WopiHost.Core.Models.ChildFile>,
    Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult,
    Microsoft.AspNetCore.Http.HttpResults.BadRequest,
    Microsoft.AspNetCore.Http.HttpResults.Conflict,
    WopiHost.Core.Results.WopiLockMismatchResult>;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// Mutating Minimal-API endpoints for the <c>/wopi/files/{id}</c> surface. POST-on-<c>{id}</c>
/// overloads dispatch via the <c>X-WOPI-Override</c> header
/// (<see cref="WopiOverrideMatcherPolicy"/>).
/// </summary>
internal static class FileMutatingEndpoints
{
    private const string UserInfoCacheKeyPrefix = "UserInfo-";

    public static void MapFileMutatingEndpoints(RouteGroupBuilder files)
    {
        // The writable-storage gate applies to every endpoint here except PutUserInfo (which
        // writes to MemoryCache, not storage). Hoist it onto a sub-group instead of repeating
        // the .AddEndpointFilter call eight times.
        var mutating = files.MapGroup("")
            .AddEndpointFilter<RequiresWritableStorageEndpointFilter>();

        mutating.MapMethods("/{id}/contents", ["PUT", "POST"], PutFile)
            .WithSummary("PutFile — writes the request body as the file's contents.")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putfile. " +
                "Returns 409 with X-WOPI-Lock on a lock mismatch, 413 when the body exceeds WopiHostOptions.MaxFileSize.")
            .RequireWopiPermission(WopiResourceType.File, Permission.Update);

        // All POST overloads on /{id} share a route + verb and are discriminated by
        // X-WOPI-Override via WopiOverrideMatcherPolicy. Each is its own endpoint with its own
        // RequireAuthorization so per-override permissions stay distinct.
        mutating.MapPost("/{id}", RenameFile)
            .WithMetadata(new WopiOverrideMetadata(WopiFileOperations.RenameFile))
            .WithSummary("RenameFile (X-WOPI-Override: RENAME_FILE).")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/renamefile. " +
                "Sanitises forbidden filesystem characters before declaring 400.")
            .RequireWopiPermission(WopiResourceType.File, Permission.Rename);

        mutating.MapPost("/{id}", PutRelativeFile)
            .WithMetadata(new WopiOverrideMetadata(WopiFileOperations.PutRelativeFile))
            .WithSummary("PutRelativeFile (X-WOPI-Override: PUT_RELATIVE).")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile. " +
                "Creates a new file relative to this file using suggested-target or relative-target negotiation.")
            .RequireWopiPermission(WopiResourceType.File, Permission.Create);

        // PutUserInfo writes only to MemoryCache — exempt from the writable-storage gate so
        // hosts without writable storage can still cache per-user info — so register it on the
        // parent group, not the mutating sub-group.
        files.MapPost("/{id}", PutUserInfo)
            .WithMetadata(new WopiOverrideMetadata(WopiFileOperations.PutUserInfo))
            .WithSummary("PutUserInfo (X-WOPI-Override: PUT_USER_INFO).")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putuserinfo. " +
                "Body size capped at 1024 bytes per spec; oversized bodies yield 400.");

        mutating.MapPost("/{id}", DeleteFile)
            .WithMetadata(new WopiOverrideMetadata(WopiFileOperations.DeleteFile))
            .WithSummary("DeleteFile (X-WOPI-Override: DELETE).")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/deletefile. " +
                "Returns 409 with X-WOPI-Lock when the file is locked.")
            .RequireWopiPermission(WopiResourceType.File, Permission.Delete);

        mutating.MapPost("/{id}", ProcessCobalt)
            .WithMetadata(new WopiOverrideMetadata(WopiFileOperations.Cobalt))
            .WithSummary("ProcessCobalt (X-WOPI-Override: COBALT) — Cobalt sub-protocol body.")
            .WithDescription("Spec: MS-FSSHTTP. Requires an ICobaltProcessor to be registered; 500 if missing.")
            .RequireWopiPermission(WopiResourceType.File, Permission.Update);

        // ProcessLock: multiplexed across 5 override values, kept on one endpoint because the
        // WOPI spec treats them as a single state machine and per-spec all five share the
        // Update permission. Sub-dispatch happens inside the handler.
        mutating.MapPost("/{id}", ProcessLock)
            .WithMetadata(new WopiOverrideMetadata(
                WopiFileOperations.Lock,
                WopiFileOperations.Put,
                WopiFileOperations.Unlock,
                WopiFileOperations.RefreshLock,
                WopiFileOperations.GetLock))
            .WithSummary("Lock state machine (Lock / UNLOCK / REFRESH_LOCK / GET_LOCK / UNLOCK_AND_RELOCK).")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/lock. " +
                "Dispatches by X-WOPI-Override inside the handler. Returns 409 with X-WOPI-Lock on every mismatch path.")
            .RequireWopiPermission(WopiResourceType.File, Permission.Update);
    }

    private static async Task<Results<NotFound, Ok, WopiLockMismatchResult, StatusCodeHttpResult>> PutFile(
        [AsParameters] PutFileRequest req)
    {
        // RequiresWritableStorageEndpointFilter already short-circuited with 501 if
        // WritableStorage was missing — the assert is defensive against pipeline misorder.
        ArgumentNullException.ThrowIfNull(req.WritableStorage);
        var file = await req.WritableStorage.GetWritableFile(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();
        if (CheckMaxFileSize(req.Http, req.Options.Value.MaxFileSize) is { } tooLarge) return tooLarge;

        // PutFile branches on the FILE'S current lock state, not on whether X-WOPI-Lock was sent.
        var existingLock = req.LockProvider is not null
            ? await req.LockProvider.GetLockAsync(req.Id, req.CancellationToken).ConfigureAwait(false)
            : null;

        if (ValidatePutFileLock(existingLock, req.RequestLockId, req.LockComparer, file) is { } mismatch)
        {
            return mismatch;
        }
        return await WriteAndAck(req.Http, req.Extensions, file, req.Editors, req.CancellationToken).ConfigureAwait(false);
    }

    // Decides whether a PutFile may proceed, given the file's current lock state. Returns the 409
    // result to short-circuit with, or null when the write may proceed.
    //
    // Keying off the file's lock (not the request header) is spec-mandated: a locked file requires
    // X-WOPI-Lock to match the stored id (missing/empty counts as a mismatch), while an unlocked
    // file admits only the 0-byte create-new flow — any other size is a 409 carrying the empty
    // placeholder lock id, since an unlocked file has no id to surface.
    // Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putfile.
    private static WopiLockMismatchResult? ValidatePutFileLock(
        WopiLockInfo? existingLock, string? requestLockId, IWopiLockComparer comparer, IWopiWritableFile file)
    {
        if (existingLock is not null)
        {
            return string.IsNullOrEmpty(requestLockId) || !comparer.AreEqual(existingLock.LockId, requestLockId)
                ? new WopiLockMismatchResult(existingLock.LockId)
                : null;
        }
        // Read Length only on the unlocked path. Touching it on the locked path would prime the
        // file's metadata cache before the write, making WriteAndAck report a stale post-write
        // version.
        return file.Length != 0 ? new WopiLockMismatchResult(existingLock: null) : null;
    }

    private static async Task<Ok> WriteAndAck(HttpContext httpContext, IWopiHostExtensions extensions, IWopiWritableFile file, string? editors, CancellationToken cancellationToken)
    {
        await httpContext.CopyToWriteStream(file, cancellationToken).ConfigureAwait(false);
        if (file.Version is not null)
        {
            httpContext.Response.Headers[WopiHeaders.ItemVersion] = file.Version;
        }
        await InvokePutFileCallbackAsync(httpContext, extensions, file, editors, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok();
    }

    private static async Task<Results<NotFound, JsonHttpResult<RenameFileResponse>, BadRequest, Conflict, WopiLockMismatchResult>> RenameFile(
        [AsParameters] RenameFileRequest req)
    {
        ArgumentNullException.ThrowIfNull(req.WritableStorage);
        var file = await req.Storage.GetWopiFile(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        if (req.LockProvider is not null
            && await req.LockProvider.GetLockAsync(req.Id, req.CancellationToken).ConfigureAwait(false) is { } existingLock
            && !req.LockComparer.AreEqual(existingLock.LockId, req.LockIdentifier))
        {
            return new WopiLockMismatchResult(existingLock.LockId);
        }

        // Spec: "X-WOPI-RequestedName ... a UTF-7 encoded string that's a file name, not including
        // the file extension." Append the source file's extension so providers can dedup against
        // sibling files with the same stem (foo.txt, foo (1).txt, ...).
        var requestedFullName = req.RequestedName + '.' + file.Extension;

        // Parent container id is the dedup scope for GetSuggestedFileName — passing the file id
        // here degenerates the provider into echoing the requested name back. The storage
        // contract is GetSuggestedFileName(CONTAINER, name).
        var ancestors = await req.Storage.GetFileAncestors(req.Id, req.CancellationToken).ConfigureAwait(false);
        var parentContainer = ancestors.LastOrDefault()
            ?? throw new ArgumentException("Cannot find parent container", nameof(req));

        // Spec: "If the host can't rename the file because the name requested is invalid or
        // conflicts with an existing file, the host should try to generate a different name
        // based on the requested name that meets the file name requirements." Try sanitisation
        // first — only return 400 if no valid candidate can be computed.
        if (!await req.WritableStorage.CheckValidFileName(requestedFullName, req.CancellationToken).ConfigureAwait(false))
        {
            var sanitised = await TryBuildValidFileNameAsync(req.WritableStorage, requestedFullName, fallbackStem: file.Name, req.CancellationToken).ConfigureAwait(false);
            if (sanitised is null)
            {
                req.Http.Response.Headers[WopiHeaders.InvalidFileName] = "Specified name is illegal";
                return TypedResults.BadRequest();
            }
            requestedFullName = sanitised;
        }

        return await TryRenameFileAsync(req.Http, req.WritableStorage, req.Id, parentContainer.Identifier, requestedFullName, req.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<Results<NotFound, JsonHttpResult<RenameFileResponse>, BadRequest, Conflict, WopiLockMismatchResult>> TryRenameFileAsync(HttpContext httpContext, IWopiWritableStorageProvider writableStorage, string id, string parentContainerId, string requestedFullName, CancellationToken cancellationToken)
    {
        try
        {
            var newName = await writableStorage.GetSuggestedFileName(parentContainerId, requestedFullName, cancellationToken).ConfigureAwait(false);
            return await writableStorage.RenameWopiFile(id, newName, cancellationToken).ConfigureAwait(false)
                ? TypedResults.Json(new RenameFileResponse(Path.GetFileNameWithoutExtension(newName)))
                : TypedResults.NotFound(); // false → missing resource (race with concurrent delete).
        }
        catch (ArgumentException)
        {
            httpContext.Response.Headers[WopiHeaders.InvalidFileName] = "Specified name is illegal";
            return TypedResults.BadRequest();
        }
        catch (FileNotFoundException) { return TypedResults.NotFound(); }
        catch (InvalidOperationException) { return TypedResults.Conflict(); }
    }

    /// <summary>
    /// Scrubs <paramref name="invalidName"/> via <see cref="WopiFileNameSanitiser"/> and falls
    /// back to the source file's stem + the original extension when the scrubbed candidate is
    /// still rejected by the provider. RenameFile has no concept of "suggested target" — the
    /// source file's name is the only reasonable fallback — so the fallback shape differs from
    /// <see cref="DefaultWopiNewChildFileNegotiator"/> (which uses the caller-supplied stem) and
    /// can't be folded into the sanitiser helper.
    /// </summary>
    private static async Task<string?> TryBuildValidFileNameAsync(IWopiWritableStorageProvider writable, string invalidName, string fallbackStem, CancellationToken cancellationToken)
    {
        var candidate = await WopiFileNameSanitiser.TryBuildValidCandidateAsync(writable, invalidName, fallbackStem, cancellationToken).ConfigureAwait(false);
        if (candidate is not null) return candidate;

        // Scrubbed candidate failed CheckValidFileName — try one more swing with the literal
        // fallback stem + the original extension. The extra round-trip is the cost of keeping
        // the sanitiser caller-agnostic; in the unrecoverable-input case (sanitiser already
        // fell back to fallbackStem+ext) the second CheckValidFileName re-asks the same
        // question and returns the same answer — at most one redundant call per RenameFile
        // failure path, which is itself already a 400 response in flight.
        var ext = WopiFileNameSanitiser.ExtractExtension(invalidName);
        var fallback = fallbackStem + ext;
        return await writable.CheckValidFileName(fallback, cancellationToken).ConfigureAwait(false)
            ? fallback
            : null;
    }

    private static async Task<PutRelativeFileResult> PutRelativeFile(
        [AsParameters] PutRelativeFileRequest req)
    {
        ArgumentNullException.ThrowIfNull(req.WritableStorage);
        var file = await req.Storage.GetWopiFile(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        if (CheckMaxFileSize(req.Http, req.Options.Value.MaxFileSize, req.DeclaredSize) is { } tooLarge)
        {
            return tooLarge;
        }

        if (EndpointHelpers.EnsureExactlyOneOf(req.SuggestedTarget, req.RelativeTarget) is { } mutex)
        {
            return mutex;
        }

        var ancestors = await req.Storage.GetFileAncestors(req.Id, req.CancellationToken).ConfigureAwait(false);
        var parentContainer = ancestors.LastOrDefault()
            ?? throw new ArgumentException("Cannot find parent container", nameof(req));

        var negotiation = await req.Negotiator.NegotiateAsync(new WopiNewChildFileRequest(
            ContainerId: parentContainer.Identifier,
            SuggestedTarget: req.SuggestedTarget,
            RelativeTarget: req.RelativeTarget,
            OverwriteRelativeTarget: req.OverwriteRelativeTarget ?? false,
            SuggestedExtensionFallbackStem: file.Name,
            User: req.Http.User), req.CancellationToken).ConfigureAwait(false);
        if (negotiation.ToErrorResult(req.Http.Response) is { } error)
        {
            // Negotiator surfaces failure modes as IResult — destructure into the typed union.
            return error switch
            {
                BadRequest br => br,
                Conflict c => c,
                WopiLockMismatchResult w => w,
                StatusCodeHttpResult sc => sc,
                _ => throw new InvalidOperationException($"Unexpected negotiator error result: {error.GetType().FullName}"),
            };
        }

        var newFile = negotiation.File!;
        await req.Http.CopyToWriteStream(newFile, req.CancellationToken).ConfigureAwait(false);
        await InvokePutRelativeFileCallbackAsync(req.Http, req.Extensions, file, newFile, req.FileConversion, req.DeclaredSize, req.CancellationToken).ConfigureAwait(false);
        var capabilities = MakeCapabilities(req.LockProvider, req.CobaltProcessor);
        var checkFileInfo = await req.CheckFileInfoBuilder.BuildAsync(newFile, req.Http.ToWopiRequestInfo(), capabilities, cancellationToken: req.CancellationToken).ConfigureAwait(false);
        // Mint a fresh token bound to the NEW file's resource id; reusing the inbound token
        // (scoped to the source file) violates the WOPI "preventing token trading" guidance and
        // would fail downstream authorization for any host whose tokens encode resource id.
        // See IWopiResourceTokenMinter for the centralized mint.
        var newFileToken = await req.TokenMinter.MintForFileAsync(req.Http.User, newFile, req.CancellationToken).ConfigureAwait(false);
        return TypedResults.Json(new ChildFile(newFile.Name + '.' + newFile.Extension, req.Http.GetWopiSrc(newFile, newFileToken.Token))
        {
            HostEditUrl = checkFileInfo.HostEditUrl,
            HostViewUrl = checkFileInfo.HostViewUrl,
        });
    }

    /// <summary>
    /// Spec-defined max body size for PutUserInfo:
    /// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putuserinfo"/>.
    /// "The UserInfo string is provided in the body of the request, and has a maximum size of
    /// 1024 ASCII characters." Cap the read so a malicious or buggy client can't push an
    /// unbounded body into the MemoryCache.
    /// </summary>
    private const int PutUserInfoMaxBytes = 1024;

    private static async Task<Results<NotFound, BadRequest, Ok>> PutUserInfo(
        [AsParameters] PutUserInfoRequest req)
    {
        var file = await req.Storage.GetWopiFile(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        // Fast-fail when the declared Content-Length already exceeds the spec cap.
        if (req.Http.Request.ContentLength is long declared && declared > PutUserInfoMaxBytes)
        {
            return TypedResults.BadRequest();
        }

        // Bounded read: WithinLimit goes false the moment the body exceeds the spec cap mid-stream
        // (chunked transfer-encoding with no Content-Length, or clients that lie about it), so a
        // malicious or buggy client can't push an unbounded body into the MemoryCache.
        var (withinLimit, bytes) = await req.Http.Request.Body.ReadBytesAsync(PutUserInfoMaxBytes, req.CancellationToken).ConfigureAwait(false);
        if (!withinLimit)
        {
            return TypedResults.BadRequest();
        }

        var userInfo = System.Text.Encoding.UTF8.GetString(bytes);

        req.MemoryCache.Set(
            $"{UserInfoCacheKeyPrefix}{req.Http.User.GetUserId()}",
            userInfo,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = req.Options.Value.UserInfoCacheLifetime,
            });
        return TypedResults.Ok();
    }

    private static async Task<Results<NotFound, Ok, WopiLockMismatchResult>> DeleteFile(
        [AsParameters] DeleteFileRequest req)
    {
        ArgumentNullException.ThrowIfNull(req.WritableStorage);
        var file = await req.Storage.GetWopiFile(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        if (req.LockProvider is not null)
        {
            var existingLock = await req.LockProvider.GetLockAsync(req.Id, req.CancellationToken).ConfigureAwait(false);
            if (existingLock is not null)
            {
                return new WopiLockMismatchResult(existingLock.LockId);
            }
        }

        if (await req.WritableStorage.DeleteWopiFile(req.Id, req.CancellationToken).ConfigureAwait(false))
        {
            return TypedResults.Ok();
        }
        // false → missing resource (race with concurrent delete).
        return TypedResults.NotFound();
    }

    private static async Task<Results<NotFound, FileContentHttpResult>> ProcessCobalt(
        [AsParameters] ProcessCobaltRequest req)
    {
        ArgumentNullException.ThrowIfNull(req.WritableStorage);
        // Resolve the file before checking the Cobalt processor so a missing file surfaces as
        // 404 (per spec) regardless of whether the host has Cobalt wired up. Missing processor
        // is a host-config issue and falls through to ArgumentNullException → 500 below.
        var file = await req.WritableStorage.GetWritableFile(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();
        ArgumentNullException.ThrowIfNull(req.CobaltProcessor);

        var bytes = await req.Http.Request.Body.ReadBytesAsync(req.CancellationToken).ConfigureAwait(false);
        var responseBytes = await req.CobaltProcessor.ProcessCobalt(file, req.Http.User, bytes, req.CancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(req.CorrelationId))
        {
            req.Http.Response.Headers.Append(WopiHeaders.CorrelationId, req.CorrelationId);
            req.Http.Response.Headers.Append("request-id", req.CorrelationId);
        }
        return TypedResults.Bytes(responseBytes, MediaTypeNames.Application.Octet);
    }

    private static async Task<ProcessLockResult> ProcessLock(
        [AsParameters] ProcessLockRequest req)
    {
        var file = await req.Storage.GetWopiFile(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();
        req.Http.Response.Headers[WopiHeaders.ItemVersion] = file.Version;
        return await ProcessLockCore(req).ConfigureAwait(false);
    }

    private static async Task<ProcessLockResult> ProcessLockCore(ProcessLockRequest req)
    {
        if (req.LockProvider is null)
        {
            return new WopiLockMismatchResult(reason: "Locking is not supported");
        }
        if (ValidateLockHeaders(req.Http, req.WopiOverrideHeader, req.OldLockIdentifier, req.NewLockIdentifier) is { } headerError)
        {
            return headerError;
        }
        var op = new LockOperationRequest(
            req.Id, req.Http, req.LockProvider, req.Options, req.LockComparer,
            req.WopiOverrideHeader, req.OldLockIdentifier, req.NewLockIdentifier, req.CancellationToken);
        var existingLock = await req.LockProvider.GetLockAsync(req.Id, req.CancellationToken).ConfigureAwait(false);
        return await DispatchLockOperation(op, existingLock).ConfigureAwait(false);
    }

    /// <summary>
    /// Spec validation gate: enforces lock-id max length and X-WOPI-Lock-required-for-mutation
    /// rules <em>before</em> dispatching to the per-operation handler. Keeping it separate leaves
    /// ProcessLockCore as a pure dispatch state machine and lets the validation rules be tested
    /// or audited in isolation.
    /// </summary>
    /// <returns>The error result that should short-circuit the operation, or <see langword="null"/>
    /// when the headers pass validation.</returns>
    private static BadRequest? ValidateLockHeaders(
        HttpContext httpContext,
        string? wopiOverrideHeader,
        string? oldLockIdentifier,
        string? newLockIdentifier)
    {
        if ((newLockIdentifier is not null && newLockIdentifier.Length > WopiLockInfo.MaxLockIdLength)
            || (oldLockIdentifier is not null && oldLockIdentifier.Length > WopiLockInfo.MaxLockIdLength))
        {
            httpContext.Response.Headers[WopiHeaders.LockFailureReason] =
                $"Lock id exceeds maximum length of {WopiLockInfo.MaxLockIdLength} characters";
            return TypedResults.BadRequest();
        }
        // Spec: Lock, Unlock, RefreshLock, UnlockAndRelock ALL list 400 Bad Request — "X-WOPI-Lock
        // was not provided or was empty" — as a distinct status from 409 (lock mismatch). GetLock
        // doesn't require X-WOPI-Lock on the request, so skip the guard for it. Whitespace-only
        // values are practically empty and rejected here too.
        if (wopiOverrideHeader is WopiFileOperations.Lock or WopiFileOperations.Put
                or WopiFileOperations.Unlock or WopiFileOperations.RefreshLock
            && string.IsNullOrWhiteSpace(newLockIdentifier))
        {
            httpContext.Response.Headers[WopiHeaders.LockFailureReason] = "X-WOPI-Lock header is required";
            return TypedResults.BadRequest();
        }
        return null;
    }

    /// <summary>
    /// Maps the validated X-WOPI-Override header onto the appropriate Handle* implementation.
    /// Lock override doubles as UnlockAndRelock when X-WOPI-OldLock is present, so dispatch is
    /// by header presence rather than baking a third branch into the switch.
    /// </summary>
    /// <remarks>
    /// Unrecognised override values fall through to 501 NotImplemented — the outer
    /// <c>WopiOverrideMatcherPolicy</c> already restricts routing to the values listed in the
    /// endpoint metadata, so the default arm is defense-in-depth for anyone bypassing the policy.
    /// </remarks>
    private static async Task<ProcessLockResult> DispatchLockOperation(
        LockOperationRequest op,
        WopiLockInfo? existingLock) => op.WopiOverrideHeader switch
        {
            WopiFileOperations.GetLock => HandleGetLock(op.Http, existingLock, op.Options),
            WopiFileOperations.Lock or WopiFileOperations.Put => op.OldLockIdentifier is null
                ? await HandleLock(op.Id, op.NewLockIdentifier!, existingLock, op.LockProvider, op.Comparer, op.CancellationToken).ConfigureAwait(false)
                : await HandleUnlockAndRelock(op.Id, op.OldLockIdentifier, op.NewLockIdentifier!, existingLock, op.LockProvider, op.Comparer, op.CancellationToken).ConfigureAwait(false),
            WopiFileOperations.Unlock => await HandleUnlock(op.Id, op.NewLockIdentifier!, existingLock, op.LockProvider, op.Comparer, op.CancellationToken).ConfigureAwait(false),
            WopiFileOperations.RefreshLock => await HandleRefreshLock(op.NewLockIdentifier!, existingLock, op.LockProvider, op.Comparer, op.CancellationToken).ConfigureAwait(false),
            _ => TypedResults.StatusCode(StatusCodes.Status501NotImplemented),
        };

    private static Ok HandleGetLock(HttpContext httpContext, WopiLockInfo? existingLock, IOptions<WopiHostOptions> options)
    {
        httpContext.Response.Headers[WopiHeaders.Lock] = existingLock is not null
            ? existingLock.LockId
            : options.Value.EmptyLockHeaderValue;
        return TypedResults.Ok();
    }

    // newLockIdentifier is non-null/non-whitespace by the time these handlers run — ProcessLockCore
    // guards 400 BadRequest for missing/empty/whitespace X-WOPI-Lock before the dispatch switch.
    // Sub-helpers declare the same wide Results<> shape as ProcessLockCore so the dispatch switch
    // arms compose without per-call .Result destructuring — in practice each helper realises only
    // a subset (Ok or WopiLockMismatchResult), and the declared width never widens the wire response.
    private static async Task<ProcessLockResult> HandleLock(string id, string newLockIdentifier, WopiLockInfo? existingLock, IWopiLockProvider lockProvider, IWopiLockComparer comparer, CancellationToken ct)
    {
        if (existingLock is not null)
        {
            // Uses the runtime comparer threaded through ProcessLockCore. Hardcoding ordinal here
            // would make hosts that registered a JSON-shaped comparer (to absorb OOS-style
            // lock-id mutations) silently mismatch locks acquired via Lock-on-existing-lock even
            // when the configured comparer would consider them equal.
            return await LockOrRefresh(newLockIdentifier, existingLock, lockProvider, comparer, ct).ConfigureAwait(false);
        }
        return await lockProvider.AddLockAsync(id, newLockIdentifier, ct).ConfigureAwait(false) is not null
            ? TypedResults.Ok()
            : new WopiLockMismatchResult(reason: "Could not create lock");
    }

    private static async Task<ProcessLockResult> HandleUnlockAndRelock(string id, string oldLockIdentifier, string newLockIdentifier, WopiLockInfo? existingLock, IWopiLockProvider lockProvider, IWopiLockComparer comparer, CancellationToken ct)
    {
        if (existingLock is null) return new WopiLockMismatchResult(reason: "File not locked");
        if (!comparer.AreEqual(existingLock.LockId, oldLockIdentifier)) return new WopiLockMismatchResult(existingLock.LockId);
        // Pass the expected old lock id down so the provider does an atomic compare-and-swap;
        // the controller-level check above is necessary but not sufficient under concurrency.
        return await lockProvider.TryUnlockAndRelockAsync(id, newLockIdentifier, oldLockIdentifier, ct).ConfigureAwait(false)
            ? TypedResults.Ok()
            : new WopiLockMismatchResult(existingLock.LockId, reason: "Lock changed concurrently");
    }

    private static async Task<ProcessLockResult> HandleUnlock(string id, string newLockIdentifier, WopiLockInfo? existingLock, IWopiLockProvider lockProvider, IWopiLockComparer comparer, CancellationToken ct)
    {
        if (existingLock is null) return new WopiLockMismatchResult(reason: "File not locked");
        if (!comparer.AreEqual(existingLock.LockId, newLockIdentifier)) return new WopiLockMismatchResult(existingLock.LockId);
        return await lockProvider.RemoveLockAsync(id, ct).ConfigureAwait(false)
            ? TypedResults.Ok()
            : new WopiLockMismatchResult(reason: "Could not remove lock");
    }

    private static async Task<ProcessLockResult> HandleRefreshLock(string newLockIdentifier, WopiLockInfo? existingLock, IWopiLockProvider lockProvider, IWopiLockComparer comparer, CancellationToken ct)
    {
        if (existingLock is null) return new WopiLockMismatchResult(reason: "File not locked");
        return await LockOrRefresh(newLockIdentifier, existingLock, lockProvider, comparer, ct).ConfigureAwait(false);
    }

    private static async Task<ProcessLockResult> LockOrRefresh(string newLock, WopiLockInfo existingLock, IWopiLockProvider lockProvider, IWopiLockComparer comparer, CancellationToken ct)
    {
        if (!comparer.AreEqual(existingLock.LockId, newLock))
        {
            return new WopiLockMismatchResult(existingLock.LockId);
        }
        // Atomic compare-and-refresh: provider receives expected lock id; if a concurrent
        // UnlockAndRelock swapped the stored id between the snapshot and this call, the refresh
        // aborts cleanly (avoids a check-then-act race).
        return await lockProvider.RefreshLockAsync(existingLock.FileId, newLock, ct).ConfigureAwait(false)
            ? TypedResults.Ok()
            : new WopiLockMismatchResult(reason: "Could not refresh lock");
    }

    private static StatusCodeHttpResult? CheckMaxFileSize(HttpContext httpContext, long? max, long? declaredSize = null)
    {
        if (max is null) return null;
        var contentLength = httpContext.Request.ContentLength;
        if ((contentLength is long len && len > max.Value)
            || (declaredSize is long ds && ds > max.Value))
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }
        return null;
    }

    // Only the PutRelativeFile handler calls this, and it has already verified
    // IWopiWritableStorageProvider is non-null (ArgumentNullException.ThrowIfNull at the top of
    // the method). SupportsUpdate is therefore unconditionally true here — for the unauthenticated /
    // missing-writable-storage cases, the read-only CheckFileInfo endpoint computes the capability
    // dynamically instead. SupportsCoauth tracks ICobaltProcessor registration since the Cobalt
    // protocol is what actually delivers multi-user editing in WopiHost (see WopiHost.Cobalt).
    private static WopiHostCapabilities MakeCapabilities(IWopiLockProvider? lockProvider, ICobaltProcessor? cobaltProcessor) => new()
    {
        SupportsCobalt = cobaltProcessor is not null,
        SupportsGetLock = lockProvider is not null,
        SupportsLocks = lockProvider is not null,
        SupportsCoauth = cobaltProcessor is not null,
        SupportsUpdate = true,
    };

    private static async Task InvokePutFileCallbackAsync(HttpContext httpContext, IWopiHostExtensions extensions, IWopiFile file, string? editorsHeader, CancellationToken ct)
    {
        var editors = ParseEditorsHeader(editorsHeader);
        await extensions.OnPutFileAsync(new WopiPutFileContext(httpContext.User, file, editors), ct).ConfigureAwait(false);
    }

    private static async Task InvokePutRelativeFileCallbackAsync(HttpContext httpContext, IWopiHostExtensions extensions, IWopiFile original, IWopiFile newFile, string? fileConversion, long? declaredSize, CancellationToken ct)
    {
        // Per spec, X-WOPI-FileConversion is presence-only; treat any bound value (including
        // empty string) as "header was present."
        var isConversion = fileConversion is not null
            || httpContext.Request.Headers.ContainsKey(WopiHeaders.FileConversion);
        await extensions.OnPutRelativeFileAsync(
            new WopiPutRelativeFileContext(httpContext.User, original, newFile, isConversion, declaredSize),
            ct).ConfigureAwait(false);
    }

    private static ReadOnlyCollection<string> ParseEditorsHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return new ReadOnlyCollection<string>([]);
        var parts = header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new ReadOnlyCollection<string>(parts);
    }
}

/// <summary>Response payload for <c>RenameFile</c> — single required <c>Name</c> property per spec.</summary>
public sealed record RenameFileResponse(string Name);

// [FromServices] on the nullable lock/cobalt/writable parameters in the records below is
// load-bearing: these services are optional and may not be registered in every host
// configuration (UseCobalt=false skips AddCobalt(); not every storage provider package
// registers IWopiWritableStorageProvider). Without [FromServices] the Minimal-API binder
// doesn't see the type in DI and falls back to body inference at startup, hard-erroring
// before any request is served.
//
// IWopiLockComparer is NOT in that bucket — AddWopi() unconditionally TryAddSingleton's an
// OrdinalWopiLockComparer (hosts override with JsonShapedWopiLockComparer if needed). The
// service is always present in the container by the time MapWopiEndpoints runs, so the
// parameter is plain (non-nullable, no [FromServices]). Declaring it nullable with a
// null-coalesce fallback at the call sites would silently mask a host's choice to register
// JsonShapedWopiLockComparer, routing every lock-compare through Ordinal.

/// <summary>Parameter bundle for <see cref="FileMutatingEndpoints.PutFile"/>.</summary>
internal readonly record struct PutFileRequest(
    [FromRoute] string Id,
    HttpContext Http,
    [FromServices] IWopiWritableStorageProvider? WritableStorage,
    IWopiHostExtensions Extensions,
    IOptions<WopiHostOptions> Options,
    [FromServices] IWopiLockProvider? LockProvider,
    IWopiLockComparer LockComparer,
    [FromHeader(Name = WopiHeaders.Lock)] string? RequestLockId,
    [FromHeader(Name = WopiHeaders.Editors)] string? Editors,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for <see cref="FileMutatingEndpoints.RenameFile"/>.</summary>
internal readonly record struct RenameFileRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    [FromServices] IWopiWritableStorageProvider? WritableStorage,
    [FromServices] IWopiLockProvider? LockProvider,
    IWopiLockComparer LockComparer,
    [FromHeader(Name = WopiHeaders.RequestedName)] UtfString RequestedName,
    [FromHeader(Name = WopiHeaders.Lock)] string? LockIdentifier,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for <see cref="FileMutatingEndpoints.PutRelativeFile"/>.</summary>
internal readonly record struct PutRelativeFileRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    [FromServices] IWopiWritableStorageProvider? WritableStorage,
    IWopiNewChildFileNegotiator Negotiator,
    IWopiHostExtensions Extensions,
    ICheckFileInfoBuilder CheckFileInfoBuilder,
    IOptions<WopiHostOptions> Options,
    IWopiResourceTokenMinter TokenMinter,
    [FromServices] IWopiLockProvider? LockProvider,
    [FromServices] ICobaltProcessor? CobaltProcessor,
    [FromHeader(Name = WopiHeaders.SuggestedTarget)] UtfString? SuggestedTarget,
    [FromHeader(Name = WopiHeaders.RelativeTarget)] UtfString? RelativeTarget,
    [FromHeader(Name = WopiHeaders.OverwriteRelativeTarget)] bool? OverwriteRelativeTarget,
    [FromHeader(Name = WopiHeaders.FileConversion)] string? FileConversion,
    [FromHeader(Name = WopiHeaders.Size)] long? DeclaredSize,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for <see cref="FileMutatingEndpoints.PutUserInfo"/>.</summary>
internal readonly record struct PutUserInfoRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    IMemoryCache MemoryCache,
    IOptions<WopiHostOptions> Options,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for <see cref="FileMutatingEndpoints.DeleteFile"/>.</summary>
internal readonly record struct DeleteFileRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    [FromServices] IWopiWritableStorageProvider? WritableStorage,
    [FromServices] IWopiLockProvider? LockProvider,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for <see cref="FileMutatingEndpoints.ProcessCobalt"/>.</summary>
internal readonly record struct ProcessCobaltRequest(
    [FromRoute] string Id,
    HttpContext Http,
    [FromServices] IWopiWritableStorageProvider? WritableStorage,
    [FromServices] ICobaltProcessor? CobaltProcessor,
    [FromHeader(Name = WopiHeaders.CorrelationId)] string? CorrelationId,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for <see cref="FileMutatingEndpoints.ProcessLock"/>.</summary>
internal readonly record struct ProcessLockRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    IOptions<WopiHostOptions> Options,
    [FromServices] IWopiLockProvider? LockProvider,
    IWopiLockComparer LockComparer,
    [FromHeader(Name = WopiHeaders.WopiOverride)] string? WopiOverrideHeader,
    [FromHeader(Name = WopiHeaders.OldLock)] string? OldLockIdentifier,
    [FromHeader(Name = WopiHeaders.Lock)] string? NewLockIdentifier,
    CancellationToken CancellationToken);

/// <summary>
/// Validated lock-operation context handed to the dispatch switch once
/// <see cref="FileMutatingEndpoints.ProcessLockCore"/> has confirmed the lock provider is present
/// and the headers pass validation. Distinct from <see cref="ProcessLockRequest"/>: it carries no
/// storage handle and its <see cref="LockProvider"/> is non-null.
/// </summary>
internal readonly record struct LockOperationRequest(
    string Id,
    HttpContext Http,
    IWopiLockProvider LockProvider,
    IOptions<WopiHostOptions> Options,
    IWopiLockComparer Comparer,
    string? WopiOverrideHeader,
    string? OldLockIdentifier,
    string? NewLockIdentifier,
    CancellationToken CancellationToken);
