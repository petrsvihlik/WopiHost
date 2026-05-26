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
/// Read-only Minimal-API endpoints for the WOPI folder surface. Folders are containers from
/// a permissions standpoint (<see cref="WopiResourceType.Container"/>) but expose only the
/// legacy folder shape.
/// </summary>
internal static class FolderEndpoints
{
    public static void MapFolderEndpoints(IEndpointRouteBuilder wopi)
    {
        var folders = wopi.MapGroup("/folders")
            .WithTags("Folders")
            .WithMetadata(new WopiResourceKindMetadata(WopiResourceType.Container));

        folders.MapGet("/{id}", CheckFolderInfo)
            .WithName(WopiRouteNames.CheckFolderInfo)
            .WithSummary("CheckFolderInfo — folder metadata (legacy folder surface).")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/folders/checkfolderinfo. " +
                "Authorization keys off the container permission since folders ARE containers in this host's model.")
            .RequireWopiPermission(WopiResourceType.Container, Permission.Read);

        folders.MapGet("/{id}/children", EnumerateChildren)
            .WithSummary("EnumerateChildren — list child files within this folder.")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/folders/enumeratechildren. " +
                "Folder shape historically returns only child files (no nested containers), matching the legacy folder enum.")
            .RequireWopiPermission(WopiResourceType.Container, Permission.Read);
    }

    private static async Task<Results<NotFound, JsonHttpResult<WopiCheckFolderInfo>>> CheckFolderInfo(
        [AsParameters] CheckFolderInfoRequest req)
    {
        var folder = await req.Storage.GetWopiContainer(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (folder is null) return TypedResults.NotFound();

        // Build sync, then fire the host hook — keeps the only `await` on a direct interface
        // call, mirroring the controller version's structure to avoid the Infer# FP at #363.
        var checkFolderInfo = req.Builder.Build(folder, req.Http.User);
        checkFolderInfo = await req.Extensions.OnCheckFolderInfoAsync(
            new WopiCheckFolderInfoContext(req.Http.User, folder, checkFolderInfo),
            req.CancellationToken).ConfigureAwait(false);
        return TypedResults.Json(checkFolderInfo);
    }

    private static async Task<Results<NotFound, JsonHttpResult<FolderChildrenResponse>>> EnumerateChildren(
        [AsParameters] EnumerateFolderChildrenRequest req)
    {
        if (await req.Storage.GetWopiContainer(req.Id, req.CancellationToken).ConfigureAwait(false) is null)
        {
            return TypedResults.NotFound();
        }

        var files = new List<ChildFile>();
        var fileExtensions = req.FileExtensionFilterList?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // Mint per-file resource-scoped tokens — see ContainerEndpoints.EnumerateChildren for
        // the same rationale (preventing token trading on child URLs). Awaits land directly
        // on the injected IWopiAccessTokenService.IssueAsync — see #471 for why.
        await foreach (var wopiFile in req.Storage.GetWopiFiles(req.Id, fileExtensions, req.CancellationToken).ConfigureAwait(false))
        {
            var filePerms = await req.PermissionProvider.GetFilePermissionsAsync(req.Http.User, wopiFile, req.CancellationToken).ConfigureAwait(false);
            var fileToken = await req.AccessTokenService.IssueAsync(
                EndpointHelpers.BuildResourceTokenRequest(req.Http.User, wopiFile.Identifier, WopiResourceType.File, filePermissions: filePerms),
                req.CancellationToken).ConfigureAwait(false);
            files.Add(new ChildFile(wopiFile.Name + '.' + wopiFile.Extension, req.Http.GetWopiSrc(wopiFile, fileToken.Token))
            {
                LastModifiedTime = wopiFile.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture),
                Size = wopiFile.Length,
                Version = wopiFile.Version ?? wopiFile.LastWriteTimeUtc.ToString("s", CultureInfo.InvariantCulture),
            });
        }

        // Folder shape matches the legacy FoldersController.EnumerateChildren payload
        // (only ChildFiles — no ChildContainers, by design of the historic folder surface).
        return TypedResults.Json(new FolderChildrenResponse(files));
    }
}

/// <summary>Parameter bundle for <see cref="FolderEndpoints.CheckFolderInfo"/>.</summary>
internal readonly record struct CheckFolderInfoRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    ICheckFolderInfoBuilder Builder,
    IWopiHostExtensions Extensions,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for the folder-side <see cref="FolderEndpoints.EnumerateChildren"/>.</summary>
internal readonly record struct EnumerateFolderChildrenRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    IWopiAccessTokenService AccessTokenService,
    IWopiPermissionProvider PermissionProvider,
    [FromHeader(Name = WopiHeaders.FILE_EXTENSION_FILTER_LIST)] string? FileExtensionFilterList,
    CancellationToken CancellationToken);

/// <summary>
/// Legacy folder-surface enumerate-children response payload: only <c>ChildFiles</c>, no
/// <c>ChildContainers</c>. Kept as a named record so typed-union returns surface a concrete
/// schema in OpenAPI rather than an anonymous shape.
/// </summary>
public sealed record FolderChildrenResponse(IReadOnlyList<ChildFile> ChildFiles);
