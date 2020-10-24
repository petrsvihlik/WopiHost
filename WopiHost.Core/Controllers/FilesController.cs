﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Models;
using WopiHost.Core.Results;
using WopiHost.Core.Security;

namespace WopiHost.Core.Controllers
{
    /// <summary>
    /// Implementation of WOPI server protocol
    /// Specification: https://wopi.readthedocs.io/projects/wopirest/en/latest/
    /// </summary>
    [Route("wopi/[controller]")]
    public class FilesController : WopiControllerBase
    {
        private readonly IAuthorizationService _authorizationService;

        public ICobaltProcessor CobaltProcessor { get; set; }

        private HostCapabilities HostCapabilities => new HostCapabilities
        {
            SupportsCobalt = CobaltProcessor is { },
            SupportsGetLock = true,
            SupportsLocks = true,
            SupportsExtendedLockLength = true,
            SupportsFolders = true,//?
            SupportsCoauth = true,//?
            SupportsUpdate = nameof(PutFile) is { } //&& PutRelativeFile - usercannotwriterelative
        };

        /// <summary>
        /// Collection holding information about locks. Should be persistent.
        /// </summary>
        private static IDictionary<string, LockInfo> _lockStorage;

        private string WopiOverrideHeader => HttpContext.Request.Headers[WopiHeaders.WOPI_OVERRIDE];

        public FilesController(IWopiStorageProvider storageProvider, IWopiSecurityHandler securityHandler, IOptionsSnapshot<WopiHostOptions> wopiHostOptions, IAuthorizationService authorizationService, IDictionary<string, LockInfo> lockStorage, ICobaltProcessor cobaltProcessor = null) : base(storageProvider, securityHandler, wopiHostOptions)
        {
            _authorizationService = authorizationService;
            _lockStorage = lockStorage;
            CobaltProcessor = cobaltProcessor;
        }

        /// <summary>
        /// Returns the metadata about a file specified by an identifier.
        /// Specification: https://wopi.readthedocs.io/projects/wopirest/en/latest/files/CheckFileInfo.html
        /// Example URL path: /wopi/files/(file_id)
        /// </summary>
        /// <param name="id">File identifier.</param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCheckFileInfo(string id)
        {
            if (!(await _authorizationService.AuthorizeAsync(User, new FileResource(id), WopiOperations.Read)).Succeeded)
            {
                return Unauthorized();
            }
            return new JsonResult(StorageProvider.GetWopiFile(id)?.GetCheckFileInfo(User, HostCapabilities), null);
        }

        /// <summary>
        /// Returns contents of a file specified by an identifier.
        /// Specification: https://wopi.readthedocs.io/projects/wopirest/en/latest/files/GetFile.html
        /// Example URL path: /wopi/files/(file_id)/contents
        /// </summary>
        /// <param name="id">File identifier.</param>
        /// <returns></returns>
        [HttpGet("{id}/contents")]
        public async Task<IActionResult> GetFile(string id)
        {
            // Check permissions
            if (!(await _authorizationService.AuthorizeAsync(User, new FileResource(id), WopiOperations.Read)).Succeeded)
            {
                return Unauthorized();
            }

            // Get file
            var file = StorageProvider.GetWopiFile(id);

            // Check expected size
            var maximumExpectedSize = HttpContext.Request.Headers[WopiHeaders.MAX_EXPECTED_SIZE].ToString().ToNullableInt();
            if (maximumExpectedSize is { } && file.GetCheckFileInfo(User, HostCapabilities).Size > maximumExpectedSize.Value)
            {
                return new PreconditionFailedResult();
            }

            // Try to read content from a stream
            return new FileStreamResult(file.GetReadStream(), "application/octet-stream");
        }

        /// <summary>
        /// Updates a file specified by an identifier. (Only for non-cobalt files.)
        /// Specification: https://wopi.readthedocs.io/projects/wopirest/en/latest/files/PutFile.html
        /// Example URL path: /wopi/files/(file_id)/contents
        /// </summary>
        /// <param name="id">File identifier.</param>
        /// <returns></returns>
        [HttpPut("{id}/contents")]
        [HttpPost("{id}/contents")]
        public async Task<IActionResult> PutFile(string id)
        {
            // Check permissions
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, new FileResource(id), WopiOperations.Update);

            if (!authorizationResult.Succeeded)
            {
                return Unauthorized();
            }

            // Acquire lock
            var lockResult = ProcessLock(id);

            if (lockResult is OkResult)
            {
                // Get file
                var file = StorageProvider.GetWopiFile(id);

                // Save file contents
                var newContent = await HttpContext.Request.Body.ReadBytesAsync();
                await using (var stream = file.GetWriteStream())
                {
                    await stream.WriteAsync(newContent, 0, newContent.Length);
                }

                return new OkResult();
            }
            return lockResult;
        }

        [HttpPost("{id}"), WopiOverrideHeader(new[] { "PUT_RELATIVE" })]
        public async Task<IActionResult> PutRelativeFile(string id)
        {
            throw new NotImplementedException($"{nameof(PutRelativeFile)} is not implemented yet.");
        }

