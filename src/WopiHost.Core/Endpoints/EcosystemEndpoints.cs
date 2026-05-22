using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;

namespace WopiHost.Core.Endpoints;

/// <summary>
/// WOPI ecosystem-pointer endpoints. The endpoints do not carry per-resource
/// <see cref="Security.Authorization.WopiAuthorizeAttribute"/> requirements — only the
/// standard authenticated-user gate from the parent group's <c>RequireAuthorization()</c>.
/// </summary>
internal static class EcosystemEndpoints
{
    public static void MapEcosystemEndpoints(IEndpointRouteBuilder wopi)
    {
        // Two endpoints, no shared metadata — register directly. MapGroup("/ecosystem") +
        // MapGet("") normalises to "/ecosystem/" (trailing slash), which would break
        // WopiRouteNames.CheckEcosystem lookups. See MapGroupEmptyTemplateTests.
        wopi.MapGet("/ecosystem", CheckEcosystem)
            .WithName(WopiRouteNames.CheckEcosystem)
            .WithTags("Ecosystem")
            .WithSummary("CheckEcosystem — returns ecosystem capabilities.")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/checkecosystem. " +
                "The host customisation hook (OnCheckEcosystemAsync) on IWopiHostExtensions can tweak the returned capabilities before they're emitted.");

        wopi.MapGet("/ecosystem/root_container_pointer", GetRootContainer)
            .WithTags("Ecosystem")
            .WithSummary("Returns a pointer (URL + token) to the WOPI root container.")
            .WithDescription("Spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getrootcontainer. " +
                "Includes the root container's CheckContainerInfo payload to save the WOPI client a round-trip back to /wopi/containers/{id}.");
    }

    private static async Task<JsonHttpResult<WopiCheckEcosystem>> CheckEcosystem(
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

    private static async Task<Results<NotFound, JsonHttpResult<RootContainerInfo>>> GetRootContainer(
        [AsParameters] GetRootContainerRequest req)
    {
        var root = await req.Storage.GetWopiContainer(req.Storage.RootContainer.Identifier, req.CancellationToken).ConfigureAwait(false);
        if (root is null) return TypedResults.NotFound();

        var permissions = await req.PermissionProvider.GetContainerPermissionsAsync(req.Http.User, root, req.CancellationToken).ConfigureAwait(false);
        var token = await req.AccessTokenService.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = req.Http.User.GetUserId(),
            UserDisplayName = req.Http.User.FindFirstValue(ClaimTypes.Name),
            UserEmail = req.Http.User.FindFirstValue(ClaimTypes.Email),
            ResourceId = root.Identifier,
            ResourceType = WopiResourceType.Container,
            ContainerPermissions = permissions,
        }, req.CancellationToken).ConfigureAwait(false);

        var rc = new RootContainerInfo
        {
            ContainerPointer = new ChildContainer(root.Name, req.Http.GetWopiSrc(root, token.Token)),
            // The spec strongly recommends including ContainerInfo so the WOPI client doesn't
            // have to round-trip back to CheckContainerInfo.
            ContainerInfo = await req.ContainerInfoBuilder.BuildAsync(root, req.Http.User, req.CancellationToken).ConfigureAwait(false),
        };
        return TypedResults.Json(rc);
    }
}

/// <summary>Parameter bundle for <see cref="EcosystemEndpoints.GetRootContainer"/>.</summary>
internal readonly record struct GetRootContainerRequest(
    HttpContext Http,
    IWopiStorageProvider Storage,
    IWopiAccessTokenService AccessTokenService,
    IWopiPermissionProvider PermissionProvider,
    ICheckContainerInfoBuilder ContainerInfoBuilder,
    CancellationToken CancellationToken);
