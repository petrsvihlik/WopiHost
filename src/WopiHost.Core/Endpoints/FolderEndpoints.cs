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
/// Read-only Minimal-API endpoints for the WOPI folder surface. Mirrors
/// <c>FoldersController</c>. Folders are containers from a permissions standpoint
/// (<see cref="WopiResourceType.Container"/>) but expose only the legacy folder shape.
/// </summary>
internal static class FolderEndpoints
{
    public static void MapFolderEndpoints(IEndpointRouteBuilder wopi)
    {
        var folders = wopi.MapGroup("/folders")
            .WithMetadata(new WopiResourceKindMetadata(WopiResourceType.Container));

        folders.MapGet("/{id}", CheckFolderInfo)
            .WithName(WopiRouteNames.CheckFolderInfo)
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.Read)));

        folders.MapGet("/{id}/children", EnumerateChildren)
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.Read)));
    }

    private static async Task<IResult> CheckFolderInfo(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storageProvider,
        ICheckFolderInfoBuilder checkFolderInfoBuilder,
        IWopiHostExtensions extensions,
        CancellationToken cancellationToken)
    {
        var folder = await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false);
        if (folder is null) return TypedResults.NotFound();

        // Build sync, then fire the host hook — keeps the only `await` on a direct interface
        // call, mirroring the controller version's structure to avoid the Infer# FP at #363.
        var checkFolderInfo = checkFolderInfoBuilder.Build(folder, httpContext);
        checkFolderInfo = await extensions.OnCheckFolderInfoAsync(
            new WopiCheckFolderInfoContext(httpContext.User, folder, checkFolderInfo),
            cancellationToken).ConfigureAwait(false);
        return TypedResults.Json(checkFolderInfo);
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

        // Anonymous object shape matches the legacy FoldersController.EnumerateChildren payload
        // (only ChildFiles — no ChildContainers, by design of the historic folder surface).
        return TypedResults.Json(new { ChildFiles = files });
    }
}
