using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// Read-only Minimal-API endpoints for WOPI file resources. Mirrors the GET surface of
/// <c>FilesController</c> behind a <see cref="WopiRouteNames"/>-named route table.
/// </summary>
internal static class FileEndpoints
{
    private const string UserInfoCacheKeyPrefix = "UserInfo-";

    public static void MapFileEndpoints(IEndpointRouteBuilder wopi)
    {
        var files = wopi.MapGroup("/files");

        files.MapGet("/{id}", CheckFileInfo)
            .WithName(WopiRouteNames.CheckFileInfo)
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Read)));

        files.MapGet("/{id}/contents", GetFile)
            .WithName(WopiRouteNames.GetFile)
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Read)));

        files.MapGet("/{id}/ecosystem_pointer", GetEcosystem)
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Read)));

        files.MapGet("/{id}/ancestry", EnumerateAncestors)
            .RequireAuthorization(p => p.AddRequirements(new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Read)));

        FileMutatingEndpoints.MapFileMutatingEndpoints(files);
    }

    private static async Task<IResult> CheckFileInfo(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storage,
        IMemoryCache memoryCache,
        ICheckFileInfoBuilder builder,
        [FromServices] IWopiLockProvider? lockProvider,
        [FromServices] ICobaltProcessor? cobaltProcessor,
        [FromServices] IWopiWritableStorageProvider? writableStorage,
        CancellationToken cancellationToken)
    {
        var file = await storage.GetWopiFile(id, cancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        _ = memoryCache.TryGetValue($"{UserInfoCacheKeyPrefix}{httpContext.User.GetUserId()}", out string? userInfo);

        // Capabilities must mirror what's actually wired up — advertising features the host
        // can't deliver would lie to the WOPI client. Cobalt provides the multi-user editing
        // surface, so SupportsCoauth tracks its registration alongside SupportsCobalt.
        // SupportsUpdate must reflect writable-storage presence; without it PutFile /
        // RenameFile / DeleteFile all 501 via RequiresWritableStorageEndpointFilter, and
        // DefaultCheckFileInfoBuilder cascades SupportsUpdate=false into
        // UserCanNotWriteRelative=true per
        // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile.
        var capabilities = new WopiHostCapabilities
        {
            SupportsCobalt = cobaltProcessor is not null,
            SupportsGetLock = lockProvider is not null,
            SupportsLocks = lockProvider is not null,
            SupportsCoauth = cobaltProcessor is not null,
            SupportsUpdate = writableStorage is not null,
        };

        var checkFileInfo = await builder.BuildAsync(file, httpContext, capabilities, userInfo, cancellationToken).ConfigureAwait(false);

        // Serialize<object>() so any properties declared on a derived WopiCheckFileInfo type
        // make it onto the wire — System.Text.Json walks the runtime type, not the declared type.
        return TypedResults.Content(JsonSerializer.Serialize<object>(checkFileInfo), MediaTypeNames.Application.Json);
    }

    private static async Task<IResult> GetFile(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storageProvider,
        [FromHeader(Name = WopiHeaders.MAX_EXPECTED_SIZE)] int? maximumExpectedSize,
        CancellationToken cancellationToken)
    {
        var file = await storageProvider.GetWopiFile(id, cancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        var size = file.Exists ? file.Length : 0;
        if (maximumExpectedSize is not null && size > maximumExpectedSize.Value)
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        if (file.Version is not null)
        {
            httpContext.Response.Headers[WopiHeaders.ITEM_VERSION] = file.Version;
        }

        var stream = await file.OpenReadAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Stream(stream, MediaTypeNames.Application.Octet);
    }

    private static async Task<IResult> GetEcosystem(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storageProvider,
        IWopiAccessTokenService accessTokenService,
        CancellationToken cancellationToken)
    {
        var file = await storageProvider.GetWopiFile(id, cancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();
        return await EndpointHelpers.IssueEcosystemPointerAsync(
            httpContext, file.Identifier, WopiResourceType.File, accessTokenService, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> EnumerateAncestors(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storageProvider,
        IWopiAccessTokenService accessTokenService,
        IWopiPermissionProvider permissionProvider,
        CancellationToken cancellationToken)
    {
        var file = await storageProvider.GetWopiFile(id, cancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        var ancestors = await storageProvider.GetFileAncestors(id, cancellationToken).ConfigureAwait(false);
        // Mint a fresh container-scoped token per ancestor URL. Reusing the inbound file token
        // here would surface the same token-trading hazard the PutRelativeFile cleanup addressed
        // — see https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/security#preventing-token-trading
        var children = new List<ChildContainer>();
        foreach (var ancestor in ancestors)
        {
            var ancestorToken = await EndpointHelpers.IssueAccessTokenForContainerAsync(
                httpContext, accessTokenService, permissionProvider, ancestor, cancellationToken).ConfigureAwait(false);
            children.Add(new ChildContainer(ancestor.Name, httpContext.GetWopiSrc(ancestor, ancestorToken)));
        }
        return TypedResults.Json(new EnumerateAncestorsResponse(children));
    }
}