        /// <summary>
        /// Changes the contents of the file in accordance with [MS-FSSHTTP].
        /// MS-FSSHTTP Specification: https://msdn.microsoft.com/en-us/library/dd943623.aspx
        /// Specification: https://msdn.microsoft.com/en-us/library/hh659581.aspx
        /// Example URL path: /wopi/files/(file_id)
        /// </summary>
        /// <param name="id">File identifier.</param>
        [HttpPost("{id}"), WopiOverrideHeader(new[] { "COBALT" })]
        public async Task<IActionResult> ProcessCobalt(string id)
        {
            // Check permissions
            if (!(await _authorizationService.AuthorizeAsync(User, new FileResource(id), WopiOperations.Update)).Succeeded)
            {
                return Unauthorized();
            }

            var file = StorageProvider.GetWopiFile(id);

            // TODO: remove workaround https://github.com/aspnet/Announcements/issues/342 (use FileBufferingWriteStream)
            var syncIoFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
            if (syncIoFeature is { })
            {
                syncIoFeature.AllowSynchronousIO = true;
            }

            var responseAction = CobaltProcessor.ProcessCobalt(file, User, await HttpContext.Request.Body.ReadBytesAsync());
            HttpContext.Response.Headers.Add(WopiHeaders.CORRELATION_ID, HttpContext.Request.Headers[WopiHeaders.CORRELATION_ID]);
            HttpContext.Response.Headers.Add("request-id", HttpContext.Request.Headers[WopiHeaders.CORRELATION_ID]);
            return new Results.FileResult(responseAction, "application/octet-stream");
        }

        #region "Locking"

        /// <summary>
        /// Processes lock-related operations.
        /// Specification: https://wopi.readthedocs.io/projects/wopirest/en/latest/files/Lock.html
        /// Example URL path: /wopi/files/(file_id)
        /// </summary>
        /// <param name="id">File identifier.</param>
        [HttpPost("{id}"), WopiOverrideHeader(new[] { "LOCK", "UNLOCK", "REFRESH_LOCK", "GET_LOCK" })]
        public IActionResult ProcessLock(string id)
        {
            string oldLock = Request.Headers[WopiHeaders.OLD_LOCK];
            string newLock = Request.Headers[WopiHeaders.LOCK];

            lock (_lockStorage)
            {
                var lockAcquired = TryGetLock(id, out var existingLock);
                switch (WopiOverrideHeader)
                {
                    case "GET_LOCK":
                        if (lockAcquired)
                        {
                            Response.Headers[WopiHeaders.LOCK] = existingLock.Lock;
                        }
                        return new OkResult();

                    case "LOCK":
                    case "PUT":
                        if (oldLock is null)
                        {
                            // Lock / put
                            if (lockAcquired)
                            {
                                if (existingLock.Lock == newLock)
                                {
                                    // File is currently locked and the lock ids match, refresh lock
                                    existingLock.DateCreated = DateTime.UtcNow;
                                    return new OkResult();
                                }
                                else
                                {
                                    // There is a valid existing lock on the file
                                    return ReturnLockMismatch(Response, existingLock.Lock);
                                }
                            }
                            else
                            {
                                // The file is not currently locked, create and store new lock information
                                _lockStorage[id] = new LockInfo { DateCreated = DateTime.UtcNow, Lock = newLock };
                                return new OkResult();
                            }
                        }
                        else
                        {
                            // Unlock and relock (http://wopi.readthedocs.io/projects/wopirest/en/latest/files/UnlockAndRelock.html)
                            if (lockAcquired)
                            {
                                if (existingLock.Lock == oldLock)
                                {
                                    // Replace the existing lock with the new one
                                    _lockStorage[id] = new LockInfo { DateCreated = DateTime.UtcNow, Lock = newLock };
                                    return new OkResult();
                                }
                                else
                                {
                                    // The existing lock doesn't match the requested one. Return a lock mismatch error along with the current lock
                                    return ReturnLockMismatch(Response, existingLock.Lock);
                                }
                            }
                            else
                            {
                                // The requested lock does not exist which should result in a lock mismatch error.
                                return ReturnLockMismatch(Response, reason: "File not locked");
                            }
                        }

                    case "UNLOCK":
                        if (lockAcquired)
                        {
                            if (existingLock.Lock == newLock)
                            {
                                // Remove valid lock
                                _lockStorage.Remove(id);
                                return new OkResult();
                            }
                            else
                            {
                                // The existing lock doesn't match the requested one. Return a lock mismatch error along with the current lock
                                return ReturnLockMismatch(Response, existingLock.Lock);
                            }
                        }
                        else
                        {
                            // The requested lock does not exist.
                            return ReturnLockMismatch(Response, reason: "File not locked");
                        }

                    case "REFRESH_LOCK":
                        if (lockAcquired)
                        {
                            if (existingLock.Lock == newLock)
                            {
                                // Extend the lock timeout
                                existingLock.DateCreated = DateTime.UtcNow;
                                return new OkResult();
                            }
                            else
                            {
                                // The existing lock doesn't match the requested one. Return a lock mismatch error along with the current lock
                                return ReturnLockMismatch(Response, existingLock.Lock);
                            }
                        }
                        else
                        {
                            // The requested lock does not exist.  That's also a lock mismatch error.
                            return ReturnLockMismatch(Response, reason: "File not locked");
                        }
                }
            }

            return new OkResult();
        }

        private bool TryGetLock(string fileId, out LockInfo lockInfo)
        {
            if (_lockStorage.TryGetValue(fileId, out lockInfo))
            {
                if (lockInfo.Expired)
                {
                    _lockStorage.Remove(fileId);
                    return false;
                }
                return true;
            }

            return false;
        }

        private StatusCodeResult ReturnLockMismatch(HttpResponse response, string existingLock = null, string reason = null)
        {
            response.Headers[WopiHeaders.LOCK] = existingLock ?? string.Empty;
            if (!string.IsNullOrEmpty(reason))
            {
                response.Headers[WopiHeaders.LOCK_FAILURE_REASON] = reason;
            }
            return new ConflictResult();
        }

        #endregion
    }
}
