using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using WopiHost.Abstractions;
using WopiHost.Core.Cobalt;
using WopiHost.Core.Models;
using WopiHost.Core.Results;
using WopiHost.Core.Security;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Core.Controllers
{
    /// <summary>
    /// Implementation of WOPI server protocol https://msdn.microsoft.com/en-us/library/hh659001.aspx
    /// </summary>
    [Route("wopi/[controller]")]
    public class FilesController : WopiControllerBase
    {
        private readonly IAuthorizationService _authorizationService;

        private WopiDiscoverer _wopiDiscoverer;

        /// <summary>
        /// Collection holding information about locks. Should be persistant.
        /// </summary>
        private static IDictionary<string, LockInfo> LockStorage;

        private string WopiOverrideHeader => HttpContext.Request.Headers[WopiHeaders.WopiOverride];


        private WopiDiscoverer WopiDiscoverer
        {
            get { return _wopiDiscoverer ?? (_wopiDiscoverer = new WopiDiscoverer(new HttpDiscoveryFileProvider(Configuration.GetValue("WopiClientUrl", string.Empty)))); }
        }

        public FilesController(IWopiStorageProvider storageProvider, IWopiSecurityHandler securityHandler, IConfiguration configuration, IAuthorizationService authorizationService, IDictionary<string, LockInfo> lockStorage) : base(storageProvider, securityHandler, configuration)
        {
            _authorizationService = authorizationService;
            LockStorage = lockStorage;
        }

        private async Task<IEditSession> GetEditSessionAsync(string fileId)
        {
            var sessionId = fileId;
            IEditSession editSession = SessionManager.Current.GetSession(sessionId);

            if (editSession == null)
            {
                IWopiFile file = StorageProvider.GetWopiFile(fileId);

                //TODO: get rid of sessions, make cobalt optional, set up capabilities instead of the check
                if (await WopiDiscoverer.RequiresCobaltAsync(file.Extension, WopiActionEnum.Edit) || file.Extension == "docx")
                {
                    editSession = new CobaltSession(file, sessionId, HttpContext.User);
                }
                else
                {
                    editSession = new FileSession(file, sessionId, HttpContext.User);
                }
                SessionManager.Current.AddSession(editSession);
            }

            return editSession;
        }

        /// <summary>
        /// Returns the metadata about a file specified by an identifier.
        /// Specification: https://msdn.microsoft.com/en-us/library/hh643136.aspx
        /// Example URL: HTTP://server/<...>/wopi*/files/<id>
        /// </summary>
        /// <param name="id">File identifier.</param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCheckFileInfo(string id)
        {
            if (!await _authorizationService.AuthorizeAsync(User, new FileResource(id), WopiOperations.Read))
            {
                return Unauthorized();
            }
            return new JsonResult((await GetEditSessionAsync(id))?.GetCheckFileInfo());
        }

        /// <summary>
        /// Returns contents of a file specified by an identifier.
        /// Specification: https://msdn.microsoft.com/en-us/library/hh657944.aspx
        /// Example URL: HTTP://server/<...>/wopi*/files/<id>/contents
        /// </summary>
        /// <param name="id">File identifier.</param>
        /// <returns></returns>
        [HttpGet("{id}/contents")]
        public async Task<IActionResult> GetFile(string id)
        {
            // Check permissions
            if (!await _authorizationService.AuthorizeAsync(User, new FileResource(id), WopiOperations.Read))
            {
                return Unauthorized();
            }

            // Get session
            var editSession = await GetEditSessionAsync(id);

            // Check expected size
            int? maximumExpectedSize = HttpContext.Request.Headers[WopiHeaders.MaxExpectedSize].ToString().ToNullableInt();
            if (maximumExpectedSize != null && editSession.GetCheckFileInfo().Size > maximumExpectedSize.Value)
            {
                return new PreconditionFailedResult();
            }

            try
            {
                // Try to read content from a stream
                return new FileStreamResult(editSession.GetFileStream(), "application/octet-stream");
            }
            catch (NotImplementedException)
            {
                // Try to read content from a byte array
                return new FileContentResult(editSession.GetFileContent(), "application/octet-stream");
            }
        }

        /// <summary>
        /// Updates a file specified by an identifier. (Only for non-cobalt files.)
        /// Specification: https://msdn.microsoft.com/en-us/library/hh657364.aspx
        /// Example URL: HTTP://server/<...>/wopi*/files/<id>/contents
        /// </summary>
        /// <param name="id">File identifier.</param>
        /// <returns></returns>
        [HttpPut("{id}/contents")]
        [HttpPost("{id}/contents")]
        public async Task<IActionResult> PutFile(string id)
        {
            // Check permissions
            if (!await _authorizationService.AuthorizeAsync(User, new FileResource(id), WopiOperations.Update))
            {
                return Unauthorized();
            }

            // Acquire lock
            var lockResult = ProcessLock(id);

            if (lockResult is OkResult)
            {
                // Get session
                var editSession = await GetEditSessionAsync(id);

                // Save file contents
                editSession.SetFileContent(await HttpContext.Request.Body.ReadBytesAsync());
                return new OkResult();
            }
            return lockResult;
        }


        /// <summary>
        /// Changes the contents of the file in accordance with [MS-FSSHTTP] and performs other operations like locking.
        /// MS-FSSHTTP Specification: https://msdn.microsoft.com/en-us/library/dd943623.aspx
        /// Specification: https://msdn.microsoft.com/en-us/library/hh659581.aspx
        /// Example URL: HTTP://server/<...>/wopi*/files/<id>
        /// </summary>
        /// <param name="id"></param>
        [HttpPost("{id}")]
        public async Task<IActionResult> PerformAction(string id)
        {
            // Check permissions
            if (!await _authorizationService.AuthorizeAsync(User, new FileResource(id), WopiOperations.Update))
            {
                return Unauthorized();
            }

            var editSession = await GetEditSessionAsync(id);

            switch (WopiOverrideHeader)
            {
                case "COBALT":
                    var responseAction = editSession.SetFileContent(await HttpContext.Request.Body.ReadBytesAsync());
                    HttpContext.Response.Headers.Add(WopiHeaders.CorrelationId, HttpContext.Request.Headers[WopiHeaders.CorrelationId]);
                    HttpContext.Response.Headers.Add("request-id", HttpContext.Request.Headers[WopiHeaders.CorrelationId]);
                    return new Results.FileResult(responseAction, "application/octet-stream");

                case "LOCK":
                case "UNLOCK":
                case "REFRESH_LOCK":
                case "GET_LOCK":
                    return ProcessLock(id);

                default:
                    // Unsupported action
                    return new NotImplementedResult();
            }
        }

        #region "Locking"

        private IActionResult ProcessLock(string id)
        {
            string oldLock = Request.Headers[WopiHeaders.OldLock];
            string newLock = Request.Headers[WopiHeaders.Lock];

            lock (LockStorage)
            {
                LockInfo existingLock = null;
                bool lockAcquired = TryGetLock(id, out existingLock);
                switch (WopiOverrideHeader)
                {
                    case "GET_LOCK":
                        if (lockAcquired)
                        {
                            Response.Headers[WopiHeaders.Lock] = existingLock.Lock;
                        }
                        return new OkResult();

                    case "LOCK":
                    case "PUT":
                        if (oldLock == null)
                        {
                            if (lockAcquired)
                            {
                                // There is a valid existing lock on the file
                                return ReturnLockMismatch(Response, existingLock.Lock);
                            }
                            else
                            {
                                // The file is not currently locked, create and store new lock information
                                LockStorage[id] = new LockInfo { DateCreated = DateTime.UtcNow, Lock = newLock };
                                return new OkResult();
                            }
                        }
                        else
                        {
                            // Unlock and relock
                            if (lockAcquired)
                            {
                                if (existingLock.Lock == oldLock)
                                {
                                    // Replace the existing lock with the new one
                                    LockStorage[id] = new LockInfo { DateCreated = DateTime.UtcNow, Lock = newLock };
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
                                LockStorage.Remove(id);
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
            if (LockStorage.TryGetValue(fileId, out lockInfo))
            {
                if (lockInfo.Expired)
                {
                    LockStorage.Remove(fileId);
                    return false;
                }
                return true;
            }

            return false;
        }


        private StatusCodeResult ReturnLockMismatch(HttpResponse response, string existingLock = null, string reason = null)
        {
            response.Headers[WopiHeaders.Lock] = existingLock ?? String.Empty;
            if (!String.IsNullOrEmpty(reason))
            {
                response.Headers[WopiHeaders.LockFailureReason] = reason;
            }
            return new ConflictResult();
        }

        #endregion
    }
}
