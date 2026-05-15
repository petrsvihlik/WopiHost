using System.Globalization;
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
/// Read-only Minimal-API endpoints for WOPI container resources. Mirrors the GET surface of
/// <c>ContainersController</c>.
/// </summary>
internal static class ContainerEndpoints
{
    public static void MapContainerEndpoints(IEndpointRouteBuilder wopi)
    {
        var containers = wopi.MapGroup("/containers")
            .WithMetadata(new WopiResourceKindMetadata(WopiResourceType.Container));

        containers.MapGet("/{id}", CheckContainerInfo)
            .WithName(WopiRouteNames.CheckContainerInfo)
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.Read)));

        containers.MapGet("/{id}/ecosystem_pointer", GetEcosystem)
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.Read)));

        containers.MapGet("/{id}/ancestry", EnumerateAncestors)
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.Read)));

        containers.MapGet("/{id}/children", EnumerateChildren)
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.Read)));

        ContainerMutatingEndpoints.MapContainerMutatingEndpoints(containers);
    }

    private static async Task<IResult> CheckContainerInfo(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storageProvider,
        ICheckContainerInfoBuilder checkContainerInfoBuilder,
        CancellationToken cancellationToken)
    {
        var container = await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false);
        if (container is null) return TypedResults.NotFound();
        var info = await checkContainerInfoBuilder.BuildAsync(container, httpContext, cancellationToken).ConfigureAwait(false);
        return TypedResults.Json(info);
    }

    private static async Task<IResult> GetEcosystem(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storageProvider,
        IWopiAccessTokenService accessTokenService,
        CancellationToken cancellationToken)
    {
        var container = await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false);
        if (container is null) return TypedResults.NotFound();
        return await EndpointHelpers.IssueEcosystemPointerAsync(
            httpContext, container.Identifier, WopiResourceType.Container, accessTokenService, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> EnumerateAncestors(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storageProvider,
        CancellationToken cancellationToken)
    {
        if (await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false) is null)
        {
            return TypedResults.NotFound();
        }

        var url = httpContext.GetUrlHelper();
        var ancestors = await storageProvider.GetContainerAncestors(id, cancellationToken).ConfigureAwait(false);
        var response = new EnumerateAncestorsResponse(ancestors.Select(a => new ChildContainer(a.Name, url.GetWopiSrc(a))));
        return TypedResults.Json(response);
    }

    private static async Task<IResult> EnumerateChildren(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storageProvider,
        [FromHeader(Name = WopiHeaders.FILE_EXTENSION_FILTER_LIST)] string? fileExtensionFilterList,
        CancellationToken cancellationToken)
    {
        if (await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false) is null)
        {
            return TypedResults.NotFound();
        }

        var url = httpContext.GetUrlHelper();
        var files = new List<ChildFile>();
        var containers = new List<ChildContainer>();
        var fileExtensions = fileExtensionFilterList?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        await foreach (var wopiFile in storageProvider.GetWopiFiles(id, fileExtensions, cancellationToken).ConfigureAwait(false))
        {
            files.Add(new ChildFile(wopiFile.Name + '.' + wopiFile.Extension, url.GetWopiSrc(wopiFile))
            {
                LastModifiedTime = wopiFile.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture),
                Size = wopiFile.Length,
                Version = wopiFile.Version ?? wopiFile.LastWriteTimeUtc.ToString("s", CultureInfo.InvariantCulture),
            });
        }
        await foreach (var wopiContainer in storageProvider.GetWopiContainers(id, cancellationToken).ConfigureAwait(false))
        {
            containers.Add(new ChildContainer(wopiContainer.Name, url.GetWopiSrc(wopiContainer)));
        }

        return TypedResults.Json(new Container { ChildFiles = files, ChildContainers = containers });
    }
}
