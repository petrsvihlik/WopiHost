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
/// Implementation of WOPI Folders protocol (OneNote for the web operations).
/// Specification: https://learn.microsoft.com/openspecs/office_protocols/ms-wopi
/// Note: CheckFolderInfo and EnumerateChildren (folders) are only used by OneNote for the web
/// and are not required to integrate with Microsoft 365 for the web or Microsoft 365 for mobile.
/// See also: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/onenote
/// </summary>
/// <remarks>
/// Creates an instance of <see cref="FoldersController"/>.
/// </remarks>
/// <param name="storageProvider">Storage provider instance for retrieving files and folders.</param>
/// <param name="checkFolderInfoBuilder">Builds the default <see cref="WopiCheckFolderInfo"/> response.</param>
/// <param name="extensions">Host-customization seam for <see cref="IWopiHostExtensions.OnCheckFolderInfoAsync"/>.</param>
[Authorize]
[ApiController]
[Route("wopi/[controller]")]
[ServiceFilter(typeof(WopiOriginValidationActionFilter))]
[ServiceFilter(typeof(WopiTelemetryActionFilter))]
public class FoldersController(
    IWopiStorageProvider storageProvider,
    ICheckFolderInfoBuilder checkFolderInfoBuilder,
    IWopiHostExtensions extensions) : ControllerBase
{
    /// <summary>
    /// Returns the metadata about a folder specified by an identifier.
    /// Specification: https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/4b1d38ff-0d6a-42e4-8901-175b3c3c5890
    /// Example URL path: /wopi/folders/(folder_id)
    /// </summary>
    /// <param name="id">A string that specifies a folder ID of a folder managed by host. This string must be URL safe.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns></returns>
    [HttpGet("{id}", Name = WopiRouteNames.CheckFolderInfo)]
    [Produces(MediaTypeNames.Application.Json)]
    [WopiAuthorize(WopiResourceType.Container, Permission.Read)]
    public async Task<IActionResult> CheckFolderInfo(string id, CancellationToken cancellationToken = default)
    {
        var folder = await storageProvider.GetWopiContainer(id, cancellationToken).ConfigureAwait(false);
        if (folder is null)
        {
            return NotFound();
        }

        // Build the default response synchronously, then fire the host-customization hook here —
        // keeps the only `await` on a direct interface call, avoiding the Infer# false positive
        // tracked by #363.
        var checkFolderInfo = checkFolderInfoBuilder.Build(folder, HttpContext);
        checkFolderInfo = await extensions.OnCheckFolderInfoAsync(
            new WopiCheckFolderInfoContext(HttpContext.User, folder, checkFolderInfo),
            cancellationToken).ConfigureAwait(false);
        return new JsonResult<WopiCheckFolderInfo>(checkFolderInfo);
    }

    /// <summary>
    /// The EnumerateChildren method returns the files within a folder on the WOPI server.
    /// This is a OneNote for the web operation that returns only child files (not child containers).
    /// Specification: https://learn.microsoft.com/openspecs/office_protocols/ms-wopi
    /// Example URL path: /wopi/folders/(folder_id)/children
    /// </summary>
    /// <param name="id">A string that specifies a folder ID of a folder managed by host. This string must be URL safe.</param>
    /// <param name="fileExtensionFilterList">A string value that the host must use to filter the returned child files.
    /// This header must be a list of comma-separated file extensions with a leading dot (.).
    /// There must be no whitespace and no trailing comma in the string.
    /// Wildcard characters are not permitted.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>JSON object with ChildFiles array</returns>
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
        // Parse the WOPI wire format once and hand the typed list to the provider, which is
        // responsible for filtering at (or as close as possible to) the storage layer. See
        // IWopiStorageProvider.GetWopiFiles for the contract on extension matching.
        var fileExtensions = fileExtensionFilterList?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        await foreach (var wopiFile in storageProvider.GetWopiFiles(id, fileExtensions, cancellationToken).ConfigureAwait(false))
        {
            files.Add(new ChildFile(wopiFile.Name + '.' + wopiFile.Extension, Url.GetWopiSrc(wopiFile))
            {
                LastModifiedTime = wopiFile.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture),
                Size = wopiFile.Length,
                Version = wopiFile.Version ?? wopiFile.LastWriteTimeUtc.ToString("s", CultureInfo.InvariantCulture)
            });
        }

        return new JsonResult(new { ChildFiles = files });
    }
}
