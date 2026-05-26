using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Results;

// Shared typed-union for CreateChildContainer — branches: 404 (missing parent), 200 with
// CreateChildContainerResponse, 501 (mutex headers), 400 (illegal specific-mode name), 409
// (specific-mode name conflict), 500 (internal error path).
using CreateChildContainerResult = Microsoft.AspNetCore.Http.HttpResults.Results<
    Microsoft.AspNetCore.Http.HttpResults.NotFound,
    Microsoft.AspNetCore.Http.HttpResults.JsonHttpResult<WopiHost.Core.Models.CreateChildContainerResponse>,
    Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult,
    Microsoft.AspNetCore.Http.HttpResults.BadRequest,
    Microsoft.AspNetCore.Http.HttpResults.Conflict>;

// Shared typed-union for CreateChildFile. Mirrors PutRelativeFile minus the FileExtension /
// declared-size paths: 404, 200 ChildFile, 501 (mutex headers), 400/409 from negotiation,
// lock-mismatch when target exists locked, 500 internal-error.
using CreateChildFileResult = Microsoft.AspNetCore.Http.HttpResults.Results<
    Microsoft.AspNetCore.Http.HttpResults.NotFound,
    Microsoft.AspNetCore.Http.HttpResults.JsonHttpResult<WopiHost.Core.Models.ChildFile>,
    Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult,
    Microsoft.AspNetCore.Http.HttpResults.BadRequest,
    Microsoft.AspNetCore.Http.HttpResults.Conflict,
    WopiHost.Core.Results.WopiLockMismatchResult>;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// Mutating Minimal-API endpoints for the <c>/wopi/containers/{id}</c> surface. POST overloads
/// share the route and verb and are discriminated by <c>X-WOPI-Override</c> via
/// <see cref="WopiOverrideMatcherPolicy"/>.
/// </summary>
internal static class ContainerMutatingEndpoints
{
    public static void MapContainerMutatingEndpoints(RouteGroupBuilder containers)
    {
        // The writable-storage gate applies to every mutating container endpoint. Hoist onto a
        // sub-group so each MapPost doesn't have to repeat .AddEndpointFilter.
        var mutating = containers.MapGroup("")
            .AddEndpointFilter<RequiresWritableStorageEndpointFilter>();

        mutating.MapPost("/{id}", CreateChildContainer)
            .WithMetadata(new WopiOverrideMetadata(WopiContainerOperations.CreateChildContainer))
            .WithSummary("CreateChildContainer (X-WOPI-Override: CREATE_CHILD_CONTAINER).")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/createchildcontainer. " +
                "Suggested-mode silently sanitises invalid names; specific-mode returns 400 with X-WOPI-InvalidContainerNameError on conflict.")
            .RequireWopiPermission(WopiResourceType.Container, Permission.Create);

