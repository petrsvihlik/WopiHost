using System.Globalization;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Models;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Controllers;

/// <summary>
/// Implementation of WOPI server protocol https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/c8185d20-77dc-445c-b830-c8332a9b5fc2
/// </summary>
/// <remarks>
/// Creates an instance of <see cref="ContainersController"/>.
/// </remarks>
/// <param name="storageProvider">Storage provider instance for retrieving files and folders.</param>
/// <param name="securityHandler">Security handler instance for performing security-related operations.</param>
/// <param name="wopiHostOptions">WOPI Host configuration</param>
[Route("wopi/[controller]")]
public class ContainersController(
    IWopiStorageProvider storageProvider,
    IWopiSecurityHandler securityHandler,
    IOptions<WopiHostOptions> wopiHostOptions) : WopiControllerBase(storageProvider, securityHandler, wopiHostOptions)
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
    [WopiAuthorize(WopiResourceType.Container, Permission.Create)]
    [WopiAuthorize(WopiResourceType.Container, Permission.Delete)]
    [WopiAuthorize(WopiResourceType.Container, Permission.Read)]
    /// The EnumerateChildren method returns the contents of a container on the WOPI server.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/enumeratechildren
    /// Example URL path: /wopi/containers/(container_id)/children
    /// </summary>
    /// <param name="id">Container identifier.</param>
    /// <returns></returns>
    [HttpGet("{id}/children")]
    [WopiAuthorize(WopiResourceType.Container, Permission.Read)]
    [Produces(MediaTypeNames.Application.Json)]
    public Container EnumerateChildren(string id)
    {
        var container = new Container();
        var files = new List<ChildFile>();
        var containers = new List<ChildContainer>();

        foreach (var wopiFile in StorageProvider.GetWopiFiles(id))
        {
            files.Add(new ChildFile(wopiFile.Name, Url.GetWopiUrl(WopiResourceType.File, wopiFile.Identifier))
            {
                Name = wopiFile.Name,
                Url = GetWopiUrl("files", wopiFile.Identifier, AccessToken),
                LastModifiedTime = wopiFile.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture),
                Size = wopiFile.Size,
                Version = wopiFile.Version ?? wopiFile.LastWriteTimeUtc.ToString("s", CultureInfo.InvariantCulture)
            });
        }

        foreach (var wopiContainer in StorageProvider.GetWopiContainers(id))
        {
            containers.Add(new ChildContainer
            {
            containers.Add(
                new ChildContainer(wopiContainer.Name, Url.GetWopiUrl(WopiResourceType.Container, wopiContainer.Identifier)));
        }

        container.ChildFiles = files;
        container.ChildContainers = containers;

        return container;
    }
}
