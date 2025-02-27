using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Models;
using WopiHost.Core.Results;
using WopiHost.Core.Security;

namespace WopiHost.Core.Controllers;

/// <summary>
/// Implementation of WOPI server protocol
/// Specification: https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/
/// </summary>
[Route("wopi/[controller]")]
public class FilesController : WopiControllerBase
{
    /// <summary>
    /// Service that can process MS-FSSHTTP requests.
    /// </summary>
    private readonly ICobaltProcessor? cobaltProcessor;

    private HostCapabilities HostCapabilities => new()
    {
        SupportsCobalt = cobaltProcessor is not null,
        SupportsGetLock = lockProvider is not null,
        SupportsLocks = lockProvider is not null,
        SupportsExtendedLockLength = true,
        SupportsFolders = true,
        SupportsCoauth = false,
        SupportsUpdate = true //TODO: PutRelativeFile
    };

    private readonly IWopiLockProvider? lockProvider;
    private readonly IAuthorizationService authorizationService;

    /// <summary>
    /// Creates an instance of <see cref="FilesController"/>.
    /// </summary>
    /// <param name="storageProvider">Storage provider instance for retrieving files and folders.</param>
    /// <param name="securityHandler">Security handler instance for performing security-related operations.</param>
    /// <param name="wopiHostOptions">WOPI Host configuration</param>
    /// <param name="authorizationService">An instance of authorization service capable of resource-based authorization.</param>
    /// <param name="lockProvider">An instance of the lock provider.</param>
    /// <param name="cobaltProcessor">An instance of a MS-FSSHTTP processor.</param>
    public FilesController(
        IWopiStorageProvider storageProvider,
        IWopiSecurityHandler securityHandler,
        IOptions<WopiHostOptions> wopiHostOptions,
        IAuthorizationService authorizationService,
        IWopiLockProvider? lockProvider = null,
        ICobaltProcessor? cobaltProcessor = null) : base(storageProvider, securityHandler, wopiHostOptions)
    {
        this.authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        this.lockProvider = lockProvider;
        this.cobaltProcessor = cobaltProcessor;
    }

    /// <summary>
    /// Returns the metadata about a file specified by an identifier.
    /// Specification: https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo
    /// Example URL path: /wopi/files/(file_id)
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <returns></returns>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetCheckFileInfo(string id)
    {
        if (!(await authorizationService.AuthorizeAsync(User, new FileResource(id), WopiOperations.Read)).Succeeded)
        {
            return Unauthorized();
        }
        return new JsonResult(StorageProvider.GetWopiFile(id)?.GetCheckFileInfo(User, HostCapabilities), null);
    }

    /// <summary>
    /// Returns contents of a file specified by an identifier.
    /// Specification: https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/getfile
    /// Example URL path: /wopi/files/(file_id)/contents
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="maximumExpectedSize"></param>
    /// <returns></returns>
    [HttpGet("{id}/contents")]
    public async Task<IActionResult> GetFile(
        string id,
        [FromHeader(Name = WopiHeaders.MAX_EXPECTED_SIZE)] int? maximumExpectedSize = null)
    {
        // Check permissions
        if (!(await authorizationService.AuthorizeAsync(User, new FileResource(id), WopiOperations.Read)).Succeeded)
        {
            return Unauthorized();
        }

        // Get file
        var file = StorageProvider.GetWopiFile(id);

        // Check expected size
        if (maximumExpectedSize is not null && file.GetCheckFileInfo(User, HostCapabilities).Size > maximumExpectedSize.Value)
        {
            return new PreconditionFailedResult();
        }

        // Try to read content from a stream
        return new FileStreamResult(file.GetReadStream(), "application/octet-stream");
    }

