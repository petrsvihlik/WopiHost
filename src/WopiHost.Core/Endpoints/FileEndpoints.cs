using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// Read-only Minimal-API endpoints for WOPI file resources. Read-side of the
/// <c>/wopi/files/{id}</c> surface — CheckFileInfo, GetFile, ecosystem_pointer, ancestry.
/// </summary>
internal static class FileEndpoints
{
    private const string UserInfoCacheKeyPrefix = "UserInfo-";

    public static void MapFileEndpoints(IEndpointRouteBuilder wopi)
    {
        var files = wopi.MapGroup("/files")
            .WithTags("Files");

        files.MapGet("/{id}", CheckFileInfo)
            .WithName(WopiRouteNames.CheckFileInfo)
            .WithSummary("CheckFileInfo — returns file metadata for the WOPI client.")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo. " +
                "Returns the WopiCheckFileInfo JSON payload (capabilities, names, URLs, permissions). " +
                "Responds 404 when the resource does not resolve.")
            .RequireWopiPermission(WopiResourceType.File, Permission.Read);

        files.MapGet("/{id}/contents", GetFile)
            .WithName(WopiRouteNames.GetFile)
            .WithSummary("GetFile — streams the file binary.")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getfile. " +
                "Honours the X-WOPI-MaxExpectedSize header (412 when exceeded). Sets X-WOPI-ItemVersion on the response when the storage provider exposes a version.")
            .RequireWopiPermission(WopiResourceType.File, Permission.Read);

        files.MapGet("/{id}/ecosystem_pointer", GetEcosystem)
            .WithSummary("File ecosystem-pointer — returns a UrlResponse pointing at /wopi/ecosystem.")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getecosystem. " +
                "The returned URL carries a freshly minted minimum-privilege ecosystem access token; reusing the inbound file token would violate the WOPI \"preventing token trading\" guidance.")
            .RequireWopiPermission(WopiResourceType.File, Permission.Read);

        files.MapGet("/{id}/ancestry", EnumerateAncestors)
            .WithSummary("EnumerateAncestors — returns the ancestor container chain for this file.")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/enumerateancestors. " +
                "Each ancestor URL carries a fresh container-scoped token to avoid token-trading across resources.")
            .RequireWopiPermission(WopiResourceType.File, Permission.Read);

        FileMutatingEndpoints.MapFileMutatingEndpoints(files);
    }

    private static async Task<Results<NotFound, ContentHttpResult>> CheckFileInfo(
        [AsParameters] CheckFileInfoRequest req)
    {
        var file = await req.Storage.GetWopiFile(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        _ = req.MemoryCache.TryGetValue($"{UserInfoCacheKeyPrefix}{req.Http.User.GetUserId()}", out string? userInfo);

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
            SupportsCobalt = req.CobaltProcessor is not null,
            SupportsGetLock = req.LockProvider is not null,
            SupportsLocks = req.LockProvider is not null,
            SupportsCoauth = req.CobaltProcessor is not null,
            SupportsUpdate = req.WritableStorage is not null,
        };

        var checkFileInfo = await req.Builder.BuildAsync(file, req.Http.ToWopiRequestInfo(), capabilities, userInfo, req.CancellationToken).ConfigureAwait(false);

        // Serialize<object>() so any properties declared on a derived WopiCheckFileInfo type
        // make it onto the wire — System.Text.Json walks the runtime type, not the declared type.
        return TypedResults.Content(JsonSerializer.Serialize<object>(checkFileInfo), MediaTypeNames.Application.Json);
    }

    private static async Task<Results<NotFound, StatusCodeHttpResult, FileStreamHttpResult>> GetFile(
        [AsParameters] GetFileRequest req)
    {
        var file = await req.Storage.GetWopiFile(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        var size = file.Exists ? file.Length : 0;
        if (req.MaximumExpectedSize is not null && size > req.MaximumExpectedSize.Value)
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        if (file.Version is not null)
        {
            req.Http.Response.Headers[WopiHeaders.ITEM_VERSION] = file.Version;
        }

        var stream = await file.OpenReadAsync(req.CancellationToken).ConfigureAwait(false);
        return TypedResults.Stream(stream, MediaTypeNames.Application.Octet);
    }

    private static async Task<Results<NotFound, JsonHttpResult<UrlResponse>>> GetEcosystem(
        [AsParameters] GetEcosystemRequest req)
    {
        var file = await req.Storage.GetWopiFile(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();
        // Minimum-privilege token bound to the file's id (token-trading prevention, see
        // EndpointHelpers.BuildResourceTokenRequest). Direct await on the injected
        // IWopiAccessTokenService.IssueAsync — see ContainerEndpoints.GetEcosystem for why.
        var ecosystemToken = await req.AccessTokenService.IssueAsync(
            EndpointHelpers.BuildResourceTokenRequest(req.Http.User, file.Identifier, WopiResourceType.File),
            req.CancellationToken).ConfigureAwait(false);
        var url = req.Http.GetWopiSrc(WopiRouteNames.CheckEcosystem, identifier: null, accessToken: ecosystemToken.Token);
        return TypedResults.Json(new UrlResponse(url));
    }

    private static async Task<Results<NotFound, JsonHttpResult<EnumerateAncestorsResponse>>> EnumerateAncestors(
        [AsParameters] EnumerateAncestorsRequest req)
    {
        var file = await req.Storage.GetWopiFile(req.Id, req.CancellationToken).ConfigureAwait(false);
        if (file is null) return TypedResults.NotFound();

        var ancestors = await req.Storage.GetFileAncestors(req.Id, req.CancellationToken).ConfigureAwait(false);
        // Mint a fresh container-scoped token per ancestor URL. Reusing the inbound file token
        // here would surface the same token-trading hazard the PutRelativeFile cleanup addressed
        // — see https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/security#preventing-token-trading
        var children = new List<ChildContainer>();
        // Fresh container-scoped token per ancestor URL — same token-trading rationale as
        // ContainerEndpoints.EnumerateAncestors. The await lands directly on the injected
        // IWopiAccessTokenService.IssueAsync; routing through a static helper instead trips
        // an Infer# null-deref FP through the async state machine (#471).
        foreach (var ancestor in ancestors)
        {
            var perms = await req.PermissionProvider.GetContainerPermissionsAsync(req.Http.User, ancestor, req.CancellationToken).ConfigureAwait(false);
            var ancestorToken = await req.AccessTokenService.IssueAsync(
                EndpointHelpers.BuildResourceTokenRequest(req.Http.User, ancestor.Identifier, WopiResourceType.Container, containerPermissions: perms),
                req.CancellationToken).ConfigureAwait(false);
            children.Add(new ChildContainer(ancestor.Name, req.Http.GetWopiSrc(ancestor, ancestorToken.Token)));
        }
        return TypedResults.Json(new EnumerateAncestorsResponse(children));
    }
}

/// <summary>
/// Parameter bundle for <see cref="FileEndpoints.CheckFileInfo"/>. <c>[FromServices]</c> on the
/// nullable lock/cobalt/writable parameters is load-bearing: these services are optional and may
/// not be registered in every host configuration (e.g. <c>UseCobalt=false</c> skips
/// <c>AddCobalt()</c>). Without <c>[FromServices]</c> the Minimal-API binder doesn't see the type
/// in DI and falls back to body inference at startup, hard-erroring before any request is served.
/// </summary>
internal readonly record struct CheckFileInfoRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    IMemoryCache MemoryCache,
    ICheckFileInfoBuilder Builder,
    [FromServices] IWopiLockProvider? LockProvider,
    [FromServices] ICobaltProcessor? CobaltProcessor,
    [FromServices] IWopiWritableStorageProvider? WritableStorage,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for <see cref="FileEndpoints.GetFile"/>.</summary>
internal readonly record struct GetFileRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    [FromHeader(Name = WopiHeaders.MAX_EXPECTED_SIZE)] int? MaximumExpectedSize,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for the file/container <c>ecosystem_pointer</c> handlers.</summary>
internal readonly record struct GetEcosystemRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    IWopiAccessTokenService AccessTokenService,
    CancellationToken CancellationToken);

/// <summary>Parameter bundle for <see cref="FileEndpoints.EnumerateAncestors"/>.</summary>
internal readonly record struct EnumerateAncestorsRequest(
    [FromRoute] string Id,
    HttpContext Http,
    IWopiStorageProvider Storage,
    IWopiAccessTokenService AccessTokenService,
    IWopiPermissionProvider PermissionProvider,
    CancellationToken CancellationToken);
