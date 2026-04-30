using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Results;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Controllers;

/// <summary>
/// Implementation of the WOPI Ecosystem endpoints.
/// Spec: <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/checkecosystem"/>.
/// </summary>
/// <param name="storageProvider">Storage provider instance for retrieving files and folders.</param>
/// <param name="accessTokenService">Issues per-resource WOPI access tokens.</param>
/// <param name="permissionProvider">Computes the permissions to bake into freshly issued tokens.</param>
/// <param name="wopiHostOptions">Host configuration, including the <see cref="WopiHostOptions.OnCheckEcosystem"/> hook.</param>
[Authorize]
[ApiController]
[Route("wopi/[controller]")]
[ServiceFilter(typeof(WopiOriginValidationActionFilter))]
public class EcosystemController(
    IWopiStorageProvider storageProvider,
    IWopiAccessTokenService accessTokenService,
    IWopiPermissionProvider permissionProvider,
    IOptions<WopiHostOptions> wopiHostOptions) : ControllerBase
{
    /// <summary>
    /// The GetRootContainer operation returns the root container. A WOPI client can use this
    /// operation to get a reference to the root container, from which it can call
    /// EnumerateChildren (containers) to navigate the container hierarchy.
    /// Spec: <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getrootcontainer"/>.
    /// Example URL: <c>GET /wopi/ecosystem/root_container_pointer</c>.
    /// </summary>
    [HttpGet("root_container_pointer")]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> GetRootContainer(CancellationToken cancellationToken = default)
    {
        var root = await storageProvider.GetWopiResource<IWopiFolder>(storageProvider.RootContainerPointer.Identifier, cancellationToken);
        if (root is null)
        {
            return NotFound();
        }

        // Issue a per-container access token. Re-using the inbound token in
        // ContainerPointer.Url violates the WOPI "preventing token trading" guidance:
        // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/concepts#preventing-token-trading
        var permissions = await permissionProvider.GetContainerPermissionsAsync(User, root, cancellationToken);
        var token = await accessTokenService.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = User.GetUserId(),
            UserDisplayName = User.FindFirstValue(ClaimTypes.Name),
            UserEmail = User.FindFirstValue(ClaimTypes.Email),
            ResourceId = root.Identifier,
            ResourceType = WopiResourceType.Container,
            ContainerPermissions = permissions,
        }, cancellationToken);

        var rc = new RootContainerInfo
        {
            ContainerPointer = new ChildContainer(
                root.Name,
                Url.GetWopiSrc(WopiResourceType.Container, root.Identifier, token.Token)),
            // The spec strongly recommends including ContainerInfo so the WOPI client
            // does not have to round-trip back to CheckContainerInfo.
            ContainerInfo = await root.GetWopiCheckContainerInfo(HttpContext, cancellationToken),
        };
        return new JsonResult(rc);
    }

    /// <summary>
    /// The GetFileWopiSrc operation converts a host-specific file identifier (passed via
    /// the <c>X-WOPI-HostNativeFileName</c> header) into a <c>WopiSrc</c> URL with an
    /// access token appended.
    /// Spec: <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getfilewopisrc"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Reserved for future use.</b> Microsoft's documentation states:
    /// <i>"This operation should not be called by WOPI clients at this time. It is reserved
    /// for future use and subject to change."</i> The endpoint is wired up here so the route
    /// matches the spec when Microsoft starts shipping clients that exercise it; until then
    /// the action returns <c>501 Not Implemented</c> (which the spec allows).
    /// </para>
    /// <para>
    /// The matching <see cref="IWopiHostCapabilities.SupportsGetFileWopiSrc"/> capability flag
    /// defaults to <c>false</c> so WOPI clients do not advertise the operation. When Microsoft
    /// un-reserves the operation, hosts can replace this stub by overriding the controller or
    /// by introducing an <c>OnGetFileWopiSrc</c> hook on <see cref="WopiHostOptions"/> and
    /// flipping <c>SupportsGetFileWopiSrc</c> to <c>true</c>.
    /// </para>
    /// </remarks>
    /// <param name="hostNativeFileName">The host-specific file identifier passed in <c>X-WOPI-HostNativeFileName</c>.</param>
    [HttpPost]
    [WopiOverrideHeader(WopiEcosystemOperations.GetWopiSrcWithAccessToken)]
    [Produces(MediaTypeNames.Application.Json)]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Experimental(WopiDiagnostics.GetFileWopiSrcReserved, UrlFormat = "https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getfilewopisrc")]
    public IActionResult GetFileWopiSrc(
        [FromHeader(Name = WopiHeaders.HOST_NATIVE_FILE_NAME)] string? hostNativeFileName = null)
    {
        _ = hostNativeFileName;
        return new NotImplementedResult();
    }

    /// <summary>
    /// The CheckEcosystem operation is similar to CheckFileInfo, but does not require a file
    /// or container ID.
    /// Spec: <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/checkecosystem"/>.
    /// </summary>
    [HttpGet(Name = WopiRouteNames.CheckEcosystem)]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> CheckEcosystem()
    {
        // Mirror the capability flags surfaced via CheckFileInfo. The spec says
        // SupportsContainers here "should match" the CheckFileInfo value.
        var capabilities = new WopiHostCapabilities();
        var checkEcosystem = new WopiCheckEcosystem
        {
            SupportsContainers = capabilities.SupportsContainers,
        };

        // Allow the host to override before returning, mirroring OnCheckFileInfo /
        // OnCheckContainerInfo / OnCheckFolderInfo.
        checkEcosystem = await wopiHostOptions.Value.OnCheckEcosystem(
            new WopiCheckEcosystemContext(User, checkEcosystem));

        return new JsonResult(checkEcosystem);
    }
}
