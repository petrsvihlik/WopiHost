using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Results;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Controllers;

/// <summary>
/// Implementation of WOPI server protocol
/// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/
/// </summary>
/// <param name="storageProvider">Storage provider instance for retrieving files and folders.</param>
/// <param name="securityHandler">Security handler instance for performing security-related operations.</param>
/// <param name="wopiHostOptions">WOPI Host configuration</param>
/// <param name="memoryCache">An instance of the memory cache.</param>
/// <param name="writableStorageProvider">Storage provider instance for writing files and folders.</param>
/// <param name="lockProvider">An instance of the lock provider.</param>
/// <param name="cobaltProcessor">An instance of a MS-FSSHTTP processor.</param>
[Authorize]
[ApiController]
[Route("wopi/[controller]")]
public class FilesController(
    IWopiStorageProvider storageProvider,
    IWopiSecurityHandler securityHandler,
    IOptions<WopiHostOptions> wopiHostOptions,
    IMemoryCache memoryCache,
    IWopiWritableStorageProvider? writableStorageProvider = null,
    IWopiLockProvider? lockProvider = null,
    ICobaltProcessor? cobaltProcessor = null) : ControllerBase
{
    private WopiHostCapabilities HostCapabilities => new()
    {
        SupportsCobalt = cobaltProcessor is not null,
        SupportsGetLock = lockProvider is not null,
        SupportsLocks = lockProvider is not null,
        SupportsCoauth = false,
        SupportsUpdate = true
    };
    private const string UserInfoCacheKey = "UserInfo-{0}";

    /// <summary>
    /// Returns the metadata about a file specified by an identifier.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo
    /// Example URL path: /wopi/files/(file_id)
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    [HttpGet("{id}", Name = WopiRouteNames.CheckFileInfo)]
    [WopiAuthorize(WopiResourceType.File, Permission.Read)]
    public async Task<IActionResult> CheckFileInfo(string id, CancellationToken cancellationToken = default)
    {
        // Get file
        var file = storageProvider.GetWopiFile(id);
        if (file is null)
        {
            return NotFound();
        }

        // build default checkFileInfo
        var checkFileInfo = await BuildCheckFileInfo(file, cancellationToken);

        // instead of JsonResult we must .Serialize<object>() to support properties that
        // might be defined on custom WopiCheckFileInfo objects
        return new ContentResult()
        {
            Content = JsonSerializer.Serialize<object>(checkFileInfo),
            ContentType = MediaTypeNames.Application.Json,
            StatusCode = StatusCodes.Status200OK
        };
    }

    /// <summary>
    /// Returns contents of a file specified by an identifier.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getfile
    /// Example URL path: /wopi/files/(file_id)/contents
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="maximumExpectedSize"></param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>FileStreamResult</returns>
    [HttpGet("{id}/contents")]
    [WopiAuthorize(WopiResourceType.File, Permission.Read)]
    public async Task<IActionResult> GetFile(
        string id,
        [FromHeader(Name = WopiHeaders.MAX_EXPECTED_SIZE)] int? maximumExpectedSize = null,
        CancellationToken cancellationToken = default)
    {
        // Get file
        var file = storageProvider.GetWopiFile(id);
        if (file is null)
        {
            return NotFound();
        }

        // Check expected size
        // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getfile#request-headers
        var size = file.Exists ? file.Length : 0;
        if (maximumExpectedSize is not null &&
            size > maximumExpectedSize.Value)
        {
            // File is larger than X-WOPI-MaxExpectedSize
            return new PreconditionFailedResult();
        }

        // Returns optional version
        // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getfile#response-headers
        if (file.Version is not null)
        {
            Response.Headers[WopiHeaders.WOPI_ITEM_VERSION] = file.Version;
        }

        // Try to read content from a stream
        return new FileStreamResult(await file.GetReadStream(cancellationToken), MediaTypeNames.Application.Octet);
    }

    /// <summary>
    /// The GetEcosystem operation returns the URI for the WOPI server’s Ecosystem endpoint, given a file ID.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/getecosystem
    /// Example URL path: /wopi/files/(container_id)/ecosystem_pointer
    /// </summary>
    /// <param name="id">A string that specifies a file ID of a file managed by host. This string must be URL safe.</param>
    /// <returns>URL response pointing to <see cref="WopiRouteNames.CheckEcosystem"/></returns>
    [HttpGet("{id}/ecosystem_pointer")]
    [WopiAuthorize(WopiResourceType.File, Permission.Read)]
    [Produces(MediaTypeNames.Application.Json)]
    public IActionResult GetEcosystem(string id)
    {
        // Get file
        var file = storageProvider.GetWopiFile(id);
        if (file is null)
        {
            return NotFound();
        }
        // A URI for the WOPI server’s Ecosystem endpoint, with an access token appended. A GET request to this URL will invoke the CheckEcosystem operation.
        return new JsonResult<UrlResponse>(
            new(Url.GetWopiUrl(WopiRouteNames.CheckEcosystem)));
    }

    /// <summary>
    /// The EnumerateAncestors operation enumerates all the parents of a given file, up to and including the root container.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/enumerateancestors
    /// Example URL path: /wopi/containers/(container_id)/ancestry
    /// </summary>
    /// <param name="id">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns></returns>
    [HttpGet("{id}/ancestry")]
    [WopiAuthorize(WopiResourceType.File, Permission.Read)]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> EnumerateAncestors(string id, CancellationToken cancellationToken = default)
    {
        // Get file
        var file = storageProvider.GetWopiFile(id);
        if (file is null)
        {
            return NotFound();
        }

        var ancestors = await storageProvider.GetAncestors(WopiResourceType.File, id, cancellationToken);
        return new JsonResult(
            new EnumerateAncestorsResponse(ancestors
                .Select(a => new ChildContainer(a.Name, Url.GetWopiUrl(WopiResourceType.Container, a.Identifier)))
            ));
    }

    /// <summary>
    /// Updates a file specified by an identifier. (Only for non-cobalt files.)
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putfile
    /// Example URL path: /wopi/files/(file_id)/contents
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="newLockIdentifier">new lockId</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Returns <see cref="StatusCodes.Status200OK"/> if succeeded.</returns>
    [HttpPut("{id}/contents")]
    [HttpPost("{id}/contents")]
    [WopiAuthorize(WopiResourceType.File, Permission.Update)]
    public async Task<IActionResult> PutFile(
        string id,
        [FromHeader(Name = WopiHeaders.LOCK)] string? newLockIdentifier = null,
        CancellationToken cancellationToken = default)
    {
        // https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/online/scenarios/createnew
        var file = storageProvider.GetWopiFile(id);
        if (file is null)
        {
            return NotFound();
        }
        
        // When a host receives a PutFile request on a file that's not locked, the host checks the current size of the file.
        if (string.IsNullOrEmpty(newLockIdentifier))
        {
            // If it's 0 bytes, the PutFile request should be considered valid and should proceed
            if (file.Size == 0)
            {
                // copy new contents to storage
                await HttpContext.CopyToWriteStream(file, cancellationToken);
                return Ok();
            }
            else // If ... missing altogether, the host should respond with a 409 Conflict
            {
                return new ConflictResult();
            }
        }

        // Acquire lock
        var lockResult = ProcessLock(id, wopiOverrideHeader: WopiFileOperations.Lock, newLockIdentifier: newLockIdentifier);

        if (lockResult is OkResult)
        {
            // copy new contents to storage
            await HttpContext.CopyToWriteStream(file, cancellationToken);

            return Ok();
        }
        return lockResult;
    }

    /// <summary>
    /// The PutRelativeFile operation creates a new file on the host based on the current file.
    /// M365 spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile
    /// Protocol spec: https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/d12ab554-eab7-480f-bdc7-0bdf14922e6f
    /// Example URL path: /wopi/files/(file_id)
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="suggestedTarget">A UTF-7 encoded string specifying either a file extension or a full file name, including the file extension</param>
    /// <param name="relativeTarget">A UTF-7 encoded string that specifies a full file name including the file extension. The host must not modify the name to fulfill the request.</param>
    /// <param name="newLockIdentifier">new lockId</param>
    /// <param name="overwriteRelativeTarget">A Boolean value that specifies whether the host must overwrite the file name if it exists. The default value is false.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>Returns <see cref="StatusCodes.Status200OK"/> if succeeded.</returns>
    [HttpPost("{id}"), WopiOverrideHeader(WopiFileOperations.PutRelativeFile)]
    [WopiAuthorize(WopiResourceType.File, Permission.Create)]
    public async Task<IActionResult> PutRelativeFile(
        string id,
        [FromHeader(Name = WopiHeaders.SUGGESTED_TARGET)] string? suggestedTarget = null,
        [FromHeader(Name = WopiHeaders.RELATIVE_TARGET)] string? relativeTarget = null,
        [FromHeader(Name = WopiHeaders.LOCK)] string? newLockIdentifier = null,
        [FromHeader(Name = WopiHeaders.OVERWRITE_RELATIVE_TARGET)] bool? overwriteRelativeTarget = false,
        CancellationToken cancellationToken = default)
    {
        if (writableStorageProvider is null)
        {
            return new NotImplementedResult();
        }

        var file = storageProvider.GetWopiFile(id);
        if (file is null)
        {
            return NotFound();
        }

        // the two headers are mutually exclusive. If both headers are present, the host should respond with a 501 Not Implemented status code.
        if (!string.IsNullOrWhiteSpace(suggestedTarget) &&
            !string.IsNullOrWhiteSpace(relativeTarget))
        {
            return new NotImplementedResult();
        }

        // find target container id based on the relative file specified by id
        var ancestors = await storageProvider.GetAncestors(WopiResourceType.File, id, cancellationToken);
        var parentContainer = ancestors.LastOrDefault()
            ?? throw new ArgumentException("Cannot find parent container", nameof(id));

        IWopiFile? newFile;

        // "specific mode" - The host must not modify the name to fulfill the request.
        if (!string.IsNullOrWhiteSpace(relativeTarget))
        {
            // If the specified name is illegal, the host must respond with a 400 Bad Request.
            if (!await writableStorageProvider.CheckValidName(WopiResourceType.File, relativeTarget, cancellationToken))
            {
                return new BadRequestResult();
            }

            newFile = await storageProvider.GetWopiResourceByName(WopiResourceType.File, parentContainer.Identifier, relativeTarget, cancellationToken) as IWopiFile;

            // If a file with the specified name already exists
            if (newFile is not null)
            {
                // unless the X-WOPI-OverwriteRelativeTarget request header is set to true...
                if (overwriteRelativeTarget == false)
                {
                    // the host might include an X-WOPI-ValidRelativeTarget specifying a file name that's valid
                    Response.Headers[WopiHeaders.VALID_RELATIVE_TARGET] = await writableStorageProvider.GetSuggestedName(WopiResourceType.File, id, relativeTarget, cancellationToken);
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
                newFile = await writableStorageProvider.CreateWopiChildResource(
                    WopiResourceType.File,
                    parentContainer.Identifier,
                    relativeTarget,
                    cancellationToken) as IWopiFile;
            }
        }
        else if (!string.IsNullOrWhiteSpace(suggestedTarget))
        {
            // If only the extension is provided, the name of the initial file without extension should be combined with the extension to create the proposed name
            if (suggestedTarget.StartsWith(".", StringComparison.OrdinalIgnoreCase))
            {
                suggestedTarget = file.Name + suggestedTarget;
            }
            // If the specified name is illegal, the host must respond with a 400 Bad Request.
            else if (!await writableStorageProvider.CheckValidName(WopiResourceType.File, suggestedTarget, cancellationToken))
            {
                return new BadRequestResult();
            }

            var newName = await writableStorageProvider.GetSuggestedName(WopiResourceType.File, parentContainer.Identifier, suggestedTarget, cancellationToken);
            newFile = await writableStorageProvider.CreateWopiChildResource(
                WopiResourceType.File,
                parentContainer.Identifier,
                newName,
                cancellationToken) as IWopiFile;
        }
        else
        {
            return new BadRequestResult();
        }

        if (newFile is not null)
        {
            // copy new contents to storage
            await HttpContext.CopyToWriteStream(newFile, cancellationToken);
            var checkFileInfo = await BuildCheckFileInfo(newFile, cancellationToken);
            return new JsonResult(
                new ChildFile(
                    newFile.Name + '.' + newFile.Extension,
                    Url.GetWopiUrl(WopiResourceType.File, newFile.Identifier))
                {
                    HostEditUrl = checkFileInfo.HostEditUrl?.ToString(),
                    HostViewUrl = checkFileInfo.HostViewUrl?.ToString() 
                });
        }

        return new InternalServerErrorResult();        
    }

    /// <summary>
    /// The PutUserInfo operation stores some basic user information on the host.
    /// M365 spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putuserinfo
    /// Example URL path: /wopi/files/(file_id)
    /// </summary>
    /// <param name="id">A string that specifies a file ID of a file managed by host. This string must be URL safe.</param>
    /// <param name="userInfo">A string that specifies the user information to be stored on the host. This string must be URL safe.</param>
    /// <returns>Returns <see cref="StatusCodes.Status200OK"/> if succeeded.</returns>
    [HttpPost("{id}"), WopiOverrideHeader(WopiFileOperations.PutUserInfo)]
    public IActionResult PutUserInfo(
        string id,
        [FromStringBody] string userInfo)
    {
        // Get file
        var file = storageProvider.GetWopiFile(id);
        if (file is null)
        {
            return NotFound();
        }

        // The UserInfo string should be associated with a particular user,
        // and should be passed back to the WOPI client in subsequent CheckFileInfo responses in the UserInfo property.
        // we store indefinitely in memoryCache to avoid the need for a persistence model - it's called anyway by the Wopi client on every start
        memoryCache.Set(
            string.Format(UserInfoCacheKey, User.GetUserId()), 
            userInfo, 
            new MemoryCacheEntryOptions
            {
                Priority = CacheItemPriority.NeverRemove,
            });

        return Ok();
    }

    /// <summary>
    /// The DeleteFile operation deletes a file from a host.
    /// M365 spec: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/deletefile
    /// Example URL path: /wopi/files/(file_id)
    /// </summary>
    /// <param name="id">A string that specifies a file ID of a file managed by host. This string must be URL safe.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Returns <see cref="StatusCodes.Status200OK"/> if succeeded.</returns>
    [HttpPost("{id}"), WopiOverrideHeader(WopiFileOperations.DeleteFile)]
    [WopiAuthorize(WopiResourceType.File, Permission.Delete)]
    public async Task<IActionResult> DeleteFile(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (writableStorageProvider is null)
        {
            return new NotImplementedResult();
        }

        // Get file
        var file = storageProvider.GetWopiFile(id);
        if (file is null)
        {
            return NotFound();
        }

        // If the file is currently locked, the host should return a 409 Conflict
        // and include an X-WOPI-Lock response header containing the value of the current lock on the file
        if (lockProvider?.TryGetLock(id, out var existingLock) == true)
        {
            return new LockMismatchResult(Response, existingLock.LockId);
        }

        if (await writableStorageProvider.DeleteWopiResource(WopiResourceType.File, id, cancellationToken))
        {
            return Ok();
        }

        return new InternalServerErrorResult();
    }

    /// <summary>
    /// Changes the contents of the file in accordance with [MS-FSSHTTP].
    /// MS-FSSHTTP Specification: https://learn.microsoft.com/openspecs/sharepoint_protocols/ms-fsshttp/05fa7efd-48ed-48d5-8d85-77995e17cc81
    /// Specification: https://learn.microsoft.com/openspecs/office_protocols/ms-wopi/f52e753e-fa08-4ba4-a68b-2f8801992cf0
    /// Example URL path: /wopi/files/(file_id)
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="correlationId"></param>
    [HttpPost("{id}"), WopiOverrideHeader(WopiFileOperations.Cobalt)]
    [WopiAuthorize(WopiResourceType.File, Permission.Update)]
    public async Task<IActionResult> ProcessCobalt(
        string id,
        [FromHeader(Name = WopiHeaders.CORRELATION_ID)] string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(cobaltProcessor);

        var file = storageProvider.GetWopiFile(id);

        // TODO: remove workaround https://github.com/aspnet/Announcements/issues/342 (use FileBufferingWriteStream)
        var syncIoFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
        if (syncIoFeature is not null)
        {
            syncIoFeature.AllowSynchronousIO = true;
        }

        var responseAction = await cobaltProcessor.ProcessCobalt(file, User, await HttpContext.Request.Body.ReadBytesAsync());
        if (!string.IsNullOrEmpty(correlationId))
        {
            HttpContext.Response.Headers.Append(WopiHeaders.CORRELATION_ID, correlationId);
            HttpContext.Response.Headers.Append("request-id", correlationId);
        }
        return new Results.FileResult(responseAction, MediaTypeNames.Application.Octet);
    }

    /// <summary>
    /// Returns a CheckFileInfo model according to https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo
    /// </summary>
    /// <param name="file">File properties of which should be returned.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CheckFileInfo model</returns>
    private async Task<WopiCheckFileInfo> BuildCheckFileInfo(
        IWopiFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        var checkFileInfo = file.GetWopiCheckFileInfo(HostCapabilities);
        checkFileInfo.FileNameMaxLength = writableStorageProvider?.FileNameMaxLength ?? 0;
        checkFileInfo.Sha256 = await file.GetEncodedSha256(cancellationToken);

        if (User?.Identity?.IsAuthenticated == true)
        {
            checkFileInfo.UserId = User.GetUserId();
            checkFileInfo.HostAuthenticationId = checkFileInfo.UserId;
            checkFileInfo.UserFriendlyName = User.FindFirst(ClaimTypes.Name)?.Value;
            checkFileInfo.UserPrincipalName = User.FindFirst(ClaimTypes.Upn)?.Value ?? string.Empty;

            // try to parse permissions claims
            var permissions = await securityHandler.GetUserPermissions(User, file, cancellationToken);
            checkFileInfo.ReadOnly = permissions.HasFlag(WopiUserPermissions.ReadOnly);
            checkFileInfo.RestrictedWebViewOnly = permissions.HasFlag(WopiUserPermissions.RestrictedWebViewOnly);
            checkFileInfo.UserCanAttend = permissions.HasFlag(WopiUserPermissions.UserCanAttend);
            checkFileInfo.UserCanNotWriteRelative = !HostCapabilities.SupportsUpdate || permissions.HasFlag(WopiUserPermissions.UserCanNotWriteRelative);
            checkFileInfo.UserCanPresent = permissions.HasFlag(WopiUserPermissions.UserCanPresent);
            checkFileInfo.UserCanRename = permissions.HasFlag(WopiUserPermissions.UserCanRename);
            checkFileInfo.UserCanWrite = permissions.HasFlag(WopiUserPermissions.UserCanWrite);
            checkFileInfo.WebEditingDisabled = permissions.HasFlag(WopiUserPermissions.WebEditingDisabled);

            // The UserInfo ... should be passed back to the WOPI client in subsequent CheckFileInfo responses in the UserInfo property.
            if (memoryCache.TryGetValue(string.Format(UserInfoCacheKey, checkFileInfo.UserId), out string? userInfo) &&
                userInfo is not null)
            {
                checkFileInfo.UserInfo = userInfo;
            }
        }
        else
        {
            checkFileInfo.IsAnonymousUser = true;
        }

        // allow changes and/or extensions before returning 
        return await wopiHostOptions.Value.OnCheckFileInfo(new WopiCheckFileInfoContext(User, file, checkFileInfo));
    }

    #region "Locking"
    /// <summary>
    /// Processes lock-related operations.
    /// Specification: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/lock
    /// Example URL path: /wopi/files/(file_id)
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="wopiOverrideHeader">A string specifying the requested operation from the WOPI server</param>
    /// <param name="oldLockIdentifier"></param>
    /// <param name="newLockIdentifier"></param>
    [HttpPost("{id}")]
    [WopiOverrideHeader(
        WopiFileOperations.Lock,
        WopiFileOperations.Put,
        WopiFileOperations.Unlock,
        WopiFileOperations.RefreshLock,
        WopiFileOperations.GetLock)]
    [WopiAuthorize(WopiResourceType.File, Permission.Update)]
    public IActionResult ProcessLock(
        string id,
        [FromHeader(Name = WopiHeaders.WOPI_OVERRIDE)] string? wopiOverrideHeader = null,
        [FromHeader(Name = WopiHeaders.OLD_LOCK)] string? oldLockIdentifier = null,
        [FromHeader(Name = WopiHeaders.LOCK)] string? newLockIdentifier = null)
    {
        if (lockProvider is null)
        {
            return new LockMismatchResult(Response, reason: "Locking is not supported");
        }

        var lockAcquired = lockProvider.TryGetLock(id, out var existingLock);
        return wopiOverrideHeader switch
        {
            WopiFileOperations.GetLock => HandleGetLock(lockAcquired, existingLock),
            WopiFileOperations.Lock or WopiFileOperations.Put => HandleLockOrPut(id, oldLockIdentifier, newLockIdentifier, lockAcquired, existingLock),
            WopiFileOperations.Unlock => HandleUnlock(id, newLockIdentifier, lockAcquired, existingLock),
            WopiFileOperations.RefreshLock => HandleRefreshLock(newLockIdentifier, lockAcquired, existingLock),
            _ => new NotImplementedResult(),
        };
    }

    private IActionResult HandleGetLock(bool lockAcquired, WopiLockInfo? existingLock)
    {
        if (lockAcquired)
        {
            if (existingLock is null)
            {
                return new LockMismatchResult(Response, reason: "Missing existing lock");
            }
            Response.Headers[WopiHeaders.LOCK] = existingLock.LockId;
        }
        else
        {
            Response.Headers[WopiHeaders.LOCK] = string.Empty;
        }
        return Ok();
    }

    private IActionResult HandleLockOrPut(string id, string? oldLockIdentifier, string? newLockIdentifier, bool lockAcquired, WopiLockInfo? existingLock)
    {
        ArgumentNullException.ThrowIfNull(lockProvider);
        if (oldLockIdentifier is null)
        {
            if (string.IsNullOrWhiteSpace(newLockIdentifier))
            {
                return new LockMismatchResult(Response, reason: "Missing new lock identifier");
            }

            if (lockAcquired)
            {
                if (existingLock is null)
                {
                    return new LockMismatchResult(Response, reason: "Missing existing lock");
                }
                return LockOrRefresh(newLockIdentifier, existingLock);
            }
            else
            {
                if (lockProvider.AddLock(id, newLockIdentifier) != null)
                {
                    return Ok();
                }
                else
                {
                    return new LockMismatchResult(Response, "Could not create lock");
                }
            }
        }
        else
        {
            if (lockAcquired)
            {
                if (existingLock is null)
                {
                    return new LockMismatchResult(Response, reason: "Missing existing lock");
                }
                if (existingLock.LockId == oldLockIdentifier)
                {
                    if (string.IsNullOrWhiteSpace(newLockIdentifier))
                    {
                        return new LockMismatchResult(Response, reason: "Missing new lock identifier");
                    }

                    if (lockProvider.RefreshLock(id, newLockIdentifier))
                    {
                        return Ok();
                    }
                    else
                    {
                        return new LockMismatchResult(Response, "Could not create lock");
                    }
                }
                else
                {
                    return new LockMismatchResult(Response, existingLock.LockId);
                }
            }
            else
            {
                return new LockMismatchResult(Response, reason: "File not locked");
            }
        }
    }

    private IActionResult HandleUnlock(string id, string? newLockIdentifier, bool lockAcquired, WopiLockInfo? existingLock)
    {
        ArgumentNullException.ThrowIfNull(lockProvider);

        if (lockAcquired)
        {
            if (existingLock is null)
            {
                return new LockMismatchResult(Response, reason: "Missing existing lock");
            }
            if (existingLock.LockId == newLockIdentifier)
            {
                if (lockProvider.RemoveLock(id))
                {
                    return Ok();
                }
                else
                {
                    return new LockMismatchResult(Response, "Could not remove lock");
                }
            }
            else
            {
                return new LockMismatchResult(Response, existingLock.LockId);
            }
        }
        else
        {
            return new LockMismatchResult(Response, reason: "File not locked");
        }
    }

    private IActionResult HandleRefreshLock(string? newLockIdentifier, bool lockAcquired, WopiLockInfo? existingLock)
    {
        if (lockAcquired)
        {
            if (existingLock is null)
            {
                return new LockMismatchResult(Response, reason: "Missing existing lock");
            }
            if (string.IsNullOrWhiteSpace(newLockIdentifier))
            {
                return new LockMismatchResult(Response, reason: "Missing new lock identifier");
            }
            return LockOrRefresh(newLockIdentifier, existingLock);
        }
        else
        {
            return new LockMismatchResult(Response, reason: "File not locked");
        }
    }

    private IActionResult LockOrRefresh(string newLock, WopiLockInfo existingLock)
    {
        ArgumentNullException.ThrowIfNull(lockProvider);
        if (existingLock.LockId == newLock)
        {
            // File is currently locked and the lock ids match, refresh lock (extend the lock timeout)
            if (lockProvider.RefreshLock(existingLock.FileId))
            {
                return Ok();
            }
            else
            {
                // The lock has expired
                return new LockMismatchResult(Response, reason: "Could not refresh lock");
            }
        }
        else
        {
            // The existing lock doesn't match the requested one (someone else might have locked the file). Return a lock mismatch error along with the current lock
            return new LockMismatchResult(Response, existingLock.LockId);
        }
    }
    #endregion
}
