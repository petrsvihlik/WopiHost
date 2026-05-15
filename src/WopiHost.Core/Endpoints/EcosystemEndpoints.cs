using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// WOPI ecosystem-pointer endpoints. Mirrors the GET surface of <c>EcosystemController</c>.
/// These endpoints do not carry per-resource WopiAuthorize requirements — only the standard
/// authenticated-user gate from the group's <c>RequireAuthorization()</c>.
/// </summary>
internal static class EcosystemEndpoints
{
    public static void MapEcosystemEndpoints(IEndpointRouteBuilder wopi)
    {
        // Two endpoints, no shared metadata — register directly. Using MapGroup("/ecosystem")
        // + MapGet("") would produce the wrong RawText (the empty inner path doesn't normalise
        // to the group's exact prefix).
        wopi.MapGet("/ecosystem", CheckEcosystem)
            .WithName(WopiRouteNames.CheckEcosystem);

        wopi.MapGet("/ecosystem/root_container_pointer", GetRootContainer);
    }

    private static async Task<IResult> CheckEcosystem(
        HttpContext httpContext,
        IWopiHostExtensions extensions,
        CancellationToken cancellationToken)
    {
        var capabilities = new WopiHostCapabilities();
        var checkEcosystem = new WopiCheckEcosystem
        {
            SupportsContainers = capabilities.SupportsContainers,
        };

        // Mirrors OnCheckFileInfoAsync / OnCheckContainerInfoAsync — host customisation seam.
        checkEcosystem = await extensions.OnCheckEcosystemAsync(
            new WopiCheckEcosystemContext(httpContext.User, checkEcosystem), cancellationToken).ConfigureAwait(false);

        return TypedResults.Json(checkEcosystem);
    }

    private static async Task<IResult> GetRootContainer(
        HttpContext httpContext,
        IWopiStorageProvider storageProvider,
        IWopiAccessTokenService accessTokenService,
        IWopiPermissionProvider permissionProvider,
        ICheckContainerInfoBuilder checkContainerInfoBuilder,
        CancellationToken cancellationToken)
    {
        var root = await storageProvider.GetWopiContainer(storageProvider.RootContainer.Identifier, cancellationToken).ConfigureAwait(false);
        if (root is null) return TypedResults.NotFound();

        var permissions = await permissionProvider.GetContainerPermissionsAsync(httpContext.User, root, cancellationToken).ConfigureAwait(false);
        var token = await accessTokenService.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = httpContext.User.GetUserId(),
            UserDisplayName = httpContext.User.FindFirstValue(ClaimTypes.Name),
            UserEmail = httpContext.User.FindFirstValue(ClaimTypes.Email),
            ResourceId = root.Identifier,
            ResourceType = WopiResourceType.Container,
            ContainerPermissions = permissions,
        }, cancellationToken).ConfigureAwait(false);

        var url = httpContext.GetUrlHelper();
        var rc = new RootContainerInfo
        {
            ContainerPointer = new ChildContainer(root.Name, url.GetWopiSrc(root, token.Token)),
            // The spec strongly recommends including ContainerInfo so the WOPI client doesn't
            // have to round-trip back to CheckContainerInfo.
            ContainerInfo = await checkContainerInfoBuilder.BuildAsync(root, httpContext, cancellationToken).ConfigureAwait(false),
        };
        return TypedResults.Json(rc);
    }
}
