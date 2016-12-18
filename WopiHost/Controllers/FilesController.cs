using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

using WopiHost.Abstractions;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using WopiHost.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using WopiHost.Cobalt;
using WopiHost.Results;

namespace WopiHost.Controllers
{
	/// <summary>
	/// Implementation of WOPI server protocol https://msdn.microsoft.com/en-us/library/hh659001.aspx
	/// </summary>
	[Route("wopi/[controller]")]
	public class FilesController : WopiControllerBase
	{
		private readonly IAuthorizationService _authorizationService;

		private WopiDiscoverer _wopiDiscoverer;

		//TODO: storage for lock should be persistent
		private static readonly Dictionary<string, LockInfo> Locks = new Dictionary<string, LockInfo>();


		private WopiDiscoverer WopiDiscoverer
		{
			get { return _wopiDiscoverer ?? (_wopiDiscoverer = new WopiDiscoverer(Configuration.GetValue("WopiClientUrl", string.Empty))); }
		}

		public FilesController(IWopiStorageProvider storageProvider, IWopiSecurityHandler securityHandler, IConfiguration configuration, IAuthorizationService authorizationService) : base(storageProvider, securityHandler, configuration)
		{
			_authorizationService = authorizationService;
		}

		private async Task<AbstractEditSession> GetEditSessionAsync(string fileId)
		{
			var sessionId = /*Context.Session.GetString("SessionID");
			if (string.IsNullOrEmpty(sessionId))
			{
				sessionId = Guid.NewGuid().ToString();
				Context.Session.SetString("SessionID", sessionId);
			}
			sessionId += "|" +*/ fileId;
			AbstractEditSession editSession = SessionManager.Current.GetSession(sessionId);

			if (editSession == null)
			{
				IWopiFile file = StorageProvider.GetWopiFile(fileId);

				//TODO: remove hardcoded action 'Edit'
				//TODO: handle all requirements in a generic way (requires="cobalt,containers,update")
				//TODO: http://wopi.readthedocs.io/en/latest/discovery.html#action-requirements
				if (await WopiDiscoverer.RequiresCobaltAsync(file.Extension, WopiActionEnum.Edit))
				{
					editSession = new CobaltSession(file, sessionId);
				}
				else
				{
					editSession = new FileSession(file, sessionId);
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
		/// <param name="access_token">Access token used to validate the request.</param>
		/// <returns></returns>
		[HttpGet("{id}")]
		[Produces("application/json")]
		public async Task<CheckFileInfo> GetCheckFileInfo(string id, [FromQuery]string access_token)
		{
			return (await GetEditSessionAsync(id))?.GetCheckFileInfo();
		}

		/// <summary>
		/// Returns contents of a file specified by an identifier.
		/// Specification: https://msdn.microsoft.com/en-us/library/hh657944.aspx
		/// Example URL: HTTP://server/<...>/wopi*/files/<id>/contents
		/// </summary>
		/// <param name="id">File identifier.</param>
		/// <param name="access_token">Access token used to validate the request.</param>
		/// <returns></returns>
		[HttpGet("{id}/contents")]
		[Produces("application/octet-stream")]
		public async Task<ActionResult> GetContents(string id, [FromQuery]string access_token)
		{
			//TODO: implement authorization
			//if (!await _authorizationService.AuthorizeAsync(User, new TokenContainer { FileId = id, Token = access_token }, PolicyNames.HasValidAccessToken))
			//{
			//	return Challenge();
			//}

			var editSession = await GetEditSessionAsync(id);
			//TODO: consider using return new Microsoft.AspNetCore.Mvc.FileStreamResult(editSession.GetFileContent(), "application/octet-stream");
			return new FileContentResult(editSession.GetFileContent(), "application/octet-stream");
		}

		/// <summary>
		/// Updates a file specified by an identifier. (Only for non-cobalt files.)
		/// Specification: https://msdn.microsoft.com/en-us/library/hh657364.aspx
		/// Example URL: HTTP://server/<...>/wopi*/files/<id>/contents
		/// </summary>
		/// <param name="id">File identifier.</param>
		/// <param name="access_token">Access token used to validate the request.</param>
		/// <returns></returns>
		[HttpPut("{id}/contents")]
		[HttpPost("{id}/contents")]
		[Produces("application/octet-stream")]
		public async Task<IActionResult> PutFile(string id, [FromQuery]string access_token)
		{
			var editSession = await GetEditSessionAsync(id);
			editSession.SetFileContent(await HttpContext.Request.Body.ReadBytesAsync());
			return new OkResult();
		}

		/// <summary>
		/// The PutRelativeFile operation creates a new file on the host based on the current file.
		/// </summary>
		/// <param name="id"></param>
		/// <param name="access_token"></param>
		/// <returns></returns>
		public IActionResult PutRelativeFile(string id, [FromQuery] string access_token)
		{
			//TODO: implement as a filter, middleware or something...
			//string newLock = Request.Headers[WopiHeaders.Lock];
			//LockInfo existingLock;
			//bool hasExistingLock;

			//lock (Locks)
			//{
			//	hasExistingLock = TryGetLock(id, out existingLock);
			//}

			//if (hasExistingLock && existingLock.Lock != newLock)
			//{
			//	// lock mismatch/locked by another interface
			//	return ReturnLockMismatch(Response, existingLock.Lock);
			//}

			//TODO: implement according to https://wopirest.readthedocs.io/en/latest/files/PutRelativeFile.html
			return new OkResult();
		}

		/// <summary>
		/// Changes the contents of the file in accordance with [MS-FSSHTTP] and performs other operations like locking.
		/// MS-FSSHTTP Specification: https://msdn.microsoft.com/en-us/library/dd943623.aspx
		/// Specification: https://msdn.microsoft.com/en-us/library/hh659581.aspx
		/// Example URL: HTTP://server/<...>/wopi*/files/<id>
		/// </summary>
		/// <param name="id"></param>
		/// <param name="access_token"></param>
		[HttpPost("{id}")]
		[Produces("application/octet-stream", "text/html")]
		public async Task<IActionResult> PerformAction(string id, [FromQuery]string access_token)
		{
			var editSession = await GetEditSessionAsync(id);
			string wopiOverrideHeader = HttpContext.Request.Headers[WopiHeaders.WopiOverride];

			//TODO: Replace the else-ifs with separate methods (https://github.com/petrsvihlik/WopiHost/issues/7)
			// http://stackoverflow.com/questions/39302121/header-based-routing-in-asp-net-core

			if (wopiOverrideHeader.Equals("COBALT"))
			{
				var responseAction = editSession.SetFileContent(await HttpContext.Request.Body.ReadBytesAsync());

				HttpContext.Response.Headers.Add(WopiHeaders.CorrelationId, HttpContext.Request.Headers[WopiHeaders.CorrelationId]);
				HttpContext.Response.Headers.Add("request-id", HttpContext.Request.Headers[WopiHeaders.CorrelationId]);

				return new Results.FileResult(responseAction, "application/octet-stream");
			}
			else if (wopiOverrideHeader.Equals("LOCK") || wopiOverrideHeader.Equals("UNLOCK") || wopiOverrideHeader.Equals("REFRESH_LOCK") || wopiOverrideHeader.Equals("GET_LOCK"))
			{
				string oldLock = Request.Headers[WopiHeaders.OldLock];
				string newLock = Request.Headers[WopiHeaders.Lock];

				LockInfo existingLock = null;
				bool lockAcquired = TryGetLock(id, out existingLock);
				lock (Locks)
				{
					switch (wopiOverrideHeader)
					{
						case "GET_LOCK":
							break;

						case "LOCK":
							if (oldLock != null)
							{
								if (lockAcquired)
								{
									if (existingLock.Lock == oldLock)
									{
										// Replace the existing lock with the new one
										Locks[id] = new LockInfo { DateCreated = DateTime.UtcNow, Lock = newLock };
										Response.Headers[WopiHeaders.OldLock] = newLock;
										return new OkResult();
									}
									else
									{
										// The existing lock doesn't match the requested one.  Return a lock mismatch error along with the current lock
										return ReturnLockMismatch(Response, existingLock.Lock);
									}
								}
								else
								{
									// The requested lock does not exist.  That's also a lock mismatch error.
									return ReturnLockMismatch(Response, reason: "File not locked");
								}
							}
							else
							{
								if (lockAcquired)
								{
									// There is a valid existing lock on the file
									return ReturnLockMismatch(Response, existingLock.Lock);
								}
								else
								{
									// The file is not currently locked, create and store new lock information
									Locks[id] = new LockInfo { DateCreated = DateTime.UtcNow, Lock = newLock };
									return new OkResult();
								}
							}

						case "UNLOCK":
							if (lockAcquired)
							{
								if (existingLock.Lock == newLock)
								{
									// Remove valid lock
									Locks.Remove(id);
									return new OkResult();
								}
								else
								{
									// The existing lock doesn't match the requested one.  Return a lock mismatch error along with the current lock
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
			else
			{
				// Unsupported action
				return new NotImplementedResult();
			}
		}

		private bool TryGetLock(string fileId, out LockInfo lockInfo)
		{
			//TODO: This lock implementation is not thread safe and not persisted and all in all just an example.
			if (Locks.TryGetValue(fileId, out lockInfo))
			{
				if (lockInfo.Expired)
				{
					Locks.Remove(fileId);
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
	}
}
