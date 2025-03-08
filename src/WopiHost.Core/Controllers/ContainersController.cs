using System.Globalization;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Results;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Controllers;

/// <summary>
/// Implementation of WOPI server protocol https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/c8185d20-77dc-445c-b830-c8332a9b5fc2
/// </summary>
/// <remarks>
/// Creates an instance of <see cref="ContainersController"/>.
/// </remarks>
/// <param name="storageProvider">Storage provider instance for retrieving files and folders.</param>
/// <param name="wopiHostOptions">WOPI Host configuration</param>
/// <param name="writableStorageProvider">Storage provider instance for writing files and folders.</param>
[Authorize]
[ApiController]
[Route("wopi/[controller]")]
public class ContainersController(
    IWopiStorageProvider storageProvider,
    IOptions<WopiHostOptions> wopiHostOptions,
    IWopiWritableStorageProvider? writableStorageProvider = null) 
    : ControllerBase
{
    /// <summary>
    /// Returns the metadata about a container specified by an identifier.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/checkcontainerinfo
    /// Example URL path: /wopi/containers/(container_id)
    /// </summary>
    /// <param name="id">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns></returns>
    [HttpGet("{id}", Name = WopiRouteNames.CheckContainerInfo)]
    [Produces(MediaTypeNames.Application.Json)]
    [WopiAuthorize(WopiResourceType.Container, Permission.Read,
        CheckPermissions = [Permission.Create, Permission.Delete, Permission.Rename, Permission.CreateChildFile])]
    public async Task<IActionResult> CheckContainerInfo(string id, CancellationToken cancellationToken = default)
    {
        var container = storageProvider.GetWopiContainer(id);
        if (container is null)
        {
            return NotFound();
        }
        var checkContainerInfo = new WopiCheckContainerInfo()
        {
            Name = container.Name,
            UserCanCreateChildContainer = HttpContext.IsPermitted(Permission.Create),
            UserCanDelete = HttpContext.IsPermitted(Permission.Delete),
            UserCanRename = HttpContext.IsPermitted(Permission.Rename),
            UserCanCreateChildFile = HttpContext.IsPermitted(Permission.CreateChildFile),
            IsEduUser = false,
        };

        // allow changes and/or extensions before returning 
        checkContainerInfo = await wopiHostOptions.Value.OnCheckContainerInfo(new WopiCheckContainerInfoContext(User, container, checkContainerInfo));

        return new JsonResult<WopiCheckContainerInfo>(checkContainerInfo);
    }

    /// <summary>
    /// The CreateChildContainer operation creates a new container as a child of the specified container.
    /// </summary>
    /// <param name="id">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <param name="suggestedTarget">A UTF-7 encoded string that specifies a full container name. Required.</param>
    /// <param name="relativeTarget">A UTF-7 encoded string that specifies a full container name. The host must not modify the name to fulfill the request.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns></returns>
    [HttpPost("{id}")]
    [WopiOverrideHeader(WopiContainerOperations.CreateChildContainer)]
    [WopiAuthorize(WopiResourceType.Container, Permission.Create)]
    public async Task<IActionResult> CreateChildContainer(
        string id,
        [FromHeader(Name = WopiHeaders.SUGGESTED_TARGET)] string? suggestedTarget = null,
        [FromHeader(Name = WopiHeaders.RELATIVE_TARGET)] string? relativeTarget = null,
        CancellationToken cancellationToken = default)
    {
        if (writableStorageProvider is null)
        {
            return new NotImplementedResult();
        }

        var container = storageProvider.GetWopiContainer(id);
        if (container is null)
        {
            return NotFound();
        }

        // the two headers are mutually exclusive. If both headers are present, the host should respond with a 501 Not Implemented status code.
        if (!string.IsNullOrWhiteSpace(suggestedTarget) &&
            !string.IsNullOrWhiteSpace(relativeTarget))
        {
            return new NotImplementedResult();
        }
        if (string.IsNullOrWhiteSpace(suggestedTarget) &&
            string.IsNullOrWhiteSpace(relativeTarget))
        {
            return new BadRequestResult();
        }

        var newIdentifier = await writableStorageProvider.CreateWopiChildContainer(id, (suggestedTarget ?? relativeTarget)!, relativeTarget is not null, cancellationToken);
        if (string.IsNullOrWhiteSpace(newIdentifier))
        {
            return new ConflictResult();
        }
        var checkContainerInfo = await CheckContainerInfo(newIdentifier, cancellationToken);
        if (checkContainerInfo is not JsonResult<WopiCheckContainerInfo> jsonResult ||
            jsonResult.Data is null)
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
        return new JsonResult(
            new CreateChildContainerResponse(
                new(jsonResult.Data.Name, Url.GetWopiUrl(WopiResourceType.Container, newIdentifier)),
                jsonResult.Data));
    }

    /// <summary>
    /// The DeleteContainer operation deletes a container.
    /// </summary>
    /// <param name="id">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns></returns>
    [HttpPost("{id}")]
    [WopiOverrideHeader(WopiContainerOperations.DeleteContainer)]
    [WopiAuthorize(WopiResourceType.Container, Permission.Delete)]
    public async Task<IActionResult> DeleteContainer(string id, CancellationToken cancellationToken = default)
    {
        if (writableStorageProvider is null)
        {
            return new NotImplementedResult();
        }
        var container = storageProvider.GetWopiContainer(id);
        if (container is null)
        {
            // 404 Not Found – Resource not found/user unauthorized
            return NotFound();
        }
        try
        {
            if (await writableStorageProvider.DeleteWopiContainer(id, cancellationToken))
            {
                return Ok();
            }
        }
        catch (DirectoryNotFoundException)
        {
            // 404 Not Found – Resource not found/user unauthorized
            return NotFound();
        }
        catch (InvalidOperationException)
        {
            // 409 Conflict – Container has child files/containers
            return new ConflictResult();
        }
        return StatusCode(StatusCodes.Status500InternalServerError);
    }

    /// <summary>
    /// The GetEcosystem operation returns the URI for the WOPI server’s Ecosystem endpoint, given a container ID.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/getecosystem
    /// Example URL path: /wopi/containers/(container_id)/ecosystem_pointer
    /// </summary>
    /// <param name="id">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <returns>URL response pointing to <see cref="WopiRouteNames.CheckEcosystem"/></returns>
    [HttpGet("{id}/ecosystem_pointer")]
    [WopiAuthorize(WopiResourceType.Container, Permission.Read)]
    [Produces(MediaTypeNames.Application.Json)]
    public IActionResult GetEcosystem(string id)
    {
        var container = storageProvider.GetWopiContainer(id);
        if (container is null)
        {
            // 404 Not Found – Resource not found/user unauthorized
            return NotFound();
        }
        // A URI for the WOPI server’s Ecosystem endpoint, with an access token appended. A GET request to this URL will invoke the CheckEcosystem operation.
        return new JsonResult<UrlResponse>(
            new(Url.GetWopiUrl(WopiRouteNames.CheckEcosystem)));
    }

    

    /// <summary>
    /// The EnumerateAncestors operation enumerates all the parents of a given container, up to and including the root container.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/enumerateancestors
    /// Example URL path: /wopi/containers/(container_id)/ancestry
    /// </summary>
    /// <param name="id">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns></returns>
    [HttpGet("{id}/ancestry")]
    [WopiAuthorize(WopiResourceType.Container, Permission.Read)]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> EnumerateAncestors(string id, CancellationToken cancellationToken = default)
    {
        var container = storageProvider.GetWopiContainer(id);
        if (container is null)
        {
            return NotFound();
        }

        var ancestors = await storageProvider.GetAncestors(WopiResourceType.Container, id, cancellationToken);
        return new JsonResult(
            new EnumerateAncestorsResponse(ancestors
                .Select(a => new ChildContainer(a.Name, Url.GetWopiUrl(WopiResourceType.Container, a.Identifier))
            )));
    }

    /// <summary>
    /// The EnumerateChildren method returns the contents of a container on the WOPI server.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/enumeratechildren
    /// Example URL path: /wopi/containers/(container_id)/children
    /// </summary>
    /// <param name="id">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <returns></returns>
    [HttpGet("{id}/children")]
    [WopiAuthorize(WopiResourceType.Container, Permission.Read)]
    [Produces(MediaTypeNames.Application.Json)]
    public Container EnumerateChildren(string id)
    {
        var container = new Container();
        var files = new List<ChildFile>();
        var containers = new List<ChildContainer>();

        foreach (var wopiFile in storageProvider.GetWopiFiles(id))
        {
            files.Add(new ChildFile(wopiFile.Name, Url.GetWopiUrl(WopiResourceType.File, wopiFile.Identifier))
            {
                LastModifiedTime = wopiFile.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture),
                Size = wopiFile.Size,
                Version = wopiFile.Version ?? wopiFile.LastWriteTimeUtc.ToString("s", CultureInfo.InvariantCulture)
            });
        }

        foreach (var wopiContainer in storageProvider.GetWopiContainers(id))
        {
            containers.Add(
                new ChildContainer(wopiContainer.Name, Url.GetWopiUrl(WopiResourceType.Container, wopiContainer.Identifier)));
        }

        container.ChildFiles = files;
        container.ChildContainers = containers;

        return container;
    }
}
