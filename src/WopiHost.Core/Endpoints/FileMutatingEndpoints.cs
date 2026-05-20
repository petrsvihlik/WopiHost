using System.Collections.ObjectModel;
using System.Net.Mime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Results;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// Minimal-API mutating endpoints for the <c>/wopi/files/{id}</c> surface. Mirrors the
/// non-GET actions of <c>FilesController</c>, dispatched via the <c>X-WOPI-Override</c>
/// header (<see cref="WopiOverrideMatcherPolicy"/>) where applicable.
/// </summary>
internal static class FileMutatingEndpoints
{
    private const string UserInfoCacheKeyPrefix = "UserInfo-";

    public static void MapFileMutatingEndpoints(RouteGroupBuilder files)
    {
        // PutFile: PUT and POST on /{id}/contents — no override header involved.
        files.MapMethods("/{id}/contents", ["PUT", "POST"], PutFile)
            .AddEndpointFilter<RequiresWritableStorageEndpointFilter>()
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Update)));

        // All POST overloads on /{id} share a route + verb and are discriminated by
        // X-WOPI-Override via WopiOverrideMatcherPolicy. Each is its own endpoint with its
        // own RequireAuthorization — the security property the migration depends on
        // (see #430 §2 in the plan: per-override permissions stay distinct).
        files.MapPost("/{id}", RenameFile)
            .WithMetadata(new WopiOverrideMetadata(WopiFileOperations.RenameFile))
            .AddEndpointFilter<RequiresWritableStorageEndpointFilter>()
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Rename)));

        files.MapPost("/{id}", PutRelativeFile)
            .WithMetadata(new WopiOverrideMetadata(WopiFileOperations.PutRelativeFile))
            .AddEndpointFilter<RequiresWritableStorageEndpointFilter>()
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Create)));

        files.MapPost("/{id}", PutUserInfo)
            .WithMetadata(new WopiOverrideMetadata(WopiFileOperations.PutUserInfo));

        files.MapPost("/{id}", DeleteFile)
            .WithMetadata(new WopiOverrideMetadata(WopiFileOperations.DeleteFile))
            .AddEndpointFilter<RequiresWritableStorageEndpointFilter>()
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Delete)));

        files.MapPost("/{id}", ProcessCobalt)
            .WithMetadata(new WopiOverrideMetadata(WopiFileOperations.Cobalt))
            .AddEndpointFilter<RequiresWritableStorageEndpointFilter>()
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Update)));

        // ProcessLock: multiplexed across 5 override values, kept on one endpoint because the
        // WOPI spec treats them as a single state machine and per-spec all five share the
        // Update permission. Sub-dispatch happens inside the handler.
        files.MapPost("/{id}", ProcessLock)
            .WithMetadata(new WopiOverrideMetadata(
                WopiFileOperations.Lock,
                WopiFileOperations.Put,
                WopiFileOperations.Unlock,
                WopiFileOperations.RefreshLock,
                WopiFileOperations.GetLock))
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Update)));
    }

    private static async Task<IResult> PutFile(
        string id,
        HttpContext httpContext,
        [FromServices] IWopiWritableStorageProvider? writableStorage,
        IWopiHostExtensions extensions,
        IOptions<WopiHostOptions> options,
        [FromServices] IWopiLockProvider? lockProvider,
        [FromServices] IWopiLockComparer? lockComparer,
        [FromHeader(Name = WopiHeaders.LOCK)] string? newLockIdentifier,
        [FromHeader(Name = WopiHeaders.EDITORS)] string? editors,
        CancellationToken cancellationToken)
    {
        // RequiresWritableStorageEndpointFilter already short-circuited with 501 if
        // WritableStorage was missing — the assert is defensive against pipeline misorder.
        ArgumentNullException.ThrowIfNull(writableStorage);
        var file = await writableStorage.GetWritableFile(id, cancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();
        if (CheckMaxFileSize(httpContext, options.Value.MaxFileSize) is { } tooLarge) return tooLarge;

        // Unlocked: spec permits PutFile on a 0-byte unlocked file as a "create-new" workaround.
        if (string.IsNullOrEmpty(newLockIdentifier))
        {
            return file.Length != 0
                ? new WopiLockMismatchResult(existingLock: null)
                : await WriteAndAck(httpContext, extensions, file, editors, cancellationToken).ConfigureAwait(false);
        }

        // Locked: acquire/refresh the lock via the shared ProcessLockCore helper, only write on Ok.
        var lockResult = await ProcessLockCore(
            id, httpContext, lockProvider, options, lockComparer ?? OrdinalWopiLockComparer.Instance,
            wopiOverrideHeader: WopiFileOperations.Lock, oldLockIdentifier: null, newLockIdentifier: newLockIdentifier,
            cancellationToken).ConfigureAwait(false);
        return lockResult is Microsoft.AspNetCore.Http.HttpResults.Ok
            ? await WriteAndAck(httpContext, extensions, file, editors, cancellationToken).ConfigureAwait(false)
            : lockResult;
    }

    private static async Task<IResult> WriteAndAck(HttpContext httpContext, IWopiHostExtensions extensions, IWopiWritableFile file, string? editors, CancellationToken cancellationToken)
    {
        await httpContext.CopyToWriteStream(file, cancellationToken).ConfigureAwait(false);
        if (file.Version is not null)
        {
            httpContext.Response.Headers[WopiHeaders.ITEM_VERSION] = file.Version;
        }
        await InvokePutFileCallbackAsync(httpContext, extensions, file, editors, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok();
    }

    private static async Task<IResult> RenameFile(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storage,
        [FromServices] IWopiWritableStorageProvider? writableStorage,
        [FromServices] IWopiLockProvider? lockProvider,
        [FromServices] IWopiLockComparer? lockComparer,
        [FromHeader(Name = WopiHeaders.REQUESTED_NAME)] UtfString requestedName,
        [FromHeader(Name = WopiHeaders.LOCK)] string? lockIdentifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writableStorage);
        var file = await storage.GetWopiFile(id, cancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        var comparer = lockComparer ?? OrdinalWopiLockComparer.Instance;
        if (lockProvider is not null
            && await lockProvider.GetLockAsync(id, cancellationToken).ConfigureAwait(false) is { } existingLock
            && !comparer.AreEqual(existingLock.LockId, lockIdentifier))
        {
            return new WopiLockMismatchResult(existingLock.LockId);
        }

        if (!await writableStorage.CheckValidFileName(requestedName, cancellationToken).ConfigureAwait(false))
        {
            httpContext.Response.Headers[WopiHeaders.INVALID_FILE_NAME] = "Specified name is illegal";
            return TypedResults.BadRequest();
        }
        return await TryRenameFileAsync(httpContext, writableStorage, id, requestedName, file, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> TryRenameFileAsync(HttpContext httpContext, IWopiWritableStorageProvider writableStorage, string id, string requestedName, IWopiFile file, CancellationToken cancellationToken)
    {
        try
        {
            var newName = await writableStorage.GetSuggestedFileName(id, requestedName + '.' + file.Extension, cancellationToken).ConfigureAwait(false);
            return await writableStorage.RenameWopiFile(id, newName, cancellationToken).ConfigureAwait(false)
                ? TypedResults.Json(new { Name = Path.GetFileNameWithoutExtension(newName) })
                : TypedResults.NotFound(); // false → missing resource (race with concurrent delete).
        }
        catch (ArgumentException ae) when (ae.ParamName == nameof(requestedName))
        {
            httpContext.Response.Headers[WopiHeaders.INVALID_FILE_NAME] = "Specified name is illegal";
            return TypedResults.BadRequest();
        }
        catch (FileNotFoundException) { return TypedResults.NotFound(); }
        catch (InvalidOperationException) { return TypedResults.Conflict(); }
    }

    private static async Task<IResult> PutRelativeFile(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storage,
        [FromServices] IWopiWritableStorageProvider? writableStorage,
        IWopiNewChildFileNegotiator negotiator,
        IWopiHostExtensions extensions,
        ICheckFileInfoBuilder checkFileInfoBuilder,
        IOptions<WopiHostOptions> options,
        [FromServices] IWopiLockProvider? lockProvider,
        [FromServices] ICobaltProcessor? cobaltProcessor,
        [FromHeader(Name = WopiHeaders.SUGGESTED_TARGET)] UtfString? suggestedTarget,
        [FromHeader(Name = WopiHeaders.RELATIVE_TARGET)] UtfString? relativeTarget,
        [FromHeader(Name = WopiHeaders.OVERWRITE_RELATIVE_TARGET)] bool? overwriteRelativeTarget,
        [FromHeader(Name = WopiHeaders.FILE_CONVERSION)] string? fileConversion,
        [FromHeader(Name = WopiHeaders.SIZE)] long? declaredSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writableStorage);
        var file = await storage.GetWopiFile(id, cancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        if (CheckMaxFileSize(httpContext, options.Value.MaxFileSize, declaredSize) is { } tooLarge)
        {
            return tooLarge;
        }

        // Mutually exclusive headers per spec: 501 when both present or both missing.
        if ((!string.IsNullOrWhiteSpace(suggestedTarget) && !string.IsNullOrWhiteSpace(relativeTarget))
            || (string.IsNullOrWhiteSpace(suggestedTarget) && string.IsNullOrWhiteSpace(relativeTarget)))
        {
            return TypedResults.StatusCode(StatusCodes.Status501NotImplemented);
        }

        var ancestors = await storage.GetFileAncestors(id, cancellationToken).ConfigureAwait(false);
        var parentContainer = ancestors.LastOrDefault()
            ?? throw new ArgumentException("Cannot find parent container", nameof(id));

        var negotiation = await negotiator.NegotiateAsync(new WopiNewChildFileRequest(
            ContainerId: parentContainer.Identifier,
            SuggestedTarget: suggestedTarget,
            RelativeTarget: relativeTarget,
            OverwriteRelativeTarget: overwriteRelativeTarget ?? false,
            SuggestedExtensionFallbackStem: file.Name), cancellationToken).ConfigureAwait(false);
        if (negotiation.ToErrorResult(httpContext.Response) is { } error)
        {
            return error;
        }

        var newFile = negotiation.File!;
        await httpContext.CopyToWriteStream(newFile, cancellationToken).ConfigureAwait(false);
        await InvokePutRelativeFileCallbackAsync(httpContext, extensions, file, newFile, fileConversion, declaredSize, cancellationToken).ConfigureAwait(false);
        var capabilities = MakeCapabilities(lockProvider, cobaltProcessor);
        var checkFileInfo = await checkFileInfoBuilder.BuildAsync(newFile, httpContext, capabilities, cancellationToken: cancellationToken).ConfigureAwait(false);
        return TypedResults.Json(new ChildFile(newFile.Name + '.' + newFile.Extension, httpContext.GetWopiSrc(newFile))
        {
            HostEditUrl = checkFileInfo.HostEditUrl,
            HostViewUrl = checkFileInfo.HostViewUrl,
        });
    }

    private static async Task<IResult> PutUserInfo(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storageProvider,
        IMemoryCache memoryCache,
        IOptions<WopiHostOptions> options,
        CancellationToken cancellationToken)
    {
        var file = await storageProvider.GetWopiFile(id, cancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        // Replaces the [FromStringBody] model-binder path the MVC controller used. Manual body
        // read keeps the Minimal-API binding source explicit.
        string userInfo;
        using (var reader = new StreamReader(httpContext.Request.Body))
        {
            userInfo = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        memoryCache.Set(
            $"{UserInfoCacheKeyPrefix}{httpContext.User.GetUserId()}",
            userInfo,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = options.Value.UserInfoCacheLifetime,
            });
        return TypedResults.Ok();
    }

    private static async Task<IResult> DeleteFile(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storageProvider,
        [FromServices] IWopiWritableStorageProvider? writableStorageProvider,
        [FromServices] IWopiLockProvider? lockProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writableStorageProvider);
        var file = await storageProvider.GetWopiFile(id, cancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        if (lockProvider is not null)
        {
            var existingLock = await lockProvider.GetLockAsync(id, cancellationToken).ConfigureAwait(false);
            if (existingLock is not null)
            {
                return new WopiLockMismatchResult(existingLock.LockId);
            }
        }

        if (await writableStorageProvider.DeleteWopiFile(id, cancellationToken).ConfigureAwait(false))
        {
            return TypedResults.Ok();
        }
        // false → missing resource (race with concurrent delete).
        return TypedResults.NotFound();
    }

    private static async Task<IResult> ProcessCobalt(
        string id,
        HttpContext httpContext,
        [FromServices] IWopiWritableStorageProvider? writableStorageProvider,
        [FromServices] ICobaltProcessor? cobaltProcessor,
        [FromHeader(Name = WopiHeaders.CORRELATION_ID)] string? correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writableStorageProvider);
        ArgumentNullException.ThrowIfNull(cobaltProcessor);
        var file = await writableStorageProvider.GetWritableFile(id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("File not found");

        var bytes = await httpContext.Request.Body.ReadBytesAsync(cancellationToken).ConfigureAwait(false);
        var responseBytes = await cobaltProcessor.ProcessCobalt(file, httpContext.User, bytes, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(correlationId))
        {
            httpContext.Response.Headers.Append(WopiHeaders.CORRELATION_ID, correlationId);
            httpContext.Response.Headers.Append("request-id", correlationId);
        }
        return TypedResults.Bytes(responseBytes, MediaTypeNames.Application.Octet);
    }

    private static async Task<IResult> ProcessLock(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storage,
        IOptions<WopiHostOptions> options,
        [FromServices] IWopiLockProvider? lockProvider,
        [FromServices] IWopiLockComparer? lockComparer,
        [FromHeader(Name = WopiHeaders.WOPI_OVERRIDE)] string? wopiOverrideHeader,
        [FromHeader(Name = WopiHeaders.OLD_LOCK)] string? oldLockIdentifier,
        [FromHeader(Name = WopiHeaders.LOCK)] string? newLockIdentifier,
        CancellationToken cancellationToken)
    {
        var comparer = lockComparer ?? OrdinalWopiLockComparer.Instance;
        var file = await storage.GetWopiFile(id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("File not found");
        httpContext.Response.Headers[WopiHeaders.ITEM_VERSION] = file.Version;
        return await ProcessLockCore(id, httpContext, lockProvider, options, comparer,
            wopiOverrideHeader, oldLockIdentifier, newLockIdentifier, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> ProcessLockCore(
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

        var existingLock = await lockProvider.GetLockAsync(id, cancellationToken).ConfigureAwait(false);
        // Lock override doubles as UnlockAndRelock when X-WOPI-OldLock is present, so dispatch
        // by header presence rather than baking a third branch into the switch.
        return wopiOverrideHeader switch
        {
            WopiFileOperations.GetLock => HandleGetLock(httpContext, existingLock, options),
            WopiFileOperations.Lock or WopiFileOperations.Put => oldLockIdentifier is null
                ? await HandleLock(id, newLockIdentifier, existingLock, lockProvider, cancellationToken).ConfigureAwait(false)
                : await HandleUnlockAndRelock(id, oldLockIdentifier, newLockIdentifier, existingLock, lockProvider, comparer, cancellationToken).ConfigureAwait(false),
            WopiFileOperations.Unlock => await HandleUnlock(id, newLockIdentifier, existingLock, lockProvider, comparer, cancellationToken).ConfigureAwait(false),
            WopiFileOperations.RefreshLock => await HandleRefreshLock(newLockIdentifier, existingLock, lockProvider, comparer, cancellationToken).ConfigureAwait(false),
            _ => TypedResults.StatusCode(StatusCodes.Status501NotImplemented),
        };
    }

    private static Microsoft.AspNetCore.Http.HttpResults.Ok HandleGetLock(HttpContext httpContext, WopiLockInfo? existingLock, IOptions<WopiHostOptions> options)
    {
        httpContext.Response.Headers[WopiHeaders.LOCK] = existingLock is not null
            ? existingLock.LockId
            : options.Value.EmptyLockHeaderValue;
        return TypedResults.Ok();
    }

    private static async Task<IResult> HandleLock(string id, string? newLockIdentifier, WopiLockInfo? existingLock, IWopiLockProvider lockProvider, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newLockIdentifier))
        {
            return new WopiLockMismatchResult(reason: "Missing new lock identifier");
        }
        if (existingLock is not null)
        {
            return await LockOrRefresh(newLockIdentifier, existingLock, lockProvider, OrdinalWopiLockComparer.Instance, ct).ConfigureAwait(false);
        }
        return await lockProvider.AddLockAsync(id, newLockIdentifier, ct).ConfigureAwait(false) is not null
            ? TypedResults.Ok()
            : new WopiLockMismatchResult(reason: "Could not create lock");
    }

    private static async Task<IResult> HandleUnlockAndRelock(string id, string oldLockIdentifier, string? newLockIdentifier, WopiLockInfo? existingLock, IWopiLockProvider lockProvider, IWopiLockComparer comparer, CancellationToken ct)
    {
        if (existingLock is null) return new WopiLockMismatchResult(reason: "File not locked");
        if (!comparer.AreEqual(existingLock.LockId, oldLockIdentifier)) return new WopiLockMismatchResult(existingLock.LockId);
        if (string.IsNullOrWhiteSpace(newLockIdentifier)) return new WopiLockMismatchResult(reason: "Missing new lock identifier");
        // Pass the expected old lock id down so the provider does an atomic compare-and-swap;
        // the controller-level check above is necessary but not sufficient under concurrency.
        return await lockProvider.TryUnlockAndRelockAsync(id, newLockIdentifier, oldLockIdentifier, ct).ConfigureAwait(false)
            ? TypedResults.Ok()
            : new WopiLockMismatchResult(existingLock.LockId, reason: "Lock changed concurrently");
    }

    private static async Task<IResult> HandleUnlock(string id, string? newLockIdentifier, WopiLockInfo? existingLock, IWopiLockProvider lockProvider, IWopiLockComparer comparer, CancellationToken ct)
    {
        if (existingLock is null) return new WopiLockMismatchResult(reason: "File not locked");
        if (!comparer.AreEqual(existingLock.LockId, newLockIdentifier)) return new WopiLockMismatchResult(existingLock.LockId);
        return await lockProvider.RemoveLockAsync(id, ct).ConfigureAwait(false)
            ? TypedResults.Ok()
            : new WopiLockMismatchResult(reason: "Could not remove lock");
    }

    private static async Task<IResult> HandleRefreshLock(string? newLockIdentifier, WopiLockInfo? existingLock, IWopiLockProvider lockProvider, IWopiLockComparer comparer, CancellationToken ct)
    {
        if (existingLock is null) return new WopiLockMismatchResult(reason: "File not locked");
        if (string.IsNullOrWhiteSpace(newLockIdentifier)) return new WopiLockMismatchResult(reason: "Missing new lock identifier");
        return await LockOrRefresh(newLockIdentifier, existingLock, lockProvider, comparer, ct).ConfigureAwait(false);
    }

    private static async Task<IResult> LockOrRefresh(string newLock, WopiLockInfo existingLock, IWopiLockProvider lockProvider, IWopiLockComparer comparer, CancellationToken ct)
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

    private static Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult? CheckMaxFileSize(HttpContext httpContext, long? max, long? declaredSize = null)
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
    // dynamically instead.
    private static WopiHostCapabilities MakeCapabilities(IWopiLockProvider? lockProvider, ICobaltProcessor? cobaltProcessor) => new()
    {
        SupportsCobalt = cobaltProcessor is not null,
        SupportsGetLock = lockProvider is not null,
        SupportsLocks = lockProvider is not null,
        SupportsCoauth = false,
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
