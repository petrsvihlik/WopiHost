using System.Globalization;
using System.Net.Mime;
using System.Security.Claims;
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
/// <param name="checkContainerInfoBuilder">Builds the <see cref="WopiCheckContainerInfo"/> response
/// and fires the configured <see cref="IWopiHostExtensions.OnCheckContainerInfoAsync"/> hook.</param>
/// <param name="checkFileInfoBuilder">Builds <see cref="WopiCheckFileInfo"/> responses for newly
/// created child files in <see cref="CreateChildFile"/>.</param>
/// <param name="newChildFileNegotiator">Runs the WOPI suggested-target / relative-target /
/// overwrite-relative-target name-negotiation protocol shared with <see cref="FilesController.PutRelativeFile"/>.
/// The negotiator owns the lock probe on the overwrite path; the controller no longer takes
/// <see cref="IWopiLockProvider"/> directly. Delete / Rename paths rely on
/// <see cref="WopiLockAwareWritableStorageProvider"/> via the writable storage decorator.</param>
/// <param name="writableStorageProvider">Storage provider instance for writing files and folders.</param>
[Authorize]
[ApiController]
[Route("wopi/[controller]")]
[ServiceFilter(typeof(WopiOriginValidationActionFilter))]
[ServiceFilter(typeof(WopiTelemetryActionFilter))]
public class ContainersController(
    IWopiStorageProvider storageProvider,
    ICheckContainerInfoBuilder checkContainerInfoBuilder,
    ICheckFileInfoBuilder checkFileInfoBuilder,
    IWopiNewChildFileNegotiator newChildFileNegotiator,
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
    [WopiAuthorize(WopiResourceType.Container, Permission.Read)]
    public async Task<IActionResult> CheckContainerInfo(string id, CancellationToken cancellationToken = default)
    {
        var container = await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false);
        if (container is null)
        {
            return NotFound();
        }
        var checkContainerInfo = await checkContainerInfoBuilder.BuildAsync(container, HttpContext, cancellationToken).ConfigureAwait(false);
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
    [RequiresWritableStorage]
    public async Task<IActionResult> CreateChildContainer(
        string id,
        [FromHeader(Name = WopiHeaders.SUGGESTED_TARGET)] UtfString? suggestedTarget = null,
        [FromHeader(Name = WopiHeaders.RELATIVE_TARGET)] UtfString? relativeTarget = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writableStorageProvider);

        if (await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false) is null)
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
        if (!await writableStorageProvider.CheckValidContainerName((suggestedTarget ?? relativeTarget)!, cancellationToken).ConfigureAwait(false))
        {
            return new BadRequestResult();
        }

        IWopiContainer? newFolder;

        // "specific mode" - The host must not modify the name to fulfill the request.
        if (!string.IsNullOrWhiteSpace(relativeTarget))
        {
            newFolder = await storageProvider.GetWopiContainerByName(id, relativeTarget, cancellationToken).ConfigureAwait(false);
            // If a container with the specified name already exists
            if (newFolder is not null)
            {
                // the host may include an X-WOPI-ValidRelativeTarget specifying a container name that is valid
                var suggestedName = await writableStorageProvider.GetSuggestedContainerName(id, relativeTarget, cancellationToken).ConfigureAwait(false);
                Response.Headers[WopiHeaders.VALID_RELATIVE_TARGET] = UtfString.FromDecoded(suggestedName).ToString(true);
                // the host must respond with a 409 Conflict
                return new ConflictResult();
            }
            else
            {
                newFolder = await writableStorageProvider.CreateWopiChildContainer(id, relativeTarget, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (!string.IsNullOrWhiteSpace(suggestedTarget))
        {
            var newName = await writableStorageProvider.GetSuggestedContainerName(id, suggestedTarget, cancellationToken).ConfigureAwait(false);
            newFolder = await writableStorageProvider.CreateWopiChildContainer(id, newName, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            return new BadRequestResult();
        }

        if (newFolder is not null)
        {
            var checkContainerInfo = await checkContainerInfoBuilder.BuildAsync(newFolder, HttpContext, cancellationToken).ConfigureAwait(false);
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
    [RequiresWritableStorage]
    public async Task<IActionResult> CreateChildFile(
        string id,
        [FromHeader(Name = WopiHeaders.SUGGESTED_TARGET)] UtfString? suggestedTarget = null,
        [FromHeader(Name = WopiHeaders.RELATIVE_TARGET)] UtfString? relativeTarget = null,
        [FromHeader(Name = WopiHeaders.OVERWRITE_RELATIVE_TARGET)] bool? overwriteRelativeTarget = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writableStorageProvider);

        var container = await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false);
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

        // Suggested-target / relative-target negotiation — protocol shared with PutRelativeFile.
        // CreateChildFile has no source file in scope, so its extension-only fallback uses a fresh GUID.
        var negotiation = await newChildFileNegotiator.NegotiateAsync(new WopiNewChildFileRequest(
            ContainerId: container.Identifier,
            SuggestedTarget: suggestedTarget,
            RelativeTarget: relativeTarget,
            OverwriteRelativeTarget: overwriteRelativeTarget ?? false,
            SuggestedExtensionFallbackStem: Guid.NewGuid().ToString("N")), cancellationToken).ConfigureAwait(false);
        if (negotiation.ToErrorActionResult(Response) is { } error)
        {
            return error;
        }

        var newFile = negotiation.File!;
        var checkFileInfo = await checkFileInfoBuilder.BuildAsync(newFile, HttpContext, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new JsonResult(
            new ChildFile(
                newFile.Name + '.' + newFile.Extension,
                Url.GetWopiSrc(WopiResourceType.File, newFile.Identifier))
            {
                HostEditUrl = checkFileInfo.HostEditUrl,
                HostViewUrl = checkFileInfo.HostViewUrl,
            });
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
    [RequiresWritableStorage]
    public async Task<IActionResult> DeleteContainer(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writableStorageProvider);
        if (await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false) is null)
        {
            return NotFound();
        }
        try
        {
            if (await writableStorageProvider.DeleteWopiContainer(id, cancellationToken).ConfigureAwait(false))
            {
                return Ok();
            }
            // Provider returns false when the identifier no longer resolves to a resource — the
            // pre-check above passed but a concurrent delete won the race. Map to 404, same as
            // the throw-based path below (#380 item 4.2).
            return NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            // 404 Not Found – defensive catch for third-party providers that still throw on
            // missing resource (the in-tree providers now return false; see item 4.2).
            return NotFound();
        }
        catch (InvalidOperationException)
        {
            // 409 Conflict – Container has child files/containers
            return new ConflictResult();
        }
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
    [RequiresWritableStorage]
    public async Task<IActionResult> RenameContainer(
        string id,
        [FromHeader(Name = WopiHeaders.REQUESTED_NAME)] UtfString requestedName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writableStorageProvider);
        var container = await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false);
        IActionResult result;
        if (container is null)
        {
            // 404 Not Found – Resource not found/user unauthorized
            result = NotFound();
        }
        else if (!await writableStorageProvider.CheckValidContainerName(requestedName, cancellationToken).ConfigureAwait(false))
        {
            // 400 Bad Request – Specified name is illegal
            // A string describing the reason the rename operation couldn't be completed.
            // This header should only be included when the response code is 400 Bad Request
            Response.Headers[WopiHeaders.INVALID_CONTAINER_NAME] = "Specified name is illegal";
            result = new BadRequestResult();
        }
        else
        {
            result = await TryRenameContainerAsync(id, requestedName, container, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    /// <summary>
    /// Inner body of <see cref="RenameContainer"/>: actually attempts the rename and maps each
    /// provider outcome (success / false / typed exceptions) to its WOPI HTTP status. Lifted out
    /// of the public action method so the action's return-statement count stays below qlty's
    /// complexity threshold; pre-fix the inline version had 8 returns scattered across the
    /// pre-checks, the try block, and the catch arms.
    /// </summary>
    private async Task<IActionResult> TryRenameContainerAsync(
        string id,
        string requestedName,
        IWopiContainer container,
        CancellationToken cancellationToken)
    {
        // Result-variable pattern: each branch assigns to a single `result` so the method has
        // just one `return`. The pre-#380-item-5.6 version had a `catch (Exception) { result =
        // new InternalServerErrorResult(); }` at the end — removed because it was actively
        // harmful: ASP.NET Core's framework exception middleware already converts uncaught
        // exceptions to 500 with proper ILogger + OpenTelemetry context (and DeveloperExceptionPage
        // in dev / ProblemDetails JSON in prod), and catching them here swallowed the stack
        // trace, broke telemetry-tag correlation, and silently absorbed OperationCanceledException.
        // The wire behavior the WOPI client sees is identical — 500 with no body either way —
        // but the operational side is strictly better when we let unexpected exceptions bubble.
        IActionResult result;
        try
        {
            if (await writableStorageProvider!.RenameWopiContainer(id, requestedName, cancellationToken).ConfigureAwait(false))
            {
                // The response to a RenameContainer call is JSON containing the following required property:
                // Name(string) - The name of the renamed container.
                result = new JsonResult(new { container.Name });
            }
            else
            {
                // false → missing resource (race with concurrent delete). Map to 404. (#380 item 4.2)
                result = NotFound();
            }
        }
        catch (ArgumentException ae) when (ae.ParamName == nameof(requestedName))
        {
            // 400 Bad Request – Specified name is illegal
            // A string describing the reason the RenameContainer operation could not be completed.
            // This header should only be included when the response code is 400 Bad Request.
            // This string is only used for logging purposes.
            Response.Headers[WopiHeaders.INVALID_CONTAINER_NAME] = "Specified name is illegal";
            result = new BadRequestResult();
        }
        catch (DirectoryNotFoundException)
        {
            // 404 Not Found – defensive catch for third-party providers that still throw.
            result = NotFound();
        }
        catch (InvalidOperationException)
        {
            // 409 Conflict – requestedName already exists
            result = new ConflictResult();
        }
        return result;
    }

    /// <summary>
    /// The GetEcosystem operation returns the URI for the WOPI server’s Ecosystem endpoint, given a container ID.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/getecosystem
    /// Example URL path: /wopi/containers/(container_id)/ecosystem_pointer
    /// </summary>
    /// <param name="id">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <param name="accessTokenService">Issues the per-call access token embedded in the response URL.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>URL response pointing to <see cref="WopiRouteNames.CheckEcosystem"/></returns>
    [HttpGet("{id}/ecosystem_pointer")]
    [WopiAuthorize(WopiResourceType.Container, Permission.Read)]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> GetEcosystem(
        string id,
        [FromServices] IWopiAccessTokenService accessTokenService,
        CancellationToken cancellationToken = default)
    {
        var container = await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false);
        if (container is null)
        {
            return NotFound();
        }

        // Issue a fresh, minimum-privilege access token to embed in the response URL.
        // Reusing the inbound token violates WOPI "preventing token trading" guidance:
        // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/concepts#preventing-token-trading
        // The URL points to CheckEcosystem, which has no resource gate — so the token
        // grants WopiContainerPermissions.None and is bound to this container purely as
        // the resource binding required by the access-token model.
        var token = await accessTokenService.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = User.GetUserId(),
            UserDisplayName = User.FindFirstValue(ClaimTypes.Name),
            UserEmail = User.FindFirstValue(ClaimTypes.Email),
            ResourceId = container.Identifier,
            ResourceType = WopiResourceType.Container,
            ContainerPermissions = WopiContainerPermissions.None,
        }, cancellationToken).ConfigureAwait(false);

        return new JsonResult<UrlResponse>(
            new(Url.GetWopiSrc(WopiRouteNames.CheckEcosystem, identifier: null, accessToken: token.Token)));
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
        if (await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false) is null)
        {
            return NotFound();
        }

        var ancestors = await storageProvider.GetContainerAncestors(id, cancellationToken).ConfigureAwait(false);
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
        if (await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false) is null)
        {
            return NotFound();
        }

        var files = new List<ChildFile>();
        var containers = new List<ChildContainer>();
        // Parse the WOPI wire format once and hand the typed list to the provider, which is
        // responsible for filtering at (or as close as possible to) the storage layer. See
        // IWopiStorageProvider.GetWopiFiles for the contract on extension matching.
        var fileExtensions = fileExtensionFilterList?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        await foreach (var wopiFile in storageProvider.GetWopiFiles(id, fileExtensions, cancellationToken).ConfigureAwait(false))
        {
            files.Add(new ChildFile(wopiFile.Name + '.' + wopiFile.Extension, Url.GetWopiSrc(WopiResourceType.File, wopiFile.Identifier))
            {
                LastModifiedTime = wopiFile.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture),
                Size = wopiFile.Length,
                Version = wopiFile.Version ?? wopiFile.LastWriteTimeUtc.ToString("s", CultureInfo.InvariantCulture)
            });
        }

        await foreach (var wopiContainer in storageProvider.GetWopiContainers(id, cancellationToken).ConfigureAwait(false))
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