        mutating.MapPost("/{id}", CreateChildFile)
            .WithMetadata(new WopiOverrideMetadata(WopiContainerOperations.CreateChildFile))
            .WithSummary("CreateChildFile (X-WOPI-Override: CREATE_CHILD_FILE).")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/createchildfile. " +
                "Same suggested/relative target negotiation as PutRelativeFile.")
            .RequireWopiPermission(WopiResourceType.Container, Permission.CreateChildFile);

        mutating.MapPost("/{id}", DeleteContainer)
            .WithMetadata(new WopiOverrideMetadata(WopiContainerOperations.DeleteContainer))
            .WithSummary("DeleteContainer (X-WOPI-Override: DELETE_CONTAINER).")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/deletecontainer. " +
                "Returns 409 when the container has child files / child containers.")
            .RequireWopiPermission(WopiResourceType.Container, Permission.Delete);

        mutating.MapPost("/{id}", RenameContainer)
            .WithMetadata(new WopiOverrideMetadata(WopiContainerOperations.RenameContainer))
            .WithSummary("RenameContainer (X-WOPI-Override: RENAME_CONTAINER).")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/renamecontainer. " +
                "Returns the new name on success; 400 with X-WOPI-InvalidContainerNameError on invalid input.")
            .RequireWopiPermission(WopiResourceType.Container, Permission.Rename);
    }

    private static async Task<CreateChildContainerResult> CreateChildContainer(
        [AsParameters] CreateChildContainerRequest req)
    {
        ArgumentNullException.ThrowIfNull(req.WritableStorage);
        if (await req.Storage.GetWopiContainer(req.Id, req.CancellationToken).ConfigureAwait(false) is null) return TypedResults.NotFound();

        if (EndpointHelpers.EnsureExactlyOneOf(req.SuggestedTarget, req.RelativeTarget) is { } mutex)
        {
            return mutex;
        }

        // Spec branches on which header is set:
        //  - Specific mode (X-WOPI-RelativeTarget): invalid name → 400 with
        //    X-WOPI-InvalidContainerNameError describing why.
        //  - Suggested mode (X-WOPI-SuggestedTarget): "must never result in a 400 Bad Request or
        //    409 Conflict. Rather, the host must modify the proposed name as needed to create a
        //    new container that is legally named." → sanitise on invalid input.
        string requestedName = (req.RelativeTarget ?? req.SuggestedTarget)!;
        if (!await req.WritableStorage.CheckValidContainerName(requestedName, req.CancellationToken).ConfigureAwait(false))
        {
            if (req.RelativeTarget is not null)
            {
                req.Http.Response.Headers[WopiHeaders.INVALID_CONTAINER_NAME] = "Specified name is illegal";
                return TypedResults.BadRequest();
            }
            // Suggested mode: sanitise. Fall back to a GUID stem if even sanitisation fails.
            requestedName = SanitiseContainerName(requestedName);
            if (!await req.WritableStorage.CheckValidContainerName(requestedName, req.CancellationToken).ConfigureAwait(false))
            {
                requestedName = Guid.NewGuid().ToString("N");
            }
        }

        // Single exit for the resolve/build/respond tail — keeps the handler under qlty's
        // return-statements threshold (specific-mode conflict, provider null, and success
        // share one return via the switch). The success-arm body is in a local async
        // function so the switch arm itself stays a single `await` expression, and the
        // inner awaits land directly on injected dependencies (which Infer# tracks
        // cleanly — see #471 history).
        var resolved = await ResolveNewChildContainer(req.Http, req.Storage, req.WritableStorage, req.Id, requestedName, isSpecificMode: req.RelativeTarget is not null, req.CancellationToken).ConfigureAwait(false);
        return resolved switch
        {
            (null, { } conflict) => conflict,
            (null, _) => TypedResults.StatusCode(StatusCodes.Status500InternalServerError),
            ({ } folder, _) => await BuildSuccessAsync(folder).ConfigureAwait(false),
        };

        async Task<JsonHttpResult<CreateChildContainerResponse>> BuildSuccessAsync(IWopiContainer folder)
        {
            var info = await req.ContainerInfoBuilder.BuildAsync(folder, req.Http.User, req.CancellationToken).ConfigureAwait(false);
            // Mint a fresh container-scoped token for the new child container — see
            // IWopiResourceTokenMinter for the token-trading prevention rationale.
            var childToken = await req.TokenMinter.MintForContainerAsync(req.Http.User, folder, req.CancellationToken).ConfigureAwait(false);
            return TypedResults.Json(new CreateChildContainerResponse(new(folder.Name, req.Http.GetWopiSrc(folder, childToken.Token)), info));
        }
    }

    /// <summary>
    /// Dispatches the relative-target ("specific mode") and suggested-target name-negotiation
    /// branches. Returns <c>(folder, null)</c> on success or <c>(null, conflict)</c> when the
    /// specific-mode name collides — the conflict result writes <c>X-WOPI-ValidRelativeTarget</c>.
    /// </summary>
    private static async Task<(IWopiContainer? folder, Conflict? conflict)> ResolveNewChildContainer(
        HttpContext httpContext, IWopiStorageProvider storage, IWopiWritableStorageProvider writableStorage, string id, string requestedName, bool isSpecificMode, CancellationToken cancellationToken)
    {
        if (isSpecificMode)
        {
            // "specific mode" — host must not modify the name.
            var existing = await storage.GetWopiContainerByName(id, requestedName, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                var suggestedName = await writableStorage.GetSuggestedContainerName(id, requestedName, cancellationToken).ConfigureAwait(false);
                httpContext.Response.Headers[WopiHeaders.VALID_RELATIVE_TARGET] = UtfString.FromDecoded(suggestedName).ToString(true);
                return (null, TypedResults.Conflict());
            }
            return (await writableStorage.CreateWopiChildContainer(id, requestedName, cancellationToken).ConfigureAwait(false), null);
        }
        // suggested-target branch — host may dedupe the name.
        var newName = await writableStorage.GetSuggestedContainerName(id, requestedName, cancellationToken).ConfigureAwait(false);
        return (await writableStorage.CreateWopiChildContainer(id, newName, cancellationToken).ConfigureAwait(false), null);
    }

    /// <summary>
    /// Replaces forbidden filesystem characters in a container name with <c>_</c>. Container
    /// names have no concept of extension so no extension-preservation logic is needed (unlike
    /// the file-name sanitiser in <see cref="FileMutatingEndpoints"/>).
    /// </summary>
    private static string SanitiseContainerName(string invalid)
    {
        const string forbiddenChars = "<>:\"/\\|?* ";
        var sanitised = forbiddenChars.Aggregate(invalid, (cur, c) => cur.Replace(c, '_')).Trim();
        return string.IsNullOrWhiteSpace(sanitised) || sanitised is "." or ".." ? Guid.NewGuid().ToString("N") : sanitised;
    }

    private static async Task<CreateChildFileResult> CreateChildFile(
        [AsParameters] CreateChildFileRequest req)
    {
        ArgumentNullException.ThrowIfNull(req.WritableStorage);
        var container = await req.Storage.GetWopiContainer(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (container is null) return TypedResults.NotFound();

        if (EndpointHelpers.EnsureExactlyOneOf(req.SuggestedTarget, req.RelativeTarget) is { } mutex)
        {
            return mutex;
        }

        // Suggested-target / relative-target negotiation — protocol shared with PutRelativeFile.
        // CreateChildFile has no source file in scope, so the extension-only fallback uses a
        // fresh GUID as the stem.
        var negotiation = await req.Negotiator.NegotiateAsync(new WopiNewChildFileRequest(
            ContainerId: container.Identifier,
            SuggestedTarget: req.SuggestedTarget,
            RelativeTarget: req.RelativeTarget,
            OverwriteRelativeTarget: req.OverwriteRelativeTarget ?? false,
            SuggestedExtensionFallbackStem: Guid.NewGuid().ToString("N"),
            User: req.Http.User), req.CancellationToken).ConfigureAwait(false);
        if (negotiation.ToErrorResult(req.Http.Response) is { } error)
        {
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
        var checkFileInfo = await req.CheckFileInfoBuilder.BuildAsync(newFile, req.Http.ToWopiRequestInfo(), cancellationToken: req.CancellationToken).ConfigureAwait(false);
        // Fresh, resource-scoped token for the new file. The inbound token is bound to the parent
        // CONTAINER's id and would fail authorization on the new file's CheckFileInfo callback.
        // See IWopiResourceTokenMinter for the token-trading prevention rationale.
        var newFileToken = await req.TokenMinter.MintForFileAsync(req.Http.User, newFile, req.CancellationToken).ConfigureAwait(false);
        return TypedResults.Json(new ChildFile(newFile.Name + '.' + newFile.Extension, req.Http.GetWopiSrc(newFile, newFileToken.Token))
        {
            HostEditUrl = checkFileInfo.HostEditUrl,
            HostViewUrl = checkFileInfo.HostViewUrl,
        });
    }

    private static async Task<Results<NotFound, Ok, Conflict>> DeleteContainer(
        [AsParameters] DeleteContainerRequest req)
    {
        ArgumentNullException.ThrowIfNull(req.WritableStorage);
        if (await req.Storage.GetWopiContainer(req.Id, req.CancellationToken).ConfigureAwait(false) is null)
        {
            return TypedResults.NotFound();
        }
        try
        {
            if (await req.WritableStorage.DeleteWopiContainer(req.Id, req.CancellationToken).ConfigureAwait(false))
            {
                return TypedResults.Ok();
            }
            // Provider returns false when the id no longer resolves — concurrent delete won.
            return TypedResults.NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            // Defensive catch for third-party providers that still throw on missing resource.
            return TypedResults.NotFound();
        }
        catch (InvalidOperationException)
        {
            // 409 — container has child files / containers.
            return TypedResults.Conflict();
        }
    }

    private static async Task<Results<NotFound, JsonHttpResult<RenameContainerResponse>, BadRequest, Conflict>> RenameContainer(
        [AsParameters] RenameContainerRequest req)
    {
        ArgumentNullException.ThrowIfNull(req.WritableStorage);
        var container = await req.Storage.GetWopiContainer(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (container is null) return TypedResults.NotFound();

        if (!await req.WritableStorage.CheckValidContainerName(req.RequestedName, req.CancellationToken).ConfigureAwait(false))
        {
            req.Http.Response.Headers[WopiHeaders.INVALID_CONTAINER_NAME] = "Specified name is illegal";
            return TypedResults.BadRequest();
        }
        return await TryRenameContainerAsync(req.Http, req.WritableStorage, req.Id, req.RequestedName, req.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<Results<NotFound, JsonHttpResult<RenameContainerResponse>, BadRequest, Conflict>> TryRenameContainerAsync(HttpContext httpContext, IWopiWritableStorageProvider writableStorageProvider, string id, string requestedName, CancellationToken cancellationToken)
    {
        try
        {
            // Spec: response is JSON with a single required Name property — the NEW name. The
            // pre-fix impl returned `container.Name` from the snapshot we fetched before the
            // rename, so clients received the OLD name and got out-of-sync.
            return await writableStorageProvider.RenameWopiContainer(id, requestedName, cancellationToken).ConfigureAwait(false)
                ? TypedResults.Json(new RenameContainerResponse(requestedName))
                : TypedResults.NotFound(); // false → missing resource (race with concurrent delete).
        }
        catch (ArgumentException ae) when (ae.ParamName == nameof(requestedName))
        {
            httpContext.Response.Headers[WopiHeaders.INVALID_CONTAINER_NAME] = "Specified name is illegal";
            return TypedResults.BadRequest();
        }
        catch (DirectoryNotFoundException) { return TypedResults.NotFound(); }
        catch (InvalidOperationException) { return TypedResults.Conflict(); }
    }
}

/// <summary>Response payload for <c>RenameContainer</c> — single required <c>Name</c> property per spec.</summary>
public sealed record RenameContainerResponse(string Name);

// [FromServices] on IWopiWritableStorageProvider? is load-bearing — see the equivalent note in
// FileMutatingEndpoints. The nullable signal communicates the optional contract while the
// attribute forces DI lookup at the binder so unregistered hosts still resolve a null instead
// of triggering body-inference startup failure.

/// <summary>Parameter bundle for <see cref="ContainerMutatingEndpoints.CreateChildContainer"/>.</summary>
internal readonly record struct CreateChildContainerRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    [FromServices] IWopiWritableStorageProvider? WritableStorage,
    ICheckContainerInfoBuilder ContainerInfoBuilder,
    IWopiResourceTokenMinter TokenMinter,
    [FromHeader(Name = WopiHeaders.SUGGESTED_TARGET)] UtfString? SuggestedTarget,
    [FromHeader(Name = WopiHeaders.RELATIVE_TARGET)] UtfString? RelativeTarget,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for <see cref="ContainerMutatingEndpoints.CreateChildFile"/>.</summary>
internal readonly record struct CreateChildFileRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    [FromServices] IWopiWritableStorageProvider? WritableStorage,
    IWopiNewChildFileNegotiator Negotiator,
    ICheckFileInfoBuilder CheckFileInfoBuilder,
    IWopiResourceTokenMinter TokenMinter,
    [FromHeader(Name = WopiHeaders.SUGGESTED_TARGET)] UtfString? SuggestedTarget,
    [FromHeader(Name = WopiHeaders.RELATIVE_TARGET)] UtfString? RelativeTarget,
    [FromHeader(Name = WopiHeaders.OVERWRITE_RELATIVE_TARGET)] bool? OverwriteRelativeTarget,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for <see cref="ContainerMutatingEndpoints.DeleteContainer"/>.</summary>
internal readonly record struct DeleteContainerRequest(
    [FromRoute] string Id,
    IWopiStorageProvider Storage,
    [FromServices] IWopiWritableStorageProvider? WritableStorage,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for <see cref="ContainerMutatingEndpoints.RenameContainer"/>.</summary>
internal readonly record struct RenameContainerRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    [FromServices] IWopiWritableStorageProvider? WritableStorage,
    [FromHeader(Name = WopiHeaders.REQUESTED_NAME)] UtfString RequestedName,
    CancellationToken CancellationToken);
