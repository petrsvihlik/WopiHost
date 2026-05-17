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

    /// <summary>
    /// Bundle of services consumed by <see cref="CheckFileInfo"/>. Collapses the handler's
    /// parameter list so Minimal-API DI binding stays explicit at the call site without
    /// drowning the route lambda. Optional services carry <c>[FromServices]</c> so the
    /// framework's registration-time check doesn't fall back to body inference when the
    /// associated provider (lock / cobalt) ships as a separate package and isn't registered.
    /// </summary>
    internal sealed record CheckFileInfoDeps(
        IWopiStorageProvider Storage,
        IMemoryCache MemoryCache,
        ICheckFileInfoBuilder Builder,
        [property: FromServices] IWopiLockProvider? LockProvider,
        [property: FromServices] ICobaltProcessor? CobaltProcessor);

    private static async Task<IResult> CheckFileInfo(
        string id,
        HttpContext httpContext,
        [AsParameters] CheckFileInfoDeps deps,
        CancellationToken cancellationToken)
    {
        var file = await deps.Storage.GetWopiFile(id, cancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        _ = deps.MemoryCache.TryGetValue($"{UserInfoCacheKeyPrefix}{httpContext.User.GetUserId()}", out string? userInfo);

        var capabilities = new WopiHostCapabilities
        {
            SupportsCobalt = deps.CobaltProcessor is not null,
            SupportsGetLock = deps.LockProvider is not null,
            SupportsLocks = deps.LockProvider is not null,
            SupportsCoauth = false,
            SupportsUpdate = true,
        };

        var checkFileInfo = await deps.Builder.BuildAsync(file, httpContext, capabilities, userInfo, cancellationToken).ConfigureAwait(false);

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
        CancellationToken cancellationToken)
    {
        var file = await storageProvider.GetWopiFile(id, cancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        var ancestors = await storageProvider.GetFileAncestors(id, cancellationToken).ConfigureAwait(false);
        var response = new EnumerateAncestorsResponse(ancestors.Select(a => new ChildContainer(a.Name, httpContext.GetWopiSrc(a))));
        return TypedResults.Json(response);
    }
}
