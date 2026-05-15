using System.Diagnostics.CodeAnalysis;
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
/// Minimal-API mutating endpoints for the <c>/wopi/containers/{id}</c> surface. Mirrors the
/// non-GET actions of <c>ContainersController</c>, dispatched via <c>X-WOPI-Override</c>.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Phase 3 of #430 migration; HTTP parity tests land in phase 5 (test relocation into WopiHost.IntegrationTests)")]
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

    /// <summary>Bundle of services consumed by <see cref="CreateChildContainer"/>.</summary>
    internal sealed record CreateChildContainerDeps(
        IWopiStorageProvider Storage,
        [property: FromServices] IWopiWritableStorageProvider? WritableStorage,
        ICheckContainerInfoBuilder ContainerInfoBuilder);

    private static async Task<IResult> CreateChildContainer(
        string id,
        HttpContext httpContext,
        [AsParameters] CreateChildContainerDeps deps,
        [FromHeader(Name = WopiHeaders.SUGGESTED_TARGET)] UtfString? suggestedTarget,
        [FromHeader(Name = WopiHeaders.RELATIVE_TARGET)] UtfString? relativeTarget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deps.WritableStorage);
        if (await deps.Storage.GetWopiContainer(id, cancellationToken).ConfigureAwait(false) is null) return TypedResults.NotFound();

        // Mutually exclusive headers per spec: 501 when both present or both missing.
        if ((!string.IsNullOrWhiteSpace(suggestedTarget) && !string.IsNullOrWhiteSpace(relativeTarget))
            || (string.IsNullOrWhiteSpace(suggestedTarget) && string.IsNullOrWhiteSpace(relativeTarget)))
        {
            return TypedResults.StatusCode(StatusCodes.Status501NotImplemented);
        }
        if (!await deps.WritableStorage.CheckValidContainerName((suggestedTarget ?? relativeTarget)!, cancellationToken).ConfigureAwait(false))
        {
            return TypedResults.BadRequest();
        }

        var (newFolder, conflict) = await ResolveNewChildContainer(httpContext, deps, id, suggestedTarget, relativeTarget, cancellationToken).ConfigureAwait(false);
        if (conflict is not null) return conflict;
        if (newFolder is null) return TypedResults.StatusCode(StatusCodes.Status500InternalServerError);

        var info = await deps.ContainerInfoBuilder.BuildAsync(newFolder, httpContext, cancellationToken).ConfigureAwait(false);
        var url = httpContext.GetUrlHelper();
        return TypedResults.Json(new CreateChildContainerResponse(new(newFolder.Name, url.GetWopiSrc(newFolder)), info));
    }

    /// <summary>
    /// Dispatches the relative-target ("specific mode") and suggested-target name-negotiation
    /// branches. Returns <c>(folder, null)</c> on success or <c>(null, conflict)</c> when the
    /// specific-mode name collides — the conflict result writes <c>X-WOPI-ValidRelativeTarget</c>.
    /// </summary>
    private static async Task<(IWopiContainer? folder, IResult? conflict)> ResolveNewChildContainer(
        HttpContext httpContext, CreateChildContainerDeps deps, string id, UtfString? suggestedTarget, UtfString? relativeTarget, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(relativeTarget))
        {
            // "specific mode" — host must not modify the name.
            var existing = await deps.Storage.GetWopiContainerByName(id, relativeTarget, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                var suggestedName = await deps.WritableStorage!.GetSuggestedContainerName(id, relativeTarget, cancellationToken).ConfigureAwait(false);
                httpContext.Response.Headers[WopiHeaders.VALID_RELATIVE_TARGET] = UtfString.FromDecoded(suggestedName).ToString(true);
                return (null, TypedResults.Conflict());
            }
            return (await deps.WritableStorage!.CreateWopiChildContainer(id, relativeTarget, cancellationToken).ConfigureAwait(false), null);
        }
        // suggested-target branch — host may dedupe the name.
        var newName = await deps.WritableStorage!.GetSuggestedContainerName(id, suggestedTarget!, cancellationToken).ConfigureAwait(false);
        return (await deps.WritableStorage.CreateWopiChildContainer(id, newName, cancellationToken).ConfigureAwait(false), null);
    }

    /// <summary>Bundle of services consumed by <see cref="CreateChildFile"/>.</summary>
    internal sealed record CreateChildFileDeps(
        IWopiStorageProvider Storage,
        [property: FromServices] IWopiWritableStorageProvider? WritableStorage,
        IWopiNewChildFileNegotiator Negotiator,
        ICheckFileInfoBuilder CheckFileInfoBuilder);

    private static async Task<IResult> CreateChildFile(
        string id,
        HttpContext httpContext,
        [AsParameters] CreateChildFileDeps deps,
        [FromHeader(Name = WopiHeaders.SUGGESTED_TARGET)] UtfString? suggestedTarget,
        [FromHeader(Name = WopiHeaders.RELATIVE_TARGET)] UtfString? relativeTarget,
        [FromHeader(Name = WopiHeaders.OVERWRITE_RELATIVE_TARGET)] bool? overwriteRelativeTarget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deps.WritableStorage);
        var container = await deps.Storage.GetWopiContainer(id, cancellationToken).ConfigureAwait(false);
        if (container is null) return TypedResults.NotFound();

        if ((!string.IsNullOrWhiteSpace(suggestedTarget) && !string.IsNullOrWhiteSpace(relativeTarget))
            || (string.IsNullOrWhiteSpace(suggestedTarget) && string.IsNullOrWhiteSpace(relativeTarget)))
        {
            return TypedResults.StatusCode(StatusCodes.Status501NotImplemented);
        }

        // Suggested-target / relative-target negotiation — protocol shared with PutRelativeFile.
        // CreateChildFile has no source file in scope, so the extension-only fallback uses a
        // fresh GUID as the stem.
        var negotiation = await deps.Negotiator.NegotiateAsync(new WopiNewChildFileRequest(
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
        var checkFileInfo = await deps.CheckFileInfoBuilder.BuildAsync(newFile, httpContext, cancellationToken: cancellationToken).ConfigureAwait(false);
        var url = httpContext.GetUrlHelper();
        return TypedResults.Json(new ChildFile(newFile.Name + '.' + newFile.Extension, url.GetWopiSrc(newFile))
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
        return await TryRenameContainerAsync(httpContext, writableStorageProvider, id, requestedName, container, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> TryRenameContainerAsync(HttpContext httpContext, IWopiWritableStorageProvider writableStorageProvider, string id, string requestedName, IWopiContainer container, CancellationToken cancellationToken)
    {
        try
        {
            return await writableStorageProvider.RenameWopiContainer(id, requestedName, cancellationToken).ConfigureAwait(false)
                ? TypedResults.Json(new { container.Name })
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
