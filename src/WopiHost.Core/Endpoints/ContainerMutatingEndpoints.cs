using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// Minimal-API mutating endpoints for the <c>/wopi/containers/{id}</c> surface, dispatched via
/// <c>X-WOPI-Override</c>.
/// </summary>
internal static class ContainerMutatingEndpoints
{
    public static void MapContainerMutatingEndpoints(RouteGroupBuilder containers)
    {
        containers.MapPost("/{id}", CreateChildContainer)
            .WithMetadata(new WopiOverrideMetadata(WopiContainerOperations.CreateChildContainer))
            .AddEndpointFilter<RequiresWritableStorageEndpointFilter>()
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.Create)));

        containers.MapPost("/{id}", CreateChildFile)
            .WithMetadata(new WopiOverrideMetadata(WopiContainerOperations.CreateChildFile))
            .AddEndpointFilter<RequiresWritableStorageEndpointFilter>()
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.CreateChildFile)));

        containers.MapPost("/{id}", DeleteContainer)
            .WithMetadata(new WopiOverrideMetadata(WopiContainerOperations.DeleteContainer))
            .AddEndpointFilter<RequiresWritableStorageEndpointFilter>()
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.Delete)));

        containers.MapPost("/{id}", RenameContainer)
            .WithMetadata(new WopiOverrideMetadata(WopiContainerOperations.RenameContainer))
            .AddEndpointFilter<RequiresWritableStorageEndpointFilter>()
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.Rename)));
    }

    private static async Task<IResult> CreateChildContainer(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storage,
        [FromServices] IWopiWritableStorageProvider? writableStorage,
        ICheckContainerInfoBuilder containerInfoBuilder,
        IWopiAccessTokenService accessTokenService,
        IWopiPermissionProvider permissionProvider,
        [FromHeader(Name = WopiHeaders.SUGGESTED_TARGET)] UtfString? suggestedTarget,
        [FromHeader(Name = WopiHeaders.RELATIVE_TARGET)] UtfString? relativeTarget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writableStorage);
        if (await storage.GetWopiContainer(id, cancellationToken).ConfigureAwait(false) is null) return TypedResults.NotFound();

        // Mutually exclusive headers per spec: 501 when both present or both missing.
        if ((!string.IsNullOrWhiteSpace(suggestedTarget) && !string.IsNullOrWhiteSpace(relativeTarget))
            || (string.IsNullOrWhiteSpace(suggestedTarget) && string.IsNullOrWhiteSpace(relativeTarget)))
        {
            return TypedResults.StatusCode(StatusCodes.Status501NotImplemented);
        }

        // Spec branches on which header is set:
        //  - Specific mode (X-WOPI-RelativeTarget): invalid name → 400 with
        //    X-WOPI-InvalidContainerNameError describing why.
        //  - Suggested mode (X-WOPI-SuggestedTarget): "must never result in a 400 Bad Request or
        //    409 Conflict. Rather, the host must modify the proposed name as needed to create a
        //    new container that is legally named." → sanitise on invalid input.
        string requestedName = (relativeTarget ?? suggestedTarget)!;
        if (!await writableStorage.CheckValidContainerName(requestedName, cancellationToken).ConfigureAwait(false))
        {
            if (relativeTarget is not null)
            {
                httpContext.Response.Headers[WopiHeaders.INVALID_CONTAINER_NAME] = "Specified name is illegal";
                return TypedResults.BadRequest();
            }
            // Suggested mode: sanitise. Fall back to a GUID stem if even sanitisation fails.
            requestedName = SanitiseContainerName(requestedName);
            if (!await writableStorage.CheckValidContainerName(requestedName, cancellationToken).ConfigureAwait(false))
            {
                requestedName = Guid.NewGuid().ToString("N");
            }
        }

        // Single exit for the resolve/build/respond tail — keeps the handler under qlty's
        // return-statements threshold (specific-mode conflict, provider null, and success
        // share one return via the switch).
        var resolved = await ResolveNewChildContainer(httpContext, storage, writableStorage, id, requestedName, isSpecificMode: relativeTarget is not null, cancellationToken).ConfigureAwait(false);
        return resolved switch
        {
            (null, { } conflict) => conflict,
            (null, _) => TypedResults.StatusCode(StatusCodes.Status500InternalServerError),
            ({ } folder, _) => await BuildCreateChildContainerResponse(httpContext, containerInfoBuilder, accessTokenService, permissionProvider, folder, cancellationToken).ConfigureAwait(false),
        };
    }

    private static async Task<IResult> BuildCreateChildContainerResponse(HttpContext httpContext, ICheckContainerInfoBuilder builder, IWopiAccessTokenService accessTokenService, IWopiPermissionProvider permissionProvider, IWopiContainer folder, CancellationToken cancellationToken)
    {
        var info = await builder.BuildAsync(folder, httpContext, cancellationToken).ConfigureAwait(false);
        // Mint a fresh container-scoped token for the new child container. Reusing the inbound
        // PARENT-container token in the response URL would either fail downstream authorization
        // or constitute token trading per
        // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/security#preventing-token-trading.
        var childToken = await EndpointHelpers.IssueAccessTokenForContainerAsync(
            httpContext, accessTokenService, permissionProvider, folder, cancellationToken).ConfigureAwait(false);
        return TypedResults.Json(new CreateChildContainerResponse(new(folder.Name, httpContext.GetWopiSrc(folder, childToken)), info));
    }

    /// <summary>
    /// Dispatches the relative-target ("specific mode") and suggested-target name-negotiation
    /// branches. Returns <c>(folder, null)</c> on success or <c>(null, conflict)</c> when the
    /// specific-mode name collides — the conflict result writes <c>X-WOPI-ValidRelativeTarget</c>.
    /// </summary>
    private static async Task<(IWopiContainer? folder, IResult? conflict)> ResolveNewChildContainer(
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

    private static async Task<IResult> CreateChildFile(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storage,
        [FromServices] IWopiWritableStorageProvider? writableStorage,
        IWopiNewChildFileNegotiator negotiator,
        ICheckFileInfoBuilder checkFileInfoBuilder,
        IWopiAccessTokenService accessTokenService,
        IWopiPermissionProvider permissionProvider,
        [FromHeader(Name = WopiHeaders.SUGGESTED_TARGET)] UtfString? suggestedTarget,
        [FromHeader(Name = WopiHeaders.RELATIVE_TARGET)] UtfString? relativeTarget,
        [FromHeader(Name = WopiHeaders.OVERWRITE_RELATIVE_TARGET)] bool? overwriteRelativeTarget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writableStorage);
        var container = await storage.GetWopiContainer(id, cancellationToken).ConfigureAwait(false);
        if (container is null) return TypedResults.NotFound();

        if ((!string.IsNullOrWhiteSpace(suggestedTarget) && !string.IsNullOrWhiteSpace(relativeTarget))
            || (string.IsNullOrWhiteSpace(suggestedTarget) && string.IsNullOrWhiteSpace(relativeTarget)))
        {
            return TypedResults.StatusCode(StatusCodes.Status501NotImplemented);
        }

        // Suggested-target / relative-target negotiation — protocol shared with PutRelativeFile.
        // CreateChildFile has no source file in scope, so the extension-only fallback uses a
        // fresh GUID as the stem.
        var negotiation = await negotiator.NegotiateAsync(new WopiNewChildFileRequest(
            ContainerId: container.Identifier,
            SuggestedTarget: suggestedTarget,
            RelativeTarget: relativeTarget,
            OverwriteRelativeTarget: overwriteRelativeTarget ?? false,
            SuggestedExtensionFallbackStem: Guid.NewGuid().ToString("N")), cancellationToken).ConfigureAwait(false);
        if (negotiation.ToErrorResult(httpContext.Response) is { } error)
        {
            return error;
        }

        var newFile = negotiation.File!;
        var checkFileInfo = await checkFileInfoBuilder.BuildAsync(newFile, httpContext, cancellationToken: cancellationToken).ConfigureAwait(false);
        // Fresh, resource-scoped token for the new file. The inbound token is bound to the parent
        // CONTAINER's id and would fail authorization on the new file's CheckFileInfo callback.
        var newFileToken = await EndpointHelpers.IssueAccessTokenForFileAsync(
            httpContext, accessTokenService, permissionProvider, newFile, cancellationToken).ConfigureAwait(false);
        return TypedResults.Json(new ChildFile(newFile.Name + '.' + newFile.Extension, httpContext.GetWopiSrc(newFile, newFileToken))
        {
            HostEditUrl = checkFileInfo.HostEditUrl,
            HostViewUrl = checkFileInfo.HostViewUrl,
        });
    }

    private static async Task<IResult> DeleteContainer(
        string id,
        IWopiStorageProvider storageProvider,
        [FromServices] IWopiWritableStorageProvider? writableStorageProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writableStorageProvider);
        if (await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false) is null)
        {
            return TypedResults.NotFound();
        }
        try
        {
            if (await writableStorageProvider.DeleteWopiContainer(id, cancellationToken).ConfigureAwait(false))
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

    private static async Task<IResult> RenameContainer(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storageProvider,
        [FromServices] IWopiWritableStorageProvider? writableStorageProvider,
        [FromHeader(Name = WopiHeaders.REQUESTED_NAME)] UtfString requestedName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writableStorageProvider);
        var container = await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false);
        if (container is null) return TypedResults.NotFound();

        if (!await writableStorageProvider.CheckValidContainerName(requestedName, cancellationToken).ConfigureAwait(false))
        {
            httpContext.Response.Headers[WopiHeaders.INVALID_CONTAINER_NAME] = "Specified name is illegal";
            return TypedResults.BadRequest();
        }
        return await TryRenameContainerAsync(httpContext, writableStorageProvider, id, requestedName, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> TryRenameContainerAsync(HttpContext httpContext, IWopiWritableStorageProvider writableStorageProvider, string id, string requestedName, CancellationToken cancellationToken)
    {
        try
        {
            // Spec: response is JSON with a single required Name property — the NEW name. The
            // pre-fix impl returned `container.Name` from the snapshot we fetched before the
            // rename, so clients received the OLD name and got out-of-sync.
            return await writableStorageProvider.RenameWopiContainer(id, requestedName, cancellationToken).ConfigureAwait(false)
                ? TypedResults.Json(new { Name = requestedName })
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
