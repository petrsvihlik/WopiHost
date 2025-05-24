using System.Globalization;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Results;
using WopiHost.Core.Security.Authentication;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Controllers;

/// <summary>
/// Implementation of WOPI server protocol https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/c8185d20-77dc-445c-b830-c8332a9b5fc2
/// </summary>
/// <remarks>
/// Creates an instance of <see cref="ContainersController"/>.
/// </remarks>
/// <param name="storageProvider">Storage provider instance for retrieving files and folders.</param>
/// <param name="lockProvider">An instance of the lock provider.</param>
/// <param name="writableStorageProvider">Storage provider instance for writing files and folders.</param>
[Authorize]
[ApiController]
[Route("wopi/[controller]")]
[ServiceFilter(typeof(WopiOriginValidationActionFilter))]
public class ContainersController(
    IWopiStorageProvider storageProvider,
    IWopiLockProvider? lockProvider = null,
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
        var container = await storageProvider.GetWopiResource<IWopiFolder>(id, cancellationToken);
        if (container is null)
        {
            return NotFound();
        }
        var checkContainerInfo = await container.GetWopiCheckContainerInfo(HttpContext);
        return new JsonResult<WopiCheckContainerInfo>(checkContainerInfo);
    }

    /// <summary>
    /// The CreateChildContainer operation creates a new container as a child of the specified container.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/createchildcontainer
    /// Example URL path: /wopi/containers/(container_id)
    /// </summary>
    /// <param name="id">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <param name="suggestedTarget">A UTF-7 encoded string that specifies a full container name. Required.</param>
    /// <param name="relativeTarget">A UTF-7 encoded string that specifies a full container name. The host must not modify the name to fulfill the request.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns></returns>
    [HttpPost("{id}")]
    [Produces(MediaTypeNames.Application.Json)]
    [WopiOverrideHeader(WopiContainerOperations.CreateChildContainer)]
    [WopiAuthorize(WopiResourceType.Container, Permission.Create)]
    public async Task<IActionResult> CreateChildContainer(
        string id,
        [FromHeader(Name = WopiHeaders.SUGGESTED_TARGET)] UtfString? suggestedTarget = null,
        [FromHeader(Name = WopiHeaders.RELATIVE_TARGET)] UtfString? relativeTarget = null,
        CancellationToken cancellationToken = default)
    {
        if (writableStorageProvider is null)
        {
            return new NotImplementedResult();
        }

        if (await storageProvider.GetWopiResource<IWopiFolder>(id, cancellationToken) is null)
        {
            return NotFound();
        }

        // the two headers are mutually exclusive. If both headers are present (or missing), the host should respond with a 501 Not Implemented status code.
        if ((!string.IsNullOrWhiteSpace(suggestedTarget) && !string.IsNullOrWhiteSpace(relativeTarget)) ||
            (string.IsNullOrWhiteSpace(suggestedTarget) && string.IsNullOrWhiteSpace(relativeTarget)))
        {
            return new NotImplementedResult();
        }
        // If the specified name is illegal, the host must respond with a 400 Bad Request.
        if (!await writableStorageProvider.CheckValidName<IWopiFolder>((suggestedTarget ?? relativeTarget)!, cancellationToken))
        {
            return new BadRequestResult();
        }

        IWopiFolder? newFolder;

        // "specific mode" - The host must not modify the name to fulfill the request.
        if (!string.IsNullOrWhiteSpace(relativeTarget))
        {
            newFolder = await storageProvider.GetWopiResourceByName<IWopiFolder>(id, relativeTarget, cancellationToken);
            // If a container with the specified name already exists
            if (newFolder is not null)
            {
                // the host may include an X-WOPI-ValidRelativeTarget specifying a container name that is valid
                var suggestedName = await writableStorageProvider.GetSuggestedName<IWopiFolder>(id, relativeTarget, cancellationToken);
                Response.Headers[WopiHeaders.VALID_RELATIVE_TARGET] = UtfString.FromDecoded(suggestedName).ToString(true);
                // the host must respond with a 409 Conflict
                return new ConflictResult();
            }
            else
            {
                newFolder = await writableStorageProvider.CreateWopiChildResource<IWopiFolder>(id, relativeTarget, cancellationToken);
            }
        }
        else if (!string.IsNullOrWhiteSpace(suggestedTarget))
        {
            var newName = await writableStorageProvider.GetSuggestedName<IWopiFolder>(id, suggestedTarget, cancellationToken);
            newFolder = await writableStorageProvider.CreateWopiChildResource<IWopiFolder>(id, newName, cancellationToken);
        }
        else
        {
            return new BadRequestResult();
        }

        if (newFolder is not null)
        {
            var checkContainerInfo = await newFolder.GetWopiCheckContainerInfo(HttpContext);
            return new JsonResult(
                new CreateChildContainerResponse(
                    new(newFolder.Name, Url.GetWopiSrc(WopiResourceType.Container, newFolder.Identifier)),
                    checkContainerInfo));
        }

        return new InternalServerErrorResult();
    }

    /// <summary>
    /// The CreateChildFile operation creates a new file in the provided container.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/createchildfile
    /// Example URL path: /wopi/containers/(container_id)
    /// </summary>
    /// <param name="id">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <param name="suggestedTarget">A UTF-7 encoded string specifying either a file extension or a full file name, including the file extension</param>
    /// <param name="relativeTarget">A UTF-7 encoded string that specifies a full file name including the file extension. The host must not modify the name to fulfill the request.</param>
    /// <param name="overwriteRelativeTarget">A Boolean value that specifies whether the host must overwrite the file name if it exists. The default value is false.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns></returns>
    [HttpPost("{id}")]
    [Produces(MediaTypeNames.Application.Json)]
    [WopiOverrideHeader(WopiContainerOperations.CreateChildFile)]
    [WopiAuthorize(WopiResourceType.Container, Permission.CreateChildFile)]
    public async Task<IActionResult> CreateChildFile(
        string id,
        [FromHeader(Name = WopiHeaders.SUGGESTED_TARGET)] UtfString? suggestedTarget = null,
        [FromHeader(Name = WopiHeaders.RELATIVE_TARGET)] UtfString? relativeTarget = null,
        [FromHeader(Name = WopiHeaders.OVERWRITE_RELATIVE_TARGET)] bool? overwriteRelativeTarget = false,
        CancellationToken cancellationToken = default)
    {
        if (writableStorageProvider is null)
        {
            return new NotImplementedResult();
        }

        var container = await storageProvider.GetWopiResource<IWopiFolder>(id, cancellationToken);
        if (container is null)
        {
            return NotFound();
        }

        // the two headers are mutually exclusive. If both headers are present (or missing), the host should respond with a 501 Not Implemented status code.
        if ((!string.IsNullOrWhiteSpace(suggestedTarget) && !string.IsNullOrWhiteSpace(relativeTarget)) ||
            (string.IsNullOrWhiteSpace(suggestedTarget) && string.IsNullOrWhiteSpace(relativeTarget)))
        {
            return new NotImplementedResult();
        }

        IWopiFile? newFile;

        // "specific mode" - The host must not modify the name to fulfill the request.
        if (!string.IsNullOrWhiteSpace(relativeTarget))
        {
            // If the specified name is illegal, the host must respond with a 400 Bad Request.
            if (!await writableStorageProvider.CheckValidName<IWopiFile>(relativeTarget, cancellationToken))
            {
                return new BadRequestResult();
            }

            // check if such file already exists
            newFile = await storageProvider.GetWopiResourceByName<IWopiFile>(id, relativeTarget, cancellationToken);

            // If a file with the specified name already exists
            if (newFile is not null)
            {
                // unless the X-WOPI-OverwriteRelativeTarget request header is set to true...
                if (overwriteRelativeTarget == false)
                {
                    // the host might include an X-WOPI-ValidRelativeTarget specifying a file name that's valid
                    var suggestedName = await writableStorageProvider.GetSuggestedName<IWopiFile>(id, relativeTarget, cancellationToken);
                    Response.Headers[WopiHeaders.VALID_RELATIVE_TARGET] = UtfString.FromDecoded(suggestedName).ToString(true);
                    // the host must respond with a 409 Conflict
                    return new ConflictResult();
                }
                else
                {
                    // a file matching the target name might be locked
                    if (lockProvider?.TryGetLock(newFile.Identifier, out var existingLock) == true)
                    {
                        // the host must respond with a 409 Conflict and include a X-WOPI-Lock response header
                        return new LockMismatchResult(Response, existingLock.LockId, reason: "File already exists and is currently locked");
                    }
                }
            }
            else
            {
                newFile = await writableStorageProvider.CreateWopiChildResource<IWopiFile>(
                    container.Identifier,
                    relativeTarget,
                    cancellationToken);
            }
        }
        else if (!string.IsNullOrWhiteSpace(suggestedTarget))
        {
            var suggestedTargetString = suggestedTarget.ToString()!;
            // If only the extension is provided, the name of the initial file without extension should be combined with the extension to create the proposed name
            if (suggestedTargetString.StartsWith(".", StringComparison.OrdinalIgnoreCase))
            {
                suggestedTargetString = Guid.NewGuid().ToString("N") + suggestedTargetString;
            }
            // If the specified name is illegal, the host must respond with a 400 Bad Request.
            else if (!await writableStorageProvider.CheckValidName<IWopiFile>(suggestedTargetString, cancellationToken))
            {
                return new BadRequestResult();
            }

            var newName = await writableStorageProvider.GetSuggestedName<IWopiFile>(container.Identifier, suggestedTargetString, cancellationToken);
            newFile = await writableStorageProvider.CreateWopiChildResource<IWopiFile>(
                container.Identifier,
                newName,
                cancellationToken);
        }
        else
        {
            // the two headers are mutually exclusive.
            // If neither header is present, we return BadRequest
            return new BadRequestResult();
        }

        if (newFile is not null)
        {
            var checkFileInfo = await newFile.GetWopiCheckFileInfo(HttpContext, cancellationToken: cancellationToken);
            return new JsonResult(
                new ChildFile(
                    newFile.Name + '.' + newFile.Extension,
                    Url.GetWopiSrc(WopiResourceType.File, newFile.Identifier))
                {
                    HostEditUrl = checkFileInfo.HostEditUrl?.ToString(),
                    HostViewUrl = checkFileInfo.HostViewUrl?.ToString()
                });
        }

        return new InternalServerErrorResult();
    }

    /// <summary>
    /// The DeleteContainer operation deletes a container.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/deletecontainer
    /// Example URL path: /wopi/containers/(container_id)
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
        if (await storageProvider.GetWopiResource<IWopiFolder>(id, cancellationToken) is null)
        {
            return NotFound();
        }
        try
        {
            if (await writableStorageProvider.DeleteWopiResource<IWopiFolder>(id, cancellationToken))
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
        return new InternalServerErrorResult();
    }

    /// <summary>
    /// The RenameContainer operation renames a container.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/renamecontainer
    /// Example URL path: /wopi/containers/(container_id)
    /// </summary>
    /// <param name="id">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <param name="requestedName">A UTF-7 encoded string that is a container name. Required.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns></returns>
    [HttpPost("{id}")]
    [Produces(MediaTypeNames.Application.Json)]
    [WopiOverrideHeader(WopiContainerOperations.RenameContainer)]
    [WopiAuthorize(WopiResourceType.Container, Permission.Rename)]
    public async Task<IActionResult> RenameContainer(
        string id,
        [FromHeader(Name = WopiHeaders.REQUESTED_NAME)] UtfString requestedName,
        CancellationToken cancellationToken = default)
    {
        if (writableStorageProvider is null)
        {
            return new NotImplementedResult();
        }
        var container = await storageProvider.GetWopiResource<IWopiFolder>(id, cancellationToken);
        if (container is null)
        {
            // 404 Not Found – Resource not found/user unauthorized
            return NotFound();
        }
        if (!await writableStorageProvider.CheckValidName<IWopiFolder>(requestedName, cancellationToken))
        {
            // 400 Bad Request – Specified name is illegal
            // A string describing the reason the rename operation couldn't be completed.
            // This header should only be included when the response code is 400 Bad Request
            Response.Headers[WopiHeaders.INVALID_CONTAINER_NAME] = "Specified name is illegal";
            return new BadRequestResult();
        }
        try
        {
            if (await writableStorageProvider.RenameWopiResource<IWopiFolder>(id, requestedName, cancellationToken))
            {
                // The response to a RenameContainer call is JSON containing the following required property:
                // Name(string) - The name of the renamed container.
                return new JsonResult(new { container.Name });
            }
        }
        catch (ArgumentException ae) when (ae.ParamName == nameof(requestedName))
        {
            // 400 Bad Request – Specified name is illegal
            // A string describing the reason the RenameContainer operation could not be completed.
            // This header should only be included when the response code is 400 Bad Request.
            // This string is only used for logging purposes.
            Response.Headers[WopiHeaders.INVALID_CONTAINER_NAME] = "Specified name is illegal";
            return new BadRequestResult();
        }
        catch (DirectoryNotFoundException)
        {
            // 404 Not Found – Resource not found/user unauthorized
            return NotFound();
        }
        catch (InvalidOperationException)
        {
            // 409 Conflict – requestedName already exists
            return new ConflictResult();
        }
        catch (Exception)
        {
            return new InternalServerErrorResult();
        }
        return new InternalServerErrorResult();
    }

    /// <summary>
    /// The GetEcosystem operation returns the URI for the WOPI server’s Ecosystem endpoint, given a container ID.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/getecosystem
    /// Example URL path: /wopi/containers/(container_id)/ecosystem_pointer
    /// </summary>
    /// <param name="id">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>URL response pointing to <see cref="WopiRouteNames.CheckEcosystem"/></returns>
    [HttpGet("{id}/ecosystem_pointer")]
    [WopiAuthorize(WopiResourceType.Container, Permission.Read)]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> GetEcosystem(string id, CancellationToken cancellationToken = default)
    {
        if (await storageProvider.GetWopiResource<IWopiFolder>(id, cancellationToken) is null)
        {
            return NotFound();
        }
        // A URI for the WOPI server’s Ecosystem endpoint, with an access token appended. A GET request to this URL will invoke the CheckEcosystem operation.
        return new JsonResult<UrlResponse>(
            new(Url.GetWopiSrc(WopiRouteNames.CheckEcosystem)));
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
        if (await storageProvider.GetWopiResource<IWopiFolder>(id, cancellationToken) is null)
        {
            return NotFound();
        }

        var ancestors = await storageProvider.GetAncestors<IWopiFolder>(id, cancellationToken);
        return new JsonResult(
            new EnumerateAncestorsResponse(ancestors
                .Select(a => new ChildContainer(a.Name, Url.GetWopiSrc(WopiResourceType.Container, a.Identifier))
            )));
    }

    /// <summary>
    /// The EnumerateChildren method returns the contents of a container on the WOPI server.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/enumeratechildren
    /// Example URL path: /wopi/containers/(container_id)/children
    /// </summary>
    /// <param name="id">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <param name="fileExtensionFilterList">A string value that the host must use to filter the returned child files. 
    /// This header must be a list of comma-separated file extensions with a leading dot (.). 
    /// There must be no whitespace and no trailing comma in the string. 
    /// Wildcard characters are not permitted.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/enumeratechildren#required-response-properties</returns>
    [HttpGet("{id}/children")]
    [WopiAuthorize(WopiResourceType.Container, Permission.Read)]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> EnumerateChildren(
        string id,
        [FromHeader(Name = WopiHeaders.FILE_EXTENSION_FILTER_LIST)] string? fileExtensionFilterList = null,
        CancellationToken cancellationToken = default)
    {
        if (await storageProvider.GetWopiResource<IWopiFolder>(id, cancellationToken) is null)
        {
            return NotFound();
        }

        var files = new List<ChildFile>();
        var containers = new List<ChildContainer>();
        var fileExtensions = fileExtensionFilterList?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        await foreach (var wopiFile in storageProvider.GetWopiFiles(id, cancellationToken: cancellationToken))
        {
            // If included, the host must only return child files whose file extensions match the filter list, based on a case-insensitive match.
            if (fileExtensions?.Length > 0 && !fileExtensions.Contains('.' + wopiFile.Extension, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }
            files.Add(new ChildFile(wopiFile.Name + '.' + wopiFile.Extension, Url.GetWopiSrc(WopiResourceType.File, wopiFile.Identifier))
            {
                LastModifiedTime = wopiFile.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture),
                Size = wopiFile.Size,
                Version = wopiFile.Version ?? wopiFile.LastWriteTimeUtc.ToString("s", CultureInfo.InvariantCulture)
            });
        }

        await foreach (var wopiContainer in storageProvider.GetWopiContainers(id, cancellationToken))
        {
            containers.Add(
                new ChildContainer(wopiContainer.Name, Url.GetWopiSrc(WopiResourceType.Container, wopiContainer.Identifier)));
        }

        var container = new Container
        {
            ChildFiles = files,
            ChildContainers = containers
        };
        return new JsonResult(container);
    }
}
