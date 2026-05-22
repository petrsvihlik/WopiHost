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

// Shared typed-union for ProcessLock / ProcessLockCore and the LOCK / UNLOCK / REFRESH_LOCK /
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
        // RequireAuthorization — the security property the migration depends on (per-override
        // permissions stay distinct).
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
            .WithSummary("Lock state machine (LOCK / UNLOCK / REFRESH_LOCK / GET_LOCK / UNLOCK_AND_RELOCK).")
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

        // PutFile branches on the FILE'S current lock state, not on whether X-WOPI-Lock was
        // sent. Spec: "When a host receives a PutFile request on a file that's not locked, the
        // host checks the current size of the file." Earlier impl keyed off the request header
        // (string.IsNullOrEmpty(requestLockId)) — which silently overwrote locked 0-byte files
        // when the client omitted the header (#A) and acquired locks as a side effect when the
        // client sent one against an unlocked file (#B). Both deviated from
        // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putfile.
        var existingLock = req.LockProvider is not null
            ? await req.LockProvider.GetLockAsync(req.Id, req.CancellationToken).ConfigureAwait(false)
            : null;

        if (existingLock is not null)
        {
            // File is locked → X-WOPI-Lock must match the current lock id. Missing/empty header
            // counts as a mismatch (sent="" vs current=existing). Spec: 409 with the current
            // lock id in X-WOPI-Lock.
            if (string.IsNullOrEmpty(req.RequestLockId) || !req.LockComparer.AreEqual(existingLock.LockId, req.RequestLockId))
            {
                return new WopiLockMismatchResult(existingLock.LockId);
            }
            return await WriteAndAck(req.Http, req.Extensions, file, req.Editors, req.CancellationToken).ConfigureAwait(false);
        }

        // File is unlocked → size decides. 0-byte file is the create-new flow per spec; any
        // other size returns 409 with X-WOPI-Lock set to the empty placeholder regardless of
        // whether the client sent X-WOPI-Lock (an unlocked file has no lock id to surface).
        return file.Length != 0
            ? new WopiLockMismatchResult(existingLock: null)
            : await WriteAndAck(req.Http, req.Extensions, file, req.Editors, req.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<Ok> WriteAndAck(HttpContext httpContext, IWopiHostExtensions extensions, IWopiWritableFile file, string? editors, CancellationToken cancellationToken)
    {
        await httpContext.CopyToWriteStream(file, cancellationToken).ConfigureAwait(false);
        if (file.Version is not null)
        {
            httpContext.Response.Headers[WopiHeaders.ITEM_VERSION] = file.Version;
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
        // here is a long-standing bug that degenerates the provider into echoing the requested
        // name back. Same fix shape as the pre-#420 PutRelativeFile correction; the storage
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
                req.Http.Response.Headers[WopiHeaders.INVALID_FILE_NAME] = "Specified name is illegal";
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
            httpContext.Response.Headers[WopiHeaders.INVALID_FILE_NAME] = "Specified name is illegal";
            return TypedResults.BadRequest();
        }
        catch (FileNotFoundException) { return TypedResults.NotFound(); }
        catch (InvalidOperationException) { return TypedResults.Conflict(); }
    }

    /// <summary>
    /// Replaces filesystem-forbidden characters in <paramref name="invalidName"/>'s stem with
    /// <c>_</c>, preserving the extension. Returns the sanitised name only if it passes
    /// <see cref="IWopiWritableStorageProvider.CheckValidFileName"/>; otherwise tries the
    /// fallback stem; otherwise returns <see langword="null"/>. Mirrors the helper in
    /// <see cref="DefaultWopiNewChildFileNegotiator"/>; kept local rather than extracted because
    /// the two callers' fallback policies differ slightly (RenameFile has no concept of
    /// "suggested target" — the source file's name is the only reasonable fallback).
    /// </summary>
    private static async Task<string?> TryBuildValidFileNameAsync(IWopiWritableStorageProvider writable, string invalidName, string fallbackStem, CancellationToken cancellationToken)
    {
        var dot = invalidName.LastIndexOf('.');
        var ext = dot > 0 ? invalidName[dot..] : string.Empty;
        var stem = dot > 0 ? invalidName[..dot] : invalidName;

        const string forbiddenChars = "<>:\"/\\|?* ";
        var sanitisedStem = forbiddenChars.Aggregate(stem, (cur, c) => cur.Replace(c, '_')).Trim();
        if (string.IsNullOrWhiteSpace(sanitisedStem) || sanitisedStem is "." or "..")
        {
            sanitisedStem = fallbackStem;
        }

        var candidate = sanitisedStem + ext;
        if (await writable.CheckValidFileName(candidate, cancellationToken).ConfigureAwait(false))
        {
            return candidate;
        }

        var fallback = fallbackStem + ext;
        return candidate != fallback && await writable.CheckValidFileName(fallback, cancellationToken).ConfigureAwait(false)
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

        // Mutually exclusive headers per spec: 501 when both present or both missing.
        if ((!string.IsNullOrWhiteSpace(req.SuggestedTarget) && !string.IsNullOrWhiteSpace(req.RelativeTarget))
            || (string.IsNullOrWhiteSpace(req.SuggestedTarget) && string.IsNullOrWhiteSpace(req.RelativeTarget)))
        {
            return TypedResults.StatusCode(StatusCodes.Status501NotImplemented);
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
        var newFileToken = await EndpointHelpers.IssueAccessTokenForFileAsync(
            req.Http, req.AccessTokenService, req.PermissionProvider, newFile, req.CancellationToken).ConfigureAwait(false);
        return TypedResults.Json(new ChildFile(newFile.Name + '.' + newFile.Extension, req.Http.GetWopiSrc(newFile, newFileToken))
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
    /// unbounded body into our MemoryCache.
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

        // Read up to MAX+1 so we can detect bodies that exceed the cap mid-stream (chunked
        // transfer-encoding with no Content-Length, or clients that lie about it). Single
        // backing buffer — ReadAsync writes directly at the running offset, no intermediate
        // chunk array or MemoryStream copy.
        var buffer = new byte[PutUserInfoMaxBytes + 1];
        var total = 0;
        int read;
        while ((read = await req.Http.Request.Body.ReadAsync(buffer.AsMemory(total), req.CancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > PutUserInfoMaxBytes)
            {
                return TypedResults.BadRequest();
            }
        }

        var userInfo = System.Text.Encoding.UTF8.GetString(buffer, 0, total);

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
            req.Http.Response.Headers.Append(WopiHeaders.CORRELATION_ID, req.CorrelationId);
            req.Http.Response.Headers.Append("request-id", req.CorrelationId);
        }
        return TypedResults.Bytes(responseBytes, MediaTypeNames.Application.Octet);
    }

    private static async Task<ProcessLockResult> ProcessLock(
        [AsParameters] ProcessLockRequest req)
    {
        var file = await req.Storage.GetWopiFile(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();
        req.Http.Response.Headers[WopiHeaders.ITEM_VERSION] = file.Version;
        return await ProcessLockCore(req.Id, req.Http, req.LockProvider, req.Options, req.LockComparer,
            req.WopiOverrideHeader, req.OldLockIdentifier, req.NewLockIdentifier, req.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<ProcessLockResult> ProcessLockCore(
        string id,
        HttpContext httpContext,
        IWopiLockProvider? lockProvider,
        IOptions<WopiHostOptions> options,
        IWopiLockComparer comparer,
        string? wopiOverrideHeader,
        string? oldLockIdentifier,
        string? newLockIdentifier,
        CancellationToken cancellationToken)
    {
        if (lockProvider is null)
        {
            return new WopiLockMismatchResult(reason: "Locking is not supported");
        }

        if ((newLockIdentifier is not null && newLockIdentifier.Length > WopiLockInfo.MaxLockIdLength)
            || (oldLockIdentifier is not null && oldLockIdentifier.Length > WopiLockInfo.MaxLockIdLength))
        {
            httpContext.Response.Headers[WopiHeaders.LOCK_FAILURE_REASON] =
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
            httpContext.Response.Headers[WopiHeaders.LOCK_FAILURE_REASON] = "X-WOPI-Lock header is required";
            return TypedResults.BadRequest();
        }

        var existingLock = await lockProvider.GetLockAsync(id, cancellationToken).ConfigureAwait(false);
        // Lock override doubles as UnlockAndRelock when X-WOPI-OldLock is present, so dispatch
        // by header presence rather than baking a third branch into the switch.
        return wopiOverrideHeader switch
        {
            WopiFileOperations.GetLock => HandleGetLock(httpContext, existingLock, options),
            WopiFileOperations.Lock or WopiFileOperations.Put => oldLockIdentifier is null
                ? await HandleLock(id, newLockIdentifier!, existingLock, lockProvider, comparer, cancellationToken).ConfigureAwait(false)
                : await HandleUnlockAndRelock(id, oldLockIdentifier, newLockIdentifier!, existingLock, lockProvider, comparer, cancellationToken).ConfigureAwait(false),
            WopiFileOperations.Unlock => await HandleUnlock(id, newLockIdentifier!, existingLock, lockProvider, comparer, cancellationToken).ConfigureAwait(false),
            WopiFileOperations.RefreshLock => await HandleRefreshLock(newLockIdentifier!, existingLock, lockProvider, comparer, cancellationToken).ConfigureAwait(false),
            _ => TypedResults.StatusCode(StatusCodes.Status501NotImplemented),
        };
    }

    private static Ok HandleGetLock(HttpContext httpContext, WopiLockInfo? existingLock, IOptions<WopiHostOptions> options)
    {
        httpContext.Response.Headers[WopiHeaders.LOCK] = existingLock is not null
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
            // Pre-#456 this hardcoded OrdinalWopiLockComparer.Instance, ignoring the runtime
            // comparer threaded through ProcessLockCore. Hosts that registered a JSON-shaped
            // comparer to absorb OOS-style lock-id mutations would silently fall back to
            // ordinal here — locks acquired via Lock-on-existing-lock would mismatch even
            // when the configured comparer would have considered them equal. Now uses the
            // runtime comparer like every sibling Handle* method.
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
        // UnlockAndRelock swapped the stored id between our snapshot and this call, the refresh
        // aborts cleanly. Pre-fix this was a check-then-act race.
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
            || httpContext.Request.Headers.ContainsKey(WopiHeaders.FILE_CONVERSION);
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
// parameter is plain (non-nullable, no [FromServices]). Pre-#456 the records carried
// IWopiLockComparer? + a null-coalesce fallback at the call sites, which silently masked a
// host's choice to register JsonShapedWopiLockComparer — every lock-compare went through
// Ordinal even when the configured comparer was JSON-aware.

/// <summary>Parameter bundle for <see cref="FileMutatingEndpoints.PutFile"/>.</summary>
internal readonly record struct PutFileRequest(
    [FromRoute] string Id,
    HttpContext Http,
    [FromServices] IWopiWritableStorageProvider? WritableStorage,
    IWopiHostExtensions Extensions,
    IOptions<WopiHostOptions> Options,
    [FromServices] IWopiLockProvider? LockProvider,
    IWopiLockComparer LockComparer,
    [FromHeader(Name = WopiHeaders.LOCK)] string? RequestLockId,
    [FromHeader(Name = WopiHeaders.EDITORS)] string? Editors,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for <see cref="FileMutatingEndpoints.RenameFile"/>.</summary>
internal readonly record struct RenameFileRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    [FromServices] IWopiWritableStorageProvider? WritableStorage,
    [FromServices] IWopiLockProvider? LockProvider,
    IWopiLockComparer LockComparer,
    [FromHeader(Name = WopiHeaders.REQUESTED_NAME)] UtfString RequestedName,
    [FromHeader(Name = WopiHeaders.LOCK)] string? LockIdentifier,
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
    IWopiAccessTokenService AccessTokenService,
    IWopiPermissionProvider PermissionProvider,
    [FromServices] IWopiLockProvider? LockProvider,
    [FromServices] ICobaltProcessor? CobaltProcessor,
    [FromHeader(Name = WopiHeaders.SUGGESTED_TARGET)] UtfString? SuggestedTarget,
    [FromHeader(Name = WopiHeaders.RELATIVE_TARGET)] UtfString? RelativeTarget,
    [FromHeader(Name = WopiHeaders.OVERWRITE_RELATIVE_TARGET)] bool? OverwriteRelativeTarget,
    [FromHeader(Name = WopiHeaders.FILE_CONVERSION)] string? FileConversion,
    [FromHeader(Name = WopiHeaders.SIZE)] long? DeclaredSize,
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
    [FromHeader(Name = WopiHeaders.CORRELATION_ID)] string? CorrelationId,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for <see cref="FileMutatingEndpoints.ProcessLock"/>.</summary>
internal readonly record struct ProcessLockRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    IOptions<WopiHostOptions> Options,
    [FromServices] IWopiLockProvider? LockProvider,
    IWopiLockComparer LockComparer,
    [FromHeader(Name = WopiHeaders.WOPI_OVERRIDE)] string? WopiOverrideHeader,
    [FromHeader(Name = WopiHeaders.OLD_LOCK)] string? OldLockIdentifier,
    [FromHeader(Name = WopiHeaders.LOCK)] string? NewLockIdentifier,
    CancellationToken CancellationToken);