    /// <summary>
    /// Updates a file specified by an identifier. (Only for non-cobalt files.)
    /// Specification: https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/putfile
    /// Example URL path: /wopi/files/(file_id)/contents
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="newLockIdentifier">new lockId</param>
    /// <returns>Returns <see cref="StatusCodes.Status200OK"/> if succeeded.</returns>
    [HttpPut("{id}/contents")]
    [HttpPost("{id}/contents")]
    public async Task<IActionResult> PutFile(
        string id,
        [FromHeader(Name = WopiHeaders.LOCK)] string? newLockIdentifier = null)
    {
        // Check permissions
        var authorizationResult = await authorizationService.AuthorizeAsync(User, new FileResource(id), WopiOperations.Update);

        if (!authorizationResult.Succeeded)
        {
            return Unauthorized();
        }

        // Acquire lock
        var lockResult = ProcessLock(id, null, newLockIdentifier);

        if (lockResult is OkResult)
        {
            // Get file
            var file = StorageProvider.GetWopiFile(id);

            // Save file contents
            var newContent = await HttpContext.Request.Body.ReadBytesAsync();
            await using (var stream = file.GetWriteStream())
            {
                await stream.WriteAsync(newContent.AsMemory(0, newContent.Length));
            }

            return new OkResult();
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
    /// <returns>Returns <see cref="StatusCodes.Status200OK"/> if succeeded.</returns>
    [HttpPost("{id}"), WopiOverrideHeader([WopiFileOperations.PutRelativeFile])]
    public Task<IActionResult> PutRelativeFile(string id) => throw new NotImplementedException($"{nameof(PutRelativeFile)} is not implemented yet.");

    /// <summary>
    /// Changes the contents of the file in accordance with [MS-FSSHTTP].
    /// MS-FSSHTTP Specification: https://msdn.microsoft.com/en-us/library/dd943623.aspx
    /// Specification: https://msdn.microsoft.com/en-us/library/hh659581.aspx
    /// Example URL path: /wopi/files/(file_id)
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="correlationId"></param>
    [HttpPost("{id}"), WopiOverrideHeader([WopiFileOperations.Cobalt])]
    public async Task<IActionResult> ProcessCobalt(
        string id,
        [FromHeader(Name = WopiHeaders.CORRELATION_ID)] string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(cobaltProcessor);
        // Check permissions
        if (!(await authorizationService.AuthorizeAsync(User, new FileResource(id), WopiOperations.Update)).Succeeded)
        {
            return Unauthorized();
        }

        var file = StorageProvider.GetWopiFile(id);

        // TODO: remove workaround https://github.com/aspnet/Announcements/issues/342 (use FileBufferingWriteStream)
        var syncIoFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
        if (syncIoFeature is not null)
        {
            syncIoFeature.AllowSynchronousIO = true;
        }

        var responseAction = cobaltProcessor.ProcessCobalt(file, User, await HttpContext.Request.Body.ReadBytesAsync());
        if (!string.IsNullOrEmpty(correlationId))
        {
            HttpContext.Response.Headers.Append(WopiHeaders.CORRELATION_ID, correlationId);
            HttpContext.Response.Headers.Append("request-id", correlationId);
        }
        return new Results.FileResult(responseAction, "application/octet-stream");
    }

    #region "Locking"

    /// <summary>
    /// Processes lock-related operations.
    /// Specification: https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/lock
    /// Example URL path: /wopi/files/(file_id)
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="wopiOverrideHeader">A string specifying the requested operation from the WOPI server</param>
    /// <param name="oldLockIdentifier"></param>
    /// <param name="newLockIdentifier"></param>
    [HttpPost("{id}")]
    [WopiOverrideHeader([
        WopiFileOperations.Lock,
        WopiFileOperations.Put,
        WopiFileOperations.Unlock,
        WopiFileOperations.RefreshLock,
        WopiFileOperations.GetLock])]
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
        switch (wopiOverrideHeader)
        {
            case WopiFileOperations.GetLock:
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
                    // File is not locked (or lock expired)... return empty X-WOPI-Lock header
                    Response.Headers[WopiHeaders.LOCK] = string.Empty;
                }
                return new OkResult();

            case WopiFileOperations.Lock:
            case WopiFileOperations.Put:
                if (oldLockIdentifier is null)
                {
                    if (string.IsNullOrWhiteSpace(newLockIdentifier))
                    {
                        return new LockMismatchResult(Response, reason: "Missing new lock identifier");
                    }

                    // Lock / put
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
                        // The file is not currently locked, create and store new lock information
                        if (lockProvider.AddLock(id, newLockIdentifier) != null)
                        {
                            return new OkResult();
                        }
                        else
                        {
                            return new LockMismatchResult(Response, "Could not create lock");
                        }
                    }
                }
                else
                {
                    // Unlock and re-lock (https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/unlockandrelock)
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

                            // Replace the existing lock with the new one
                            if (lockProvider.RefreshLock(id, newLockIdentifier))
                            {
                                return new OkResult();
                            }
                            else
                            {
                                return new LockMismatchResult(Response, "Could not create lock");
                            }
                        }
                        else
                        {
                            // The existing lock doesn't match the requested one. Return a lock mismatch error along with the current lock
                            return new LockMismatchResult(Response, existingLock.LockId);
                        }
                    }
                    else
                    {
                        // The requested lock does not exist which should result in a lock mismatch error.
                        return new LockMismatchResult(Response, reason: "File not locked");
                    }
                }

            case WopiFileOperations.Unlock:
                if (lockAcquired)
                {
                    if (existingLock is null)
                    {
                        return new LockMismatchResult(Response, reason: "Missing existing lock");
                    }
                    if (existingLock.LockId == newLockIdentifier)
                    {
                        // Remove valid lock
                        if (lockProvider.RemoveLock(id))
                        {
                            return new OkResult();
                        }
                        else
                        {
                            return new LockMismatchResult(Response, "Could not remove lock");
                        }
                    }
                    else
                    {
                        // The existing lock doesn't match the requested one. Return a lock mismatch error along with the current lock
                        return new LockMismatchResult(Response, existingLock.LockId);
                    }
                }
                else
                {
                    // The requested lock does not exist.
                    return new LockMismatchResult(Response, reason: "File not locked");
                }

            case WopiFileOperations.RefreshLock:
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
                    // The requested lock does not exist. That's also a lock mismatch error.
                    return new LockMismatchResult(Response, reason: "File not locked");
                }
        }

        return new OkResult();
    }

    private IActionResult LockOrRefresh(string newLock, WopiLockInfo existingLock)
    {
        ArgumentNullException.ThrowIfNull(lockProvider);
        if (existingLock.LockId == newLock)
        {
            // File is currently locked and the lock ids match, refresh lock (extend the lock timeout)
            if (lockProvider.RefreshLock(existingLock.FileId))
            {
                return new OkResult();
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
