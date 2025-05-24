using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Controllers;

/// <summary>
/// Implementation of WOPI server protocol https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/checkecosystem
/// </summary>
/// <remarks>
/// Creates an instance of <see cref="EcosystemController"/>.
/// </remarks>
/// <param name="storageProvider">Storage provider instance for retrieving files and folders.</param>
[Authorize]
[ApiController]
[Route("wopi/[controller]")]
[ServiceFilter(typeof(WopiOriginValidationActionFilter))]
public class EcosystemController(
    IWopiStorageProvider storageProvider) : ControllerBase
{
    /// <summary>
    /// The GetRootContainer operation returns the root container. A WOPI client can use this operation to get a reference to the root container, from which the client can call EnumerateChildren (containers) to navigate a container hierarchy.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getrootcontainer
    /// Example URL: GET /wopi/ecosystem/root_container_pointer
    /// </summary>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns></returns>
    [HttpGet("root_container_pointer")]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> GetRootContainer(CancellationToken cancellationToken = default)
    {
        var root = await storageProvider.GetWopiResource<IWopiFolder>(storageProvider.RootContainerPointer.Identifier, cancellationToken)
            ?? throw new InvalidOperationException("Root container not found");
        var rc = new RootContainerInfo
        {
            ContainerPointer = new ChildContainer(root.Name, Url.GetWopiSrc(WopiResourceType.Container, root.Identifier))
        };
        return new JsonResult(rc);
    }

    /// <summary>
    /// The CheckEcosystem operation is similar to the the CheckFileInfo operation, but does not require a file or container ID.
    /// </summary>
    /// <returns></returns>
    [HttpGet(Name = WopiRouteNames.CheckEcosystem)]
    [Produces(MediaTypeNames.Application.Json)]
    public IActionResult CheckEcosystem()
    {
        return new JsonResult(new { new WopiHostCapabilities().SupportsContainers });
    }
}
