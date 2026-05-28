using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// Read-only Minimal-API endpoints for WOPI container resources. Read side of the
/// <c>/wopi/containers/{id}</c> surface — CheckContainerInfo, ecosystem_pointer, ancestry, children.
/// </summary>
internal static class ContainerEndpoints
{
    public static void MapContainerEndpoints(IEndpointRouteBuilder wopi)
    {
        var containers = wopi.MapGroup("/containers")
            .WithTags("Containers")
            .WithMetadata(new WopiResourceKindMetadata(WopiResourceType.Container));

        containers.MapGet("/{id}", CheckContainerInfo)
            .WithName(WopiRouteNames.CheckContainerInfo)
            .WithSummary("CheckContainerInfo — returns container metadata.")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/checkcontainerinfo. " +
                "Mirrors the file-side CheckFileInfo response shape for container resources.")
            .RequireWopiPermission(WopiResourceType.Container, Permission.Read);

        containers.MapGet("/{id}/ecosystem_pointer", GetEcosystem)
            .WithSummary("Container ecosystem-pointer — returns a UrlResponse pointing at /wopi/ecosystem.")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/getecosystem. " +
                "Fresh minimum-privilege token avoids token-trading.")
            .RequireWopiPermission(WopiResourceType.Container, Permission.Read);

        containers.MapGet("/{id}/ancestry", EnumerateAncestors)
            .WithSummary("EnumerateAncestors — ancestor container chain for this container.")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/enumerateancestors. " +
                "Each ancestor URL carries a freshly minted container-scoped token.")
            .RequireWopiPermission(WopiResourceType.Container, Permission.Read);

        containers.MapGet("/{id}/children", EnumerateChildren)
            .WithSummary("EnumerateChildren — lists child files and containers.")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/enumeratechildren. " +
                "Honours the X-WOPI-FileExtensionFilterList header to constrain returned files by extension. " +
                "Each child URL carries a resource-scoped token to prevent token trading on the parent token.")
            .RequireWopiPermission(WopiResourceType.Container, Permission.Read);

        ContainerMutatingEndpoints.MapContainerMutatingEndpoints(containers);
    }

    private static async Task<Results<NotFound, JsonHttpResult<WopiCheckContainerInfo>>> CheckContainerInfo(
        [AsParameters] CheckContainerInfoRequest req)
    {
        var container = await req.Storage.GetWopiContainer(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (container is null) return TypedResults.NotFound();
        var info = await req.Builder.BuildAsync(container, req.Http.User, req.CancellationToken).ConfigureAwait(false);
        return TypedResults.Json(info);
    }

    private static async Task<Results<NotFound, JsonHttpResult<UrlResponse>>> GetEcosystem(
        [AsParameters] GetEcosystemRequest req)
    {
        var container = await req.Storage.GetWopiContainer(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (container is null) return TypedResults.NotFound();
        var ecosystemToken = await req.TokenMinter.MintForEcosystemAsync(
            req.Http.User, container.Identifier, WopiResourceType.Container, req.CancellationToken).ConfigureAwait(false);
        var url = req.Http.GetWopiSrc(WopiRouteNames.CheckEcosystem, identifier: null, accessToken: ecosystemToken.Token);
        return TypedResults.Json(new UrlResponse(url));
    }

    private static async Task<Results<NotFound, JsonHttpResult<EnumerateAncestorsResponse>>> EnumerateAncestors(
        [AsParameters] EnumerateAncestorsRequest req)
    {
        if (await req.Storage.GetWopiContainer(req.Id, req.CancellationToken).ConfigureAwait(false) is null)
        {
            return TypedResults.NotFound();
        }

        var ancestors = await req.Storage.GetContainerAncestors(req.Id, req.CancellationToken).ConfigureAwait(false);
        // Fresh container-scoped token per ancestor URL — see IWopiResourceTokenMinter for the
        // token-trading prevention rationale. Going through the injected minter keeps Infer#'s
        // async-state-machine analysis clean (#471): the await lands on an injected interface
        // method, not a same-class or shared static async helper.
        var children = new List<ChildContainer>();
        foreach (var ancestor in ancestors)
        {
            var ancestorToken = await req.TokenMinter.MintForContainerAsync(req.Http.User, ancestor, req.CancellationToken).ConfigureAwait(false);
            children.Add(new ChildContainer(ancestor.Name, req.Http.GetWopiSrc(ancestor, ancestorToken.Token)));
        }
        return TypedResults.Json(new EnumerateAncestorsResponse(children));
    }

    private static async Task<Results<NotFound, JsonHttpResult<Container>>> EnumerateChildren(
        [AsParameters] EnumerateContainerChildrenRequest req)
    {
        if (await req.Storage.GetWopiContainer(req.Id, req.CancellationToken).ConfigureAwait(false) is null)
        {
            return TypedResults.NotFound();
        }

        var files = new List<ChildFile>();
        var containers = new List<ChildContainer>();
        var fileExtensions = req.FileExtensionFilterList?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Mint per-child resource-scoped tokens — the inbound token is bound to the PARENT
        // container's id, so reusing it for child URLs trips "preventing token trading"
        // (https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/security#preventing-token-trading).
        // Routed through IWopiResourceTokenMinter so the await lands on an injected interface
        // method — see #471 for the Infer# precision-loss this shape avoids.
        await foreach (var wopiFile in req.Storage.GetWopiFiles(req.Id, fileExtensions, req.CancellationToken).ConfigureAwait(false))
        {
            var fileToken = await req.TokenMinter.MintForFileAsync(req.Http.User, wopiFile, req.CancellationToken).ConfigureAwait(false);
            files.Add(new ChildFile(wopiFile.Name + '.' + wopiFile.Extension, req.Http.GetWopiSrc(wopiFile, fileToken.Token))
            {
                LastModifiedTime = wopiFile.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture),
                Size = wopiFile.Length,
                Version = wopiFile.Version ?? wopiFile.LastWriteTimeUtc.ToString("s", CultureInfo.InvariantCulture),
            });
        }
        await foreach (var wopiContainer in req.Storage.GetWopiContainers(req.Id, req.CancellationToken).ConfigureAwait(false))
        {
            var containerToken = await req.TokenMinter.MintForContainerAsync(req.Http.User, wopiContainer, req.CancellationToken).ConfigureAwait(false);
            containers.Add(new ChildContainer(wopiContainer.Name, req.Http.GetWopiSrc(wopiContainer, containerToken.Token)));
        }

        return TypedResults.Json(new Container { ChildFiles = files, ChildContainers = containers });
    }
}

/// <summary>Parameter bundle for <see cref="ContainerEndpoints.CheckContainerInfo"/>.</summary>
internal readonly record struct CheckContainerInfoRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    ICheckContainerInfoBuilder Builder,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for the container-side <see cref="ContainerEndpoints.EnumerateChildren"/>.</summary>
internal readonly record struct EnumerateContainerChildrenRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    IWopiResourceTokenMinter TokenMinter,
    [FromHeader(Name = WopiHeaders.FILE_EXTENSION_FILTER_LIST)] string? FileExtensionFilterList,
    CancellationToken CancellationToken);
