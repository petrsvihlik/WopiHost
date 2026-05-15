using System.Net.Mime;
using System.Security.Claims;
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
    }

    private static async Task<IResult> CheckFileInfo(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storageProvider,
        IMemoryCache memoryCache,
        ICheckFileInfoBuilder checkFileInfoBuilder,
        // Optional providers — annotate explicitly so Minimal-API registration doesn't fall
        // back to body inference when the provider isn't registered (lock and cobalt providers
        // ship as separate opt-in packages).
        [FromServices] IWopiLockProvider? lockProvider,
        [FromServices] ICobaltProcessor? cobaltProcessor,
        CancellationToken cancellationToken)
    {
        var file = await storageProvider.GetWopiFile(id, cancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        _ = memoryCache.TryGetValue($"{UserInfoCacheKeyPrefix}{httpContext.User.GetUserId()}", out string? userInfo);

        var capabilities = new WopiHostCapabilities
        {
            SupportsCobalt = cobaltProcessor is not null,
            SupportsGetLock = lockProvider is not null,
            SupportsLocks = lockProvider is not null,
            SupportsCoauth = false,
            SupportsUpdate = true,
        };

        var checkFileInfo = await checkFileInfoBuilder.BuildAsync(file, httpContext, capabilities, userInfo, cancellationToken).ConfigureAwait(false);

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

        // Issue a minimum-privilege token: the URL points to CheckEcosystem which has no
        // resource gate. Reusing the inbound token violates WOPI "preventing token trading".
        var token = await accessTokenService.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = httpContext.User.GetUserId(),
            UserDisplayName = httpContext.User.FindFirstValue(ClaimTypes.Name),
            UserEmail = httpContext.User.FindFirstValue(ClaimTypes.Email),
            ResourceId = file.Identifier,
            ResourceType = WopiResourceType.File,
            FilePermissions = WopiFilePermissions.None,
        }, cancellationToken).ConfigureAwait(false);

        var url = httpContext.GetUrlHelper().GetWopiSrc(WopiRouteNames.CheckEcosystem, identifier: null, accessToken: token.Token);
        return TypedResults.Json(new UrlResponse(url));
    }

    private static async Task<IResult> EnumerateAncestors(
        string id,
        HttpContext httpContext,
        IWopiStorageProvider storageProvider,
        CancellationToken cancellationToken)
    {
        var file = await storageProvider.GetWopiFile(id, cancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        var url = httpContext.GetUrlHelper();
        var ancestors = await storageProvider.GetFileAncestors(id, cancellationToken).ConfigureAwait(false);
        var response = new EnumerateAncestorsResponse(ancestors.Select(a => new ChildContainer(a.Name, url.GetWopiSrc(a))));
        return TypedResults.Json(response);
    }
}
